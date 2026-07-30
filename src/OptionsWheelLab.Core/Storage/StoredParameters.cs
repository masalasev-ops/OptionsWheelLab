using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The one path a typed value takes to a parameter, rendering through its
/// stored form.
/// </summary>
/// <remarks>
/// This is D-W29's write-side seam, owed since 0.4 against the first real
/// decimal column, which migration 5's bars and the chain writer's quotes now
/// are. A call site that renders by hand can misrender in ways the store
/// accepts, and a misrendered strike is a different contract rather than an
/// error, which is the failure the canonical form exists to prevent.
/// <para>
/// <b>The decimal overload renders through the refusing entry point.</b>
/// Synthetic values are exact [D-W31] and nothing computes a decimal yet, so a
/// value that needs rounding reaching a parameter is an error today rather
/// than a policy. When Phase 3 computes values, the rounding path is a
/// deliberate call to <see cref="StoreDecimal.ToStoredRounded"/> before the
/// bind, visible at the site, not a quiet default inside this seam.
/// </para>
/// <para>
/// <b>What stays convention, stated.</b> Nothing structural prevents a call
/// site binding a hand-rendered string; a type-level check was declined at
/// D-W33. This is the sanctioned path that review holds call sites to, and the
/// chain writer is its first exclusive consumer.
/// </para>
/// </remarks>
public static class StoredParameters
{
    public static void AddStored(
        this SqliteParameterCollection parameters, string name, decimal value)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.AddWithValue(name, StoreDecimal.ToStored(value));
    }

    public static void AddStored(
        this SqliteParameterCollection parameters, string name, decimal? value)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.AddWithValue(
            name, value is null ? DBNull.Value : StoreDecimal.ToStored(value.Value));
    }

    public static void AddStored(
        this SqliteParameterCollection parameters, string name, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.AddWithValue(name, StoreDate.ToStored(date));
    }

    public static void AddStored(
        this SqliteParameterCollection parameters, string name, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.AddWithValue(name, StoreTimestamp.ToStored(instant));
    }

    public static void AddStored(
        this SqliteParameterCollection parameters, string name, OptionRight right)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.AddWithValue(name, StoreOptionRight.ToStored(right));
    }
}
