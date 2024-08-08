namespace SDV.DependenciesAnalyzer.Helpers;

public static class FilterHelper
{
    private const char Mask = '*';

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
                return s => split[0].Equals(s);
            case 2:
            {
                if(split.First() != string.Empty && split.Last() != string.Empty) // fil*ter
                {
                    return s => s.StartsWith(split.First()) && packagePrefix.EndsWith(split.Last());

                }
                if (split.First() == string.Empty && split.Last() == string.Empty) // *
                {
                    throw new ArgumentException($"Unknown filter specified: {packagePrefix}");
                }
                if (split.First() == string.Empty) // *filter
                {
                    return s => s.EndsWith(split.Last());
                }
                // filter*
                return s => s.StartsWith(split.First());
                
            }
            case 3 when split.First() == string.Empty && split.Last() == string.Empty:
                return s => s.Contains(split[1]);
            default:
                throw new ArgumentException($"Unknown filter specified: {packagePrefix}");
        }
    }

}