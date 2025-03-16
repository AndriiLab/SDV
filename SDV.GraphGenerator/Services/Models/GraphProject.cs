using System.Text.Json.Serialization;
using SDV.DependenciesAnalyzer.Models;

namespace SDV.GraphGenerator.Services.Models;

public class GraphProject
{
    public required GraphPackage Package { get; init; }
    public Dictionary<string, GraphEdge> Edges { get; } = new();
}

public class GraphPackage
{
    [JsonPropertyName("id")] public required string Id { get; init; }

    [JsonPropertyName("label")] public required string Label { get; set; }

    [JsonPropertyName("type")] public string Type => TypeInternal.HasFlag(DependencyType.Project) ? DependencyType.Project.ToString() : DependencyType.Package.ToString();
        
    [JsonPropertyName("multipleVersions")] public bool HasMultipleVersions { get; set; }
    
    [JsonIgnore] public DependencyType TypeInternal { get; set; }

}

public class GraphEdge
{
    [JsonPropertyName("from")] public required string From { get; init; }

    [JsonPropertyName("to")] public required string To { get; init; }

    [JsonPropertyName("label")] public required string Label { get; set; }
    
    [JsonPropertyName("multipleVersions")] public bool HasMultipleVersions { get; set; }
}