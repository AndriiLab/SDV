namespace SDV.DependenciesAnalyzer.Common;

public class CaseInsensitiveMap<T> : Dictionary<string, T>
{
    public CaseInsensitiveMap() : base(StringComparer.OrdinalIgnoreCase)
    {
    }
}