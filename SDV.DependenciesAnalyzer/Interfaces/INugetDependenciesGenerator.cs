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
    public string[] PackagePrefixes { get; init; }
    public PackageFilterMode Mode { get; init; }
    public bool IncludeDependentProjects { get; init; }

    public TreeGeneratorConfiguration(string slnFilePath)
    {
        SlnFilePath = slnFilePath;
        PackagePrefixes = Array.Empty<string>();
        Mode = PackageFilterMode.None;
        IncludeDependentProjects = false;
    }
}

public enum PackageFilterMode
{
    None = 0,
    Include = 1,
    Exclude = 2
}