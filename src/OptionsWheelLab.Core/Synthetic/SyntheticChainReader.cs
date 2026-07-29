using System.Globalization;
using System.Text.Json;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Synthetic;

/// <summary>
/// Reads a hand-written synthetic chain [D-W31].
/// </summary>
/// <remarks>
/// A pure function over text, following <c>ConfigReferenceParser</c>, which is
/// how this repository already reads a hand-written file. It resolves no paths
/// and opens no files, so it needs no configured root and introduces no
/// configuration key; the caller supplies the text.
/// <para>
/// <b>Every value in a chain is a JSON string, including the numbers.</b> One
/// rule, so nothing has to be decided at the keyboard. It also closes a hole the
/// source guard states it cannot see: an unquoted JSON number binds to a
/// <c>double</c> and no scan in this repository would catch it. Quoted, the file
/// carries exactly the text <see cref="StoreDecimal.ParseStored"/> reads, and
/// there is no conversion layer to get wrong.
/// </para>
/// <para>
/// <b>The shape is a chain, not a table.</b> Symbol, snapshot date, expiry and
/// right are stated once and the strike rows carry only what varies. Three of
/// those four make up contract identity, so a schema-mirroring row that repeated
/// them would turn a typo into a different contract rather than into an error
/// [D-W29].
/// </para>
/// <para>
/// <b>Nothing is returned unless everything read.</b> The whole document is
/// checked, every problem collected, and a chain built only if there were none.
/// </para>
/// </remarks>
public static class SyntheticChainReader
{
    private static readonly JsonDocumentOptions Options = new()
    {
        // Hand-written files carry commentary. WORKED_EXAMPLE §5 annotates its
        // bars with "trial opens" and "low of the trial window", and a format
        // that cannot hold that pushes it out of the file and into nowhere.
        CommentHandling = JsonCommentHandling.Skip,

        // A trailing comma survives reordering a list by hand, which is the edit
        // these files get most.
        AllowTrailingCommas = true,
    };

    private static readonly string[] RootProperties = ["symbol", "bars", "chains"];

    private static readonly string[] BarProperties =
        ["date", "close", "open", "high", "low", "adjustedClose", "volume"];

    private static readonly string[] ChainProperties = ["date", "contracts"];

    private static readonly string[] ContractProperties = ["expiry", "right", "quotes"];

    private static readonly string[] QuoteProperties =
    [
        "strike", "bid", "ask", "last", "volume", "openInterest",
        "impliedVolatility", "delta", "gamma", "theta", "vega",
    ];

    /// <summary>
    /// The chain the text states, or <see cref="MalformedChainException"/>
    /// carrying every reason it does not.
    /// </summary>
    public static SyntheticChain Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json, Options);
        }
        catch (JsonException exception)
        {
            // Not a document at all, so there is nothing to collect problems
            // from and this is the one reason reported alone.
            throw new MalformedChainException(
                [$"the file is not readable as JSON: {exception.Message}"],
                exception);
        }

        using (document)
        {
            var problems = new List<string>();
            var chain = Build(document.RootElement, problems);

            if (problems.Count != 0 || chain is null)
            {
                throw new MalformedChainException(problems);
            }

            return chain;
        }
    }

    private static SyntheticChain? Build(JsonElement root, List<string> problems)
    {
        if (!IsObject(root, "the file", problems))
        {
            return null;
        }

        RefuseUnknown(root, RootProperties, "the file", problems);

        var symbol = ReadSymbol(root, problems);
        var bars = ReadBars(root, symbol, problems);
        var quotes = ReadChains(root, symbol, problems);

        if (symbol is null)
        {
            return null;
        }

        RefuseDuplicateBars(bars, problems);
        RefuseDuplicateQuotes(quotes, problems);

        return new SyntheticChain(
            symbol,
            [.. bars.OrderBy(bar => bar.SessionDate)],
            [.. quotes
                .OrderBy(quote => quote.SnapshotDate)
                .ThenBy(quote => quote.Contract)]);
    }

    private static Ticker? ReadSymbol(JsonElement root, List<string> problems)
    {
        var raw = RequiredString(root, "symbol", "the file", problems);

        if (raw is null)
        {
            return null;
        }

        if (Ticker.TryNormalise(raw, out var ticker, out var reason))
        {
            return ticker;
        }

        problems.Add($"symbol: {reason}");
        return null;
    }

    private static List<UnderlyingBar> ReadBars(
        JsonElement root,
        Ticker? symbol,
        List<string> problems)
    {
        var bars = new List<UnderlyingBar>();

        if (!TryArray(root, "bars", "the file", problems, out var array))
        {
            return bars;
        }

        var index = 0;

        foreach (var element in array.EnumerateArray())
        {
            var path = $"bars[{index++}]";

            if (!IsObject(element, path, problems))
            {
                continue;
            }

            RefuseUnknown(element, BarProperties, path, problems);

            var date = RequiredDate(element, "date", path, problems);
            var close = RequiredDecimal(element, "close", path, problems);

            var open = OptionalDecimal(element, "open", path, problems);
            var high = OptionalDecimal(element, "high", path, problems);
            var low = OptionalDecimal(element, "low", path, problems);
            var adjusted = OptionalDecimal(element, "adjustedClose", path, problems);
            var volume = OptionalCount(element, "volume", path, problems);

            if (symbol is null || date is null || close is null)
            {
                continue;
            }

            bars.Add(new UnderlyingBar(
                symbol,
                date.Value,
                close.Value,
                open,
                high,
                low,
                adjusted,
                volume));
        }

        return bars;
    }

    private static List<ContractQuote> ReadChains(
        JsonElement root,
        Ticker? symbol,
        List<string> problems)
    {
        var quotes = new List<ContractQuote>();

        if (!TryArray(root, "chains", "the file", problems, out var array))
        {
            return quotes;
        }

        var chainIndex = 0;

        foreach (var chain in array.EnumerateArray())
        {
            var chainPath = $"chains[{chainIndex++}]";

            if (!IsObject(chain, chainPath, problems))
            {
                continue;
            }

            RefuseUnknown(chain, ChainProperties, chainPath, problems);

            var snapshotDate = RequiredDate(chain, "date", chainPath, problems);

            if (!TryArray(chain, "contracts", chainPath, problems, out var contracts))
            {
                continue;
            }

            var contractIndex = 0;

            foreach (var contract in contracts.EnumerateArray())
            {
                var contractPath = $"{chainPath}.contracts[{contractIndex++}]";
                ReadContract(contract, contractPath, symbol, snapshotDate, quotes, problems);
            }
        }

        return quotes;
    }

    private static void ReadContract(
        JsonElement contract,
        string path,
        Ticker? symbol,
        DateOnly? snapshotDate,
        List<ContractQuote> quotes,
        List<string> problems)
    {
        if (!IsObject(contract, path, problems))
        {
            return;
        }

        RefuseUnknown(contract, ContractProperties, path, problems);

        var expiry = RequiredDate(contract, "expiry", path, problems);
        var right = RequiredRight(contract, path, problems);

        // An expired contract cannot appear in a snapshot taken after it. The
        // expiry day itself is admitted, since a contract quotes on the morning
        // it expires.
        if (expiry is not null && snapshotDate is not null && expiry < snapshotDate)
        {
            problems.Add(
                $"{path}.expiry: '{StoreDate.ToStored(expiry.Value)}' is before the snapshot "
                + $"date '{StoreDate.ToStored(snapshotDate.Value)}', so the contract had "
                + "already expired when the chain was taken.");
        }

        if (!TryArray(contract, "quotes", path, problems, out var array))
        {
            return;
        }

        var index = 0;

        foreach (var quote in array.EnumerateArray())
        {
            var quotePath = $"{path}.quotes[{index++}]";
            ReadQuote(quote, quotePath, symbol, snapshotDate, expiry, right, quotes, problems);
        }
    }

    private static void ReadQuote(
        JsonElement quote,
        string path,
        Ticker? symbol,
        DateOnly? snapshotDate,
        DateOnly? expiry,
        OptionRight? right,
        List<ContractQuote> quotes,
        List<string> problems)
    {
        if (!IsObject(quote, path, problems))
        {
            return;
        }

        RefuseUnknown(quote, QuoteProperties, path, problems);

        var strike = RequiredDecimal(quote, "strike", path, problems);
        var bid = RequiredDecimal(quote, "bid", path, problems);
        var ask = RequiredDecimal(quote, "ask", path, problems);

        var last = OptionalDecimal(quote, "last", path, problems);
        var volume = OptionalCount(quote, "volume", path, problems);
        var openInterest = OptionalCount(quote, "openInterest", path, problems);
        var iv = OptionalDecimal(quote, "impliedVolatility", path, problems);
        var delta = OptionalDecimal(quote, "delta", path, problems);
        var gamma = OptionalDecimal(quote, "gamma", path, problems);
        var theta = OptionalDecimal(quote, "theta", path, problems);
        var vega = OptionalDecimal(quote, "vega", path, problems);

        RefuseImpossibleMarket(path, bid, ask, problems);

        if (symbol is null || snapshotDate is null || expiry is null || right is null
            || strike is null || bid is null || ask is null)
        {
            return;
        }

        ContractIdentity identity;

        try
        {
            identity = ContractIdentity.Of(symbol, expiry.Value, right.Value, strike.Value);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            problems.Add($"{path}.strike: {exception.Message}");
            return;
        }

        quotes.Add(new ContractQuote(
            identity,
            snapshotDate.Value,
            bid.Value,
            ask.Value,
            last,
            volume,
            openInterest,
            iv,
            delta,
            gamma,
            theta,
            vega));
    }

    /// <summary>
    /// The one domain rule this reader enforces, and it is deliberate.
    /// </summary>
    /// <remarks>
    /// A crossed market is not an observation that existed, and a hand-written
    /// one is a transposition. It matters more than it looks: the spread cap is a
    /// fraction of mid [D-W22], so a crossed quote gives a negative numerator and
    /// passes a cap that exists to reject wide markets.
    /// <para>
    /// The cost is recorded in carried obligations rather than here, because it
    /// is real: no synthetic chain can now express a crossed or locked market, so
    /// nothing can exercise the gate against one. Phase 2 decides whether the
    /// gate handles it, and if it does this refusal moves there.
    /// </para>
    /// </remarks>
    private static void RefuseImpossibleMarket(
        string path,
        decimal? bid,
        decimal? ask,
        List<string> problems)
    {
        if (bid is < 0)
        {
            problems.Add($"{path}.bid: {bid} is negative, which is not a market that existed.");
        }

        if (ask is < 0)
        {
            problems.Add($"{path}.ask: {ask} is negative, which is not a market that existed.");
        }

        if (bid is not null && ask is not null && bid > ask)
        {
            problems.Add(
                $"{path}: the bid {bid} is above the ask {ask}, which is a crossed market and "
                + "is almost always a transposition. It is refused rather than carried, because "
                + "a spread taken as a fraction of mid would come out negative and pass a cap "
                + "meant to reject wide markets.");
        }
    }

    private static void RefuseDuplicateBars(List<UnderlyingBar> bars, List<string> problems)
    {
        foreach (var duplicate in bars
            .GroupBy(bar => bar.SessionDate)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key))
        {
            problems.Add(
                $"bars: {StoreDate.ToStored(duplicate.Key)} appears {duplicate.Count()} times. A "
                + "session has one bar, and two would leave which one is the bar undecided.");
        }
    }

    private static void RefuseDuplicateQuotes(List<ContractQuote> quotes, List<string> problems)
    {
        foreach (var duplicate in quotes
            .GroupBy(quote => (quote.SnapshotDate, quote.Contract))
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key.SnapshotDate)
            .ThenBy(group => group.Key.Contract))
        {
            problems.Add(
                $"chains: {duplicate.Key.Contract} appears {duplicate.Count()} times on "
                + $"{StoreDate.ToStored(duplicate.Key.SnapshotDate)}. A contract has one quote "
                + "per snapshot date, and two would leave which one it was undecided.");
        }
    }

    private static bool IsObject(JsonElement element, string path, List<string> problems)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        problems.Add($"{path}: expected an object and found {Describe(element.ValueKind)}.");
        return false;
    }

    /// <summary>
    /// An unrecognised property is refused rather than ignored.
    /// </summary>
    /// <remarks>
    /// This is the worst failure a hand-written file has: <c>"dleta"</c> ignored
    /// silently leaves the delta absent, the chain loads, and the value is gone
    /// with nothing to show for it.
    /// </remarks>
    private static void RefuseUnknown(
        JsonElement element,
        string[] known,
        string path,
        List<string> problems)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!known.Contains(property.Name, StringComparer.Ordinal))
            {
                problems.Add(
                    $"{path}: '{property.Name}' is not a property a synthetic chain has. The "
                    + $"ones here are {string.Join(", ", known)}. A misspelling is refused "
                    + "rather than ignored, because ignoring it drops the value silently.");
            }
        }
    }

    private static bool TryArray(
        JsonElement parent,
        string name,
        string path,
        List<string> problems,
        out JsonElement array)
    {
        array = default;

        if (!parent.TryGetProperty(name, out var element))
        {
            problems.Add($"{path}: '{name}' is required and is absent.");
            return false;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            problems.Add(
                $"{path}.{name}: expected an array and found {Describe(element.ValueKind)}.");
            return false;
        }

        array = element;
        return true;
    }

    private static string? RequiredString(
        JsonElement parent,
        string name,
        string path,
        List<string> problems)
    {
        if (!parent.TryGetProperty(name, out var element))
        {
            problems.Add($"{path}: '{name}' is required and is absent.");
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            problems.Add(
                $"{path}.{name}: expected a quoted value and found "
                + $"{Describe(element.ValueKind)}. Every value in a synthetic chain is quoted, "
                + "including the numbers, so a number can never be read as floating point.");
            return null;
        }

        return element.GetString();
    }

    private static decimal? RequiredDecimal(
        JsonElement parent,
        string name,
        string path,
        List<string> problems) =>
        ParseDecimal(RequiredString(parent, name, path, problems), $"{path}.{name}", problems);

    private static decimal? OptionalDecimal(
        JsonElement parent,
        string name,
        string path,
        List<string> problems) =>
        parent.TryGetProperty(name, out _)
            ? RequiredDecimal(parent, name, path, problems)
            : null;

    /// <summary>
    /// Through the stored form, never <c>decimal.Parse</c>.
    /// </summary>
    /// <remarks>
    /// The parsing path refuses more decimal places than the scale holds, counted
    /// on the string. A hand-written value is exact, so a value beyond the scale
    /// is a malformed chain rather than one to round: rounding it would make the
    /// chain read back as a different chain from the one written [D-W29, D-W31].
    /// </remarks>
    private static decimal? ParseDecimal(string? text, string path, List<string> problems)
    {
        if (text is null)
        {
            return null;
        }

        try
        {
            return StoreDecimal.ParseStored(text);
        }
        catch (FormatException exception)
        {
            problems.Add($"{path}: {exception.Message}");
            return null;
        }
    }

    private static DateOnly? RequiredDate(
        JsonElement parent,
        string name,
        string path,
        List<string> problems)
    {
        var text = RequiredString(parent, name, path, problems);

        if (text is null)
        {
            return null;
        }

        try
        {
            return StoreDate.ParseStored(text);
        }
        catch (FormatException)
        {
            problems.Add(
                $"{path}.{name}: '{text}' is not a date in the form "
                + $"{StoreDate.StoredFormat}.");
            return null;
        }
    }

    private static OptionRight? RequiredRight(
        JsonElement parent,
        string path,
        List<string> problems)
    {
        var text = RequiredString(parent, "right", path, problems);

        if (text is null)
        {
            return null;
        }

        try
        {
            return StoreOptionRight.ParseStored(text);
        }
        catch (FormatException exception)
        {
            problems.Add($"{path}.right: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// A volume or an open interest: a whole count, quoted like everything else.
    /// </summary>
    /// <remarks>
    /// No sign is admitted, which is a refusal rather than an oversight. Neither
    /// quantity can be negative, and the alternative is a chain that states one.
    /// </remarks>
    private static long? OptionalCount(
        JsonElement parent,
        string name,
        string path,
        List<string> problems)
    {
        if (!parent.TryGetProperty(name, out _))
        {
            return null;
        }

        var text = RequiredString(parent, name, path, problems);

        if (text is null)
        {
            return null;
        }

        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return count;
        }

        problems.Add(
            $"{path}.{name}: '{text}' is not a whole count. It is written in digits with no "
            + "sign, no separators and no decimal point.");
        return null;
    }

    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Undefined => "nothing",
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a quoted value",
        JsonValueKind.Number => "an unquoted number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Null => "null",
        _ => "something unrecognised",
    };
}
