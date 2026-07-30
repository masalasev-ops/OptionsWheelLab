using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.MarketData;

/// <summary>One generation of a contract's lineage.</summary>
public sealed record LineageEntry(
    long ContractId,
    ContractIdentity Identity,
    long? PredecessorContractId);

/// <summary>
/// Walks a contract's predecessors, all generations, newest first.
/// </summary>
/// <remarks>
/// <b>Its own small reader, and deliberately not a member of
/// <see cref="AsOfMarketData"/>.</b> Contracts carry no observation axis: a
/// corporate action mints a new identity rather than restating a row [§4.1],
/// so lineage is timeless, and a by-name <c>asOf</c> member here would claim a
/// filter the schema cannot honour. The point-in-time discipline lives at the
/// quotes, which carry both axes.
/// <para>
/// The walk is the recursive CTE 1.1 proved clean under the alias convention:
/// a CTE names its working set rather than renaming the table, so the
/// historical join across a split needs no self-join and nothing acquires two
/// names. The <c>contracts_predecessor</c> index is the only access path to a
/// predecessor link and exists for exactly this query.
/// </para>
/// </remarks>
public sealed class ContractLineage
{
    private readonly SqliteConnection _connection;

    public ContractLineage(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// The contract and every predecessor, in generation order starting at
    /// <paramref name="contractId"/>, empty when no such contract exists.
    /// </summary>
    public IReadOnlyList<LineageEntry> WalkFrom(long contractId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            WITH RECURSIVE lineage(contract_id, symbol, expiry, right, strike,
                                   deliverable_shares, predecessor_contract_id,
                                   generation) AS (
                SELECT contract_id, symbol, expiry, right, strike,
                       deliverable_shares, predecessor_contract_id, 0
                FROM contracts
                WHERE contract_id = $start
                UNION ALL
                SELECT contracts.contract_id, contracts.symbol, contracts.expiry,
                       contracts.right, contracts.strike,
                       contracts.deliverable_shares,
                       contracts.predecessor_contract_id,
                       lineage.generation + 1
                FROM contracts
                JOIN lineage ON contracts.contract_id = lineage.predecessor_contract_id
            )
            SELECT contract_id, symbol, expiry, right, strike,
                   deliverable_shares, predecessor_contract_id
            FROM lineage
            ORDER BY generation;
            """;
        command.Parameters.AddWithValue("$start", contractId);

        var generations = new List<LineageEntry>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            generations.Add(new LineageEntry(
                reader.GetInt64(0),
                ContractIdentity.Of(
                    Ticker.Normalise(reader.GetString(1)),
                    StoreDate.ParseStored(reader.GetString(2)),
                    StoreOptionRight.ParseStored(reader.GetString(3)),
                    StoreDecimal.ParseStored(reader.GetString(4)),
                    reader.GetInt32(5)),
                reader.IsDBNull(6) ? null : reader.GetInt64(6)));
        }

        return generations;
    }
}
