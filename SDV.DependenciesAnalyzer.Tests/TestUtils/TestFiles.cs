using System.IO.Compression;
using System.Security;
using System.Text;

namespace SDV.DependenciesAnalyzer.Tests.TestUtils;

/// <summary>
/// Builders for the on-disk artifacts SDV reads: solution files, csproj files, packages.config and nupkg archives.
/// </summary>
public static class TestFiles
{
    private const string CsharpProjectType = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
    private const string SolutionFolderType = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

    public static string Solution(params string[] projectNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        foreach (var name in projectNames)
        {
            sb.AppendLine(ProjectLine(name, $"{name}\\{name}.csproj"));
            sb.AppendLine("EndProject");
        }

        sb.AppendLine("Global");
        sb.AppendLine("EndGlobal");
        return sb.ToString();
    }

    public static string ProjectLine(string name, string relativePath, string projectType = CsharpProjectType) =>
        $"Project(\"{projectType}\") = \"{name}\", \"{relativePath}\", \"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}\"";

    public static string SolutionFolderLine(string name) => ProjectLine(name, name, SolutionFolderType);

    public static string SdkCsproj() =>
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    /// <summary>
    /// Legacy (non-SDK) csproj, the format that accompanies packages.config.
    /// </summary>
    public static string LegacyCsproj(params string[] projectReferences)
    {
        var references = string.Join(Environment.NewLine, projectReferences.Select(r =>
            $"""    <ProjectReference Include="..\{r}\{r}.csproj"><Project>{Guid.NewGuid():B}</Project><Name>{r}</Name></ProjectReference>"""));

        return $"""
                <?xml version="1.0" encoding="utf-8"?>
                <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <ItemGroup>
                    <Reference Include="System" />
                  </ItemGroup>
                  <ItemGroup>
                {references}
                  </ItemGroup>
                </Project>
                """;
    }

    public static string PackagesConfig(params (string Id, string Version)[] packages)
    {
        var lines = packages.Select(p =>
            $"""  <package id="{p.Id}" version="{p.Version}" targetFramework="net48" />""");
        return $"""
                <?xml version="1.0" encoding="utf-8"?>
                <packages>
                {string.Join(Environment.NewLine, lines)}
                </packages>
                """;
    }

    /// <summary>
    /// Writes a minimal but valid nupkg (a zip holding only the nuspec) to <paramref name="path"/>.
    /// </summary>
    public static void WriteNupkg(string path, string id, string version, params string[] dependencies)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var dependencyElements = string.Join("", dependencies.Select(d =>
            $"""<dependency id="{SecurityElement.Escape(d)}" version="1.0.0" />"""));
        var nuspec = $"""
                      <?xml version="1.0" encoding="utf-8"?>
                      <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                        <metadata>
                          <id>{id}</id>
                          <version>{version}</version>
                          <authors>tests</authors>
                          <description>test package</description>
                          <dependencies>
                            <group targetFramework=".NETFramework4.8">{dependencyElements}</group>
                          </dependencies>
                        </metadata>
                      </package>
                      """;

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry($"{id}.nuspec");
        using var writer = new StreamWriter(entry.Open());
        writer.Write(nuspec);
    }

    /// <summary>
    /// Path where <c>CommonUtils.FindNupkgFilePath</c> looks first for a package referenced from packages.config.
    /// </summary>
    public static string PackagesConfigNupkgPath(string solutionDir, string id, string version) =>
        Path.Combine(solutionDir, "packages", $"{id}.{version}", $"{id}.{version}.nupkg");
}
