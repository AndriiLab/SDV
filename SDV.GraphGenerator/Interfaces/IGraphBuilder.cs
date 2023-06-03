using SDV.DependenciesAnalyzer.Interfaces;

namespace SDV.GraphGenerator.Interfaces;

public interface IGraphBuilder
{
    string BuildGraphAndGetFilePath(GraphBuilderRequest request);
}

public class GraphBuilderRequest
{
    public IEnumerable<string> SlnFilePaths { get; }
    public string[] PackagePrefixes { get; init; }
    public PackageFilterMode Mode { get; init; }
    public bool IncludeDependentProjects { get; init; }
    public bool MergeProjects { get; init; }

    public GraphBuilderRequest(IEnumerable<string> slnFilePaths)
    {
        SlnFilePaths = slnFilePaths;
        PackagePrefixes = Array.Empty<string>();
    }
}
