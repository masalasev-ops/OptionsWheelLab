namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// The <c>Storage</c> section, holding where the store and its snapshots live.
/// </summary>
/// <remarks>
/// <c>app</c>-classed by necessity rather than by the read-path criterion: a
/// value the process needs in order to open the store cannot be stored in the
/// store [D-W27]. The connection factory sits on every path including simulated
/// ones, so arguing this participates in no decision would be the weaker claim.
/// The reason is circularity.
/// <para>
/// Carries no <c>[Required]</c> and no <c>ValidateOnStart</c>. 0.2 established
/// that binding-time validation of an unset key would stop a host whose
/// configuration is incomplete by design from starting at all. The path is
/// checked where it is used instead, in
/// <see cref="Storage.StoreConnectionFactory"/>, so a process that merely binds
/// configuration is unaffected while one that opens the store fails fast.
/// </para>
/// </remarks>
public sealed class StorageOptions
{
    public const string SectionPath = "Storage";

    /// <summary>
    /// Absolute directory holding the store and its snapshots.
    /// </summary>
    /// <remarks>
    /// Committed empty and supplied per machine through the environment
    /// variable <c>Storage__Path</c>. A committed absolute path would start the
    /// Worker on one machine only and would publish a filesystem layout to a
    /// public repository.
    /// </remarks>
    public string? Path { get; set; }
}
