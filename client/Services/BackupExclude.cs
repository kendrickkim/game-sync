namespace GameSync.Services;

public static class BackupExclude
{
    public static string NormalizeRelative(string relativePath)
    {
        var value = (relativePath ?? "").Replace('/', Path.DirectorySeparatorChar).Trim();
        value = value.TrimStart(Path.DirectorySeparatorChar);
        while (value.EndsWith(Path.DirectorySeparatorChar))
        {
            value = value[..^1];
        }

        return value;
    }

    public static List<string> NormalizeList(string? root, IEnumerable<string>? excludes)
    {
        var result = new List<string>();
        if (excludes is null)
        {
            return result;
        }

        foreach (var item in excludes)
        {
            var normalized = NormalizeRelative(item);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(root) && Path.IsPathRooted(normalized))
            {
                if (!TryMakeRelative(root, normalized, out var relative))
                {
                    continue;
                }

                normalized = relative;
            }

            if (result.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(normalized);
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return PruneCovered(result);
    }

    public static bool TryMakeRelative(string root, string fullPath, out string relative)
    {
        relative = "";
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(root, fullPath);
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath == "." ||
            relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            return false;
        }

        relative = NormalizeRelative(relativePath);
        return !string.IsNullOrWhiteSpace(relative);
    }

    public static bool IsExcluded(string fullPath, string root, IReadOnlyCollection<string>? excludes)
    {
        if (excludes is null || excludes.Count == 0 || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        if (!TryMakeRelative(root, fullPath, out var relative))
        {
            return false;
        }

        foreach (var exclude in excludes)
        {
            var normalized = NormalizeRelative(exclude);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (string.Equals(relative, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var prefix = normalized + Path.DirectorySeparatorChar;
            if (relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string Summarize(IReadOnlyCollection<string>? excludes)
    {
        if (excludes is null || excludes.Count == 0)
        {
            return "(없음)";
        }

        return string.Join("; ", excludes);
    }

    private static List<string> PruneCovered(List<string> items)
    {
        var kept = new List<string>();
        foreach (var item in items.OrderBy(v => v.Length).ThenBy(v => v, StringComparer.OrdinalIgnoreCase))
        {
            if (kept.Any(existing =>
                    string.Equals(item, existing, StringComparison.OrdinalIgnoreCase) ||
                    item.StartsWith(existing + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            kept.Add(item);
        }

        kept.Sort(StringComparer.OrdinalIgnoreCase);
        return kept;
    }
}
