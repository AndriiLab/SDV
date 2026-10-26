using SDV.DependenciesAnalyzer.Common;
using SDV.DependenciesAnalyzer.Exceptions;
using SDV.DependenciesAnalyzer.PackagesConfig;
using SDV.DependenciesAnalyzer.Tests.TestUtils;

namespace SDV.DependenciesAnalyzer.Tests.Common;

public sealed class CommonUtilsTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void FixSeparatorsToMatchOs_MixedSeparators_UsesPlatformSeparator()
    {
        var sep = Path.DirectorySeparatorChar;

        var result = CommonUtils.FixSeparatorsToMatchOs(@"src/App\App.csproj");

        Assert.Equal($"src{sep}App{sep}App.csproj", result);
    }

    [Fact]
    public void GetProjectDirectoryName_FilePath_ReturnsDirectory()
    {
        var path = Path.Combine(_dir.Path, "App", "App.csproj");

        var result = CommonUtils.GetProjectDirectoryName(path);

        Assert.Equal(Path.Combine(_dir.Path, "App"), result);
    }

    [Fact]
    public void GetProjectDirectoryName_RootPath_Throws()
    {
        var root = Path.GetPathRoot(_dir.Path)!;

        Assert.Throws<NugetDepsTreeException>(() => CommonUtils.GetProjectDirectoryName(root));
    }

    [Fact]
    public void ReadFileIfExists_ExistingFile_ReturnsContent()
    {
        var path = _dir.WriteFile("file.txt", "content");

        Assert.Equal("content", CommonUtils.ReadFileIfExists(path));
    }

    [Fact]
    public void ReadFileIfExists_MissingFile_ReturnsNull()
    {
        Assert.Null(CommonUtils.ReadFileIfExists(_dir.Combine("missing.txt")));
    }

    [Fact]
    public void ListFilesWithExtension_MixedFiles_ReturnsOnlyMatchingTopLevelFiles()
    {
        var csproj = _dir.WriteFile("App.csproj", "");
        _dir.WriteFile("App.cs", "");
        _dir.WriteFile("App.csproj.user", "");
        _dir.WriteFile(Path.Combine("nested", "Nested.csproj"), "");

        var result = CommonUtils.ListFilesWithExtension(_dir.Path, ".csproj");

        Assert.Equal([csproj], result);
    }

    [Fact]
    public void ParseXmlToObject_PackagesConfig_ParsesPackages()
    {
        var xml = TestFiles.PackagesConfig(("Newtonsoft.Json", "13.0.1"), ("Serilog", "2.0.0"));

        var model = CommonUtils.ParseXmlToObject<PackagesConfigModel>(xml);

        Assert.NotNull(model.Package);
        Assert.Collection(model.Package,
            p => Assert.Equal(("Newtonsoft.Json", "13.0.1"), (p.Id, p.Version)),
            p => Assert.Equal(("Serilog", "2.0.0"), (p.Id, p.Version)));
    }

    [Fact]
    public void ParseXmlToObject_LegacyCsproj_ParsesProjectReferences()
    {
        var xml = TestFiles.LegacyCsproj("Core", "Data");

        var model = CommonUtils.ParseXmlToObject<CsprojProjectModel>(xml);

        var names = model.ItemGroup
            .SelectMany(i => i.ProjectReference ?? [])
            .Select(r => r.Name);
        Assert.Equal(["Core", "Data"], names);
    }

    [Fact]
    public void ParseXmlToObject_MalformedXml_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CommonUtils.ParseXmlToObject<PackagesConfigModel>("<packages>"));
    }

    [Fact]
    public void FindNupkgFilePath_PackageInLocalPackagesFolder_ReturnsPath()
    {
        var expected = _dir.WriteFile(Path.Combine("packages", "foo.1.0.0", "foo.1.0.0.nupkg"), "");

        var result = CommonUtils.FindNupkgFilePath(_dir.Path, "foo/1.0.0", "foo.1.0.0.nupkg");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FindNupkgFilePath_PackageNowhere_ReturnsNull()
    {
        var id = $"sdv-missing-{Guid.NewGuid():N}";

        var result = CommonUtils.FindNupkgFilePath(_dir.Path, $"{id}/1.0.0", $"{id}.1.0.0.nupkg");

        Assert.Null(result);
    }
}
