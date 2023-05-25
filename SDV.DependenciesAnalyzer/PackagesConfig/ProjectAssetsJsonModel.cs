using System.Text.Json.Serialization;

namespace SDV.DependenciesAnalyzer.PackagesConfig;

public class ProjectAssetsJsonModel
{
    [JsonPropertyName("version")]
    public int Version { get; set; }
    
    [JsonPropertyName("targets")]
    public Dictionary<string, Dictionary<string, ProjectAssetsTarget>> Targets { get; set; }
    
    [JsonPropertyName("libraries")]
    public Dictionary<string, AssetsLibrary> Libraries { get; set; }
    
    [JsonPropertyName("projectFileDependencyGroups")]
    public Dictionary<string, string[]> ProjectFileDependencyGroups { get; set; }
    
    [JsonPropertyName("packageFolders")]
    public Dictionary<string, object> PackageFolders { get; set; }
    
    [JsonPropertyName("project")]
    public AssetsProject Project { get; set; }
}

public class ProjectAssetsTarget
{
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("framework")]
    public string? Framework { get; set; }
    
    [JsonPropertyName("dependencies")]
    public Dictionary<string, object>? Dependencies { get; set; }
    
    [JsonPropertyName("compile")]
    public Dictionary<string, object>? Compile { get; set; }
    
    [JsonPropertyName("runtime")]
    public Dictionary<string, object>? Runtime { get; set; }
    
    [JsonPropertyName("build")]
    public Dictionary<string, object>? Build { get; set; }
    
    [JsonPropertyName("buildMultiTargeting")]
    public Dictionary<string, object>? BuildMultiTargeting { get; set; }
}

public class AssetsLibrary
{
    [JsonPropertyName("sha512")]
    public string Sha512 { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    
    [JsonPropertyName("hasTools")]
    public bool HasTools { get; set; }
    
    [JsonPropertyName("files")]
    public string[]? Files { get; set; }
}

public class AssetsProject
{
    [JsonPropertyName("version")]
    public string Version { get; set; }
    
    [JsonPropertyName("restore")]
    public Restore? Restore { get; set; }
    
    [JsonPropertyName("frameworks")]
    public Dictionary<string, ProjectFramework>? Frameworks { get; set; }
}

public class Restore
{
    [JsonPropertyName("projectUniqueName")]
    public string ProjectUniqueName { get; set; }

    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; }

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; }

    [JsonPropertyName("packagesPath")]
    public string PackagesPath { get; set; }

    [JsonPropertyName("outputPath")]
    public string OutputPath { get; set; }

    [JsonPropertyName("projectStyle")]
    public string ProjectStyle { get; set; }

    [JsonPropertyName("crossTargeting")]
    public bool CrossTargeting { get; set; }

    [JsonPropertyName("fallbackFolders")]
    public string[] FallbackFolders { get; set; }

    [JsonPropertyName("configFilePaths")]
    public string[] ConfigFilePaths { get; set; }

    [JsonPropertyName("originalTargetFrameworks")]
    public string[] OriginalTargetFrameworks { get; set; }

    [JsonPropertyName("sources")]
    public Dictionary<string, object> Sources { get; set; }

    [JsonPropertyName("frameworks")]
    public Dictionary<string, RestoreFramework> Frameworks { get; set; }

    [JsonPropertyName("warningProperties")]
    public WarningProperties WarningProperties { get; set; }
}

public class WarningProperties
{
    [JsonPropertyName("allWarningsAsErrors")]
    public bool AllWarningsAsErrors { get; set; }

    [JsonPropertyName("noWarn")]
    public string[] NoWarn { get; set; }

    [JsonPropertyName("warnAsError")]
    public string[] WarnAsError { get; set; }
}

public class ProjectFramework
{
    [JsonPropertyName("dependencies")]
    public Dictionary<string, FrameworkDependency>? Dependencies { get; set; }
    
    [JsonPropertyName("imports")]
    public string[] Imports { get; set; }

    [JsonPropertyName("assetTargetFallback")]
    public bool AssetTargetFallback { get; set; }

    [JsonPropertyName("warn")]
    public bool Warn { get; set; }

    [JsonPropertyName("runtimeIdentifierGraphPath")]
    public string RuntimeIdentifierGraphPath { get; set; }
}

public class FrameworkDependency
{
    [JsonPropertyName("suppressParent")]
    public string SuppressParent { get; set; }

    [JsonPropertyName("target")]
    public string Target { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("autoReferenced")]
    public bool AutoReferenced { get; set; }
}

public class RestoreFramework
{
    [JsonPropertyName("projectReferences")]
    public Dictionary<string, ProjectReference> ProjectReferences { get; set; }
}

public class ProjectReference
{
    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; }
}

