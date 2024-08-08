using System.Text.Json.Serialization;
using SDV.DependenciesAnalyzer.Models;

namespace SDV.GraphGenerator.Services.Models;

public class GraphProject
{
    public GraphPackage Package { get; init; }
    public Dictionary<string, GraphEdge> Edges { get; init; } = new();
}

public class GraphPackage
{
    [JsonPropertyName("id")] public string Id { get; init; }

    [JsonPropertyName("label")] public string Label { get; set; }

    [JsonPropertyName("type")] public string Type => TypeInternal.HasFlag(DependencyType.Project) ? DependencyType.Project.ToString() : DependencyType.Package.ToString();
        
    [JsonPropertyName("multipleVersions")] public bool HasMultipleVersions { get; set; }
    
    [JsonIgnore] public DependencyType TypeInternal { get; set; }

}

public class GraphEdge
{
    [JsonPropertyName("from")] public string From { get; init; }

    [JsonPropertyName("to")] public string To { get; init; }

    [JsonPropertyName("label")] public string Label { get; set; }
    
    [JsonPropertyName("multipleVersions")] public bool HasMultipleVersions { get; set; }
}