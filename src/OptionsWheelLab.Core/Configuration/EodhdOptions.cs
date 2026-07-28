namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// The <c>Eodhd</c> section, the only section <c>CONFIG_REFERENCE.md</c> classes
/// as <c>app</c>. None of these values participates in producing or scoring a
/// simulated decision, so none needs as-of resolution [D-W27].
/// </summary>
public sealed class EodhdOptions
{
    public const string SectionPath = "Eodhd";

    /// <summary>API root. Unset; set at Phase 8 [D-W7].</summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Credential. Supplied by <c>appsettings.Secrets.json</c>, which is never
    /// committed. Null on a fresh clone.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Whether the options add-on is purchased. False until Phase 8 [D-W7].</summary>
    public bool OptionsAddOnEnabled { get; set; }
}
