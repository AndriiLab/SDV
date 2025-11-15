namespace SDV.GraphGenerator.Interfaces;

public interface IGraphBuilder
{
    string BuildGraphAndGetFilePath(GraphBuilderRequest request);
}

public class GraphBuilderRequest(IEnumerable<string> slnFilePaths)
{
    public IEnumerable<string> SlnFilePaths { get; } = slnFilePaths;
    public string[] FiltersInclude { get; init; } = [];
    public string[] FiltersExclude { get; init; } = [];
    public IReadOnlyDictionary<string, string[]> Labels { get; init; } = new Dictionary<string, string[]>();
    public bool IncludeDependentProjects { get; init; }
    public bool MergeProjects { get; init; }
}
