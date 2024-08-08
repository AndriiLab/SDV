using SDV.DependenciesAnalyzer.Helpers;
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
    public string SlnFilePath { get; set; }
    public bool IncludeDependentProjects { get; init; }
    
    private readonly Func<string, bool>[] _filtersInclude;
    private readonly Func<string, bool>[] _filtersExclude;

    public TreeGeneratorConfiguration(string slnFilePath, string[] filtersInclude, string[] filtersExclude)
    {
        _filtersInclude = filtersInclude.Select(FilterHelper.BuildFilter).Where(f => f != null).ToArray()!;
        _filtersExclude = filtersExclude.Select(FilterHelper.BuildFilter).Where(f => f != null).ToArray()!;
        SlnFilePath = slnFilePath;
        IncludeDependentProjects = false;
    }

    public bool IsPackageEnabled(string packageName)
    {
        return IsIncluded(packageName) && !IsExcluded(packageName);
    }

    private bool IsIncluded(string packageName)
    {
        return _filtersInclude.Length < 1 || _filtersInclude.Any(f => f(packageName));
    }
    
    private bool IsExcluded(string packageName)
    {
        return _filtersExclude.Any(f => f(packageName));
    }
}