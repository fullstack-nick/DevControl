namespace DevControl.Cli;

public sealed class ParsedArgs
{
    private readonly Dictionary<string, string> values;
    private readonly HashSet<string> flags;

    private ParsedArgs(Dictionary<string, string> values, HashSet<string> flags)
    {
        this.values = values;
        this.flags = flags;
    }

    public static ParsedArgs Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new CliUsageException($"Unexpected argument '{arg}'.");
            }

            var key = arg[2..];
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new CliUsageException("Empty option name.");
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                flags.Add(key);
                continue;
            }

            values[key] = args[++i];
        }

        return new ParsedArgs(values, flags);
    }

    public string? Value(string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    public string Required(string key)
    {
        return Value(key) ?? throw new CliUsageException($"--{key} is required.");
    }

    public bool HasFlag(string key)
    {
        return flags.Contains(key);
    }
}
