namespace SDV.GraphGenerator.Interfaces;

public interface IGraphBuilder
{
    string BuildGraphAndGetFilePath(GraphBuilderRequest request);
}

public class GraphBuilderRequest
{
    public IEnumerable<string> SlnFilePaths { get; }
    public string[] FiltersInclude { get; init; }
    public string[] FiltersExclude { get; init; }
    public IReadOnlyDictionary<string, string[]> Labels { get; init; }
    public bool IncludeDependentProjects { get; init; }
    public bool MergeProjects { get; init; }

    public GraphBuilderRequest(IEnumerable<string> slnFilePaths)
    {
        SlnFilePaths = slnFilePaths;
        FiltersInclude = [];
        FiltersExclude = [];
        Labels = new Dictionary<string, string[]>();
    }
}
