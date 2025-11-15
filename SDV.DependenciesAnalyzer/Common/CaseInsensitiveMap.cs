namespace SDV.DependenciesAnalyzer.Common;

public class CaseInsensitiveMap<T>() : Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);