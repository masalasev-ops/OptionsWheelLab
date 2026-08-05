using System.Globalization;
using System.Text;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// A candidate's non-monetary features, as the JSON `candidates.feature_json`
/// holds.
/// </summary>
/// <remarks>
/// <b>Nothing denominated in money appears here, and that is the line rather than
/// a coincidence.</b> Money is decimal in TEXT and
/// <see cref="DecimalColumns"/> is what governs it: a value inside a blob is one
/// the canonical form does not reach and the no-ordering rule cannot see. So
/// `bid` and `ask` are columns, and a future feature denominated in money is a
/// column too.
/// <para>
/// <b>The line does work rather than describing what is already true.</b> Of the
/// six features `SYSTEM_DESIGN.md` §3.11 names, spread width is money and would
/// be a column, except that it is the ask less the bid and both are columns
/// already, so it is not stored at all. Implied volatility <i>rank</i> and term
/// structure slope are on neither side: rank needs a history window and slope
/// needs the rest of the chain, so neither can be computed from a candidate's own
/// quote whatever the line says. What remains of the six are delta, days to
/// expiry and distance to earnings.
/// </para>
/// <para>
/// <b>Distance to earnings is absent, and that is a gap rather than a choice.</b>
/// It needs the report dates the gate read, which the generator holds and a
/// candidate does not carry. Recorded here so the checkpoint that wants the
/// feature finds the reason rather than the omission.
/// </para>
/// <para>
/// Written by hand rather than serialised, so the decimal form is
/// <see cref="StoreDecimal"/>'s and not a serialiser's. A serialiser would render
/// a decimal by its own rules, and the one rule this corpus has about where
/// decimals live would stop at the blob's edge.
/// </para>
/// </remarks>
public static class CandidateFeatures
{
    /// <summary>The features of this quote, as stored JSON.</summary>
    public static string Json(ContractQuote quote, DateOnly session)
    {
        ArgumentNullException.ThrowIfNull(quote);

        var json = new StringBuilder("{");

        // Days to expiry is a count and is computed here rather than stored twice:
        // the expiry is on the contract and the session is the set's.
        json.Append(CultureInfo.InvariantCulture, $"\"dte\":{quote.Contract.Expiry.DayNumber - session.DayNumber}");

        Decimal(json, "delta", quote.Delta);
        Decimal(json, "iv", quote.ImpliedVolatility);
        Integer(json, "volume", quote.Volume);
        Integer(json, "open_interest", quote.OpenInterest);

        return json.Append('}').ToString();
    }

    /// <summary>
    /// A feature absent from the quote is absent from the JSON, never zero.
    /// </summary>
    /// <remarks>
    /// The convention `contract_quotes` already carries: a gamma of zero is a
    /// false observation and not a missing one [§4.1], and a feature grader
    /// reading a zero delta it invented would grade a contract nobody quoted.
    /// </remarks>
    private static void Decimal(StringBuilder json, string name, decimal? value)
    {
        if (value is { } present)
        {
            json.Append(CultureInfo.InvariantCulture, $",\"{name}\":\"{StoreDecimal.ToStored(present)}\"");
        }
    }

    private static void Integer(StringBuilder json, string name, long? value)
    {
        if (value is { } present)
        {
            json.Append(CultureInfo.InvariantCulture, $",\"{name}\":{present}");
        }
    }
}
