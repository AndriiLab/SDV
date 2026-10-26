namespace SDV.DependenciesAnalyzer.Helpers;

public static class FilterHelper
{
    private const char Mask = '*';
    // package ids are case-insensitive in NuGet
    private const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

    public static Func<string, bool>? BuildFilter(string packagePrefix)
    {
        if (string.IsNullOrWhiteSpace(packagePrefix))
            return null;

        packagePrefix = packagePrefix.Trim();
        var split = packagePrefix.Split(Mask);
        switch (split.Length)
        {
            // filter
            case 1:
                return s => split[0].Equals(s, Comparison);
            case 2:
            {
                if(split.First() != string.Empty && split.Last() != string.Empty) // fil*ter
                {
                    return s => s.StartsWith(split.First(), Comparison) && s.EndsWith(split.Last(), Comparison);

                }
                if (split.First() == string.Empty && split.Last() == string.Empty) // *
                {
                    throw new ArgumentException($"Unknown filter specified: {packagePrefix}");
                }
                if (split.First() == string.Empty) // *filter
                {
                    return s => s.EndsWith(split.Last(), Comparison);
                }
                // filter*
                return s => s.StartsWith(split.First(), Comparison);

            }
            case 3 when split.First() == string.Empty && split.Last() == string.Empty && split[1] != string.Empty: // *filter*
                return s => s.Contains(split[1], Comparison);
            default:
                throw new ArgumentException($"Unknown filter specified: {packagePrefix}");
        }
    }

}
