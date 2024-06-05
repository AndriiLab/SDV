using SDV.DependenciesAnalyzer.Models;

namespace SDV.DependenciesAnalyzer.Interfaces;

public interface INugetDependenciesGenerator
{
    Tree Generate(string slnFilePath);

    /// <summary>
    /// Generates a tree based on the provided configuration.
    /// </summary>
    /// <param name="configuration">The configuration used to generate the tree.</param>
    /// <returns>The generated tree.</returns>
    Tree Generate(TreeGeneratorConfiguration configuration);
}

public class TreeGeneratorConfiguration
{
    public string SlnFilePath { get; }
    public PackageFilterMode Mode { get; init; }
    public bool IncludeDependentProjects { get; init; }
    
    private readonly List<Func<string, bool>> _filters;
    private const char Mask = '*';

    public TreeGeneratorConfiguration(string slnFilePath, string[] packageFilters)
    {
        _filters = new List<Func<string, bool>>();
        SlnFilePath = slnFilePath;
        Mode = PackageFilterMode.None;
        IncludeDependentProjects = false;
        BuildFilters(packageFilters);
    }

    public bool IsPackageEnabled(string packageName)
    {
        if (Mode == PackageFilterMode.None || _filters.Count == 0)
            return true;
        
        var isMatched = _filters.Any(f => f(packageName));
        var shouldBeProcessed = Mode == PackageFilterMode.Include ? isMatched : !isMatched;

        return shouldBeProcessed;
    }
    
    private void BuildFilters(string[] packagePrefixes)
    {
        foreach (var filter in packagePrefixes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()))
        {
            var split = filter.Split(Mask);
            if (split.Length == 1) // filter
            {
                _filters.Add(s => split[0].Equals(s));
                return;
            }

            if (split.Length == 2)
            {
                if(split.First() != string.Empty && split.Last() != string.Empty) // fil*ter
                {
                    _filters.Add(s => s.StartsWith(split.First()) && filter.EndsWith(split.Last()));

                }
                else if (split.First() == string.Empty && split.Last() == string.Empty) // *
                {
                    throw new ArgumentException($"Unknown filter specified: {filter}");
                }
                else if (split.First() == string.Empty) // *filter
                {
                    _filters.Add(s => s.EndsWith(split.Last()));
                }
                else // filter*
                {
                    _filters.Add(s => s.StartsWith(split.First()));
                }
                
                return;
            }

            if (split.Length == 3 && split.First() == string.Empty && split.Last() == string.Empty)
            {
                _filters.Add(s => s.Contains(split[1]));
                return;
            }

            throw new ArgumentException($"Unknown filter specified: {filter}");
        }
    }
}

public enum PackageFilterMode
{
    None = 0,
    Include = 1,
    Exclude = 2
}