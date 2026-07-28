namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// Resolves where the store and its snapshots live, from a configured absolute
/// directory.
/// </summary>
/// <remarks>
/// The directory is machine-supplied and validated as rooted here rather than
/// at binding time, so a process that merely binds configuration is unaffected
/// while one that opens the store fails fast.
/// <para>
/// Nothing in this type consults <see cref="AppContext.BaseDirectory"/> or the
/// working directory. The Worker and the Api have different values for both, so
/// deriving the location from either would let the same store resolve to two
/// different places, which is the defect this type exists to prevent.
/// </para>
/// </remarks>
public sealed class StoreLocation
{
    public const string EnvironmentVariable = "Storage__Path";

    public const string DatabaseFileName = "optionswheellab.db";

    private StoreLocation(string directory)
    {
        Directory = directory;
        DatabasePath = System.IO.Path.Combine(directory, DatabaseFileName);
    }

    /// <summary>The absolute directory holding the store and its snapshots.</summary>
    public string Directory { get; }

    /// <summary>The absolute path of the database file itself.</summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Validates a configured directory and resolves the store location.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The directory is missing, blank, or not rooted.
    /// </exception>
    public static StoreLocation From(StorageOptionsView options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options.Path;

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"Storage:Path is not set. Supply an absolute directory in the environment "
                + $"variable {EnvironmentVariable}. It is committed empty because a committed "
                + "absolute path would start on one machine only.");
        }

        if (!System.IO.Path.IsPathRooted(configured))
        {
            throw new InvalidOperationException(
                $"Storage:Path must be an absolute directory, but '{configured}' is relative. "
                + $"Set {EnvironmentVariable} to a rooted path. A relative path is refused "
                + "because the Worker and the Api have different working directories, so the "
                + "same value would resolve to two different stores.");
        }

        return new StoreLocation(System.IO.Path.GetFullPath(configured));
    }
}

/// <summary>
/// The part of <c>StorageOptions</c> the storage layer needs, so
/// <see cref="StoreLocation"/> does not depend on the options binding.
/// </summary>
public sealed record StorageOptionsView(string? Path);
