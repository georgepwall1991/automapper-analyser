using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AnalyzerVerifier;

namespace AutoMapperAnalyzer.Tests.Trust;

public sealed class CompatibilityContractTests
{
    [Fact]
    public void Manifest_ShouldDefineTheAdvertisedCompatibilityMatrix()
    {
        CompatibilityContract contract = CompatibilityContract.Load(GetManifestPath());

        Assert.Equal(
            [
                ("net48-am10", "windows-latest", "net48", "10.1.1"),
                ("net6-am12", "ubuntu-latest", "net6.0", "12.0.1"),
                ("net8-am14", "ubuntu-latest", "net8.0", "14.0.0"),
                ("net9-am14", "ubuntu-latest", "net9.0", "14.0.0"),
                ("net10-am14", "ubuntu-latest", "net10.0", "14.0.0")
            ],
            contract.Cases.Select(c => (c.Id, c.Runner, c.TargetFramework, c.AutoMapperVersion)));
    }

    [Fact]
    public void Manifest_ShouldRejectDuplicateCaseIds()
    {
        const string json = """
            {
              "include": [
                { "id": "net8-am14", "runner": "ubuntu-latest", "targetFramework": "net8.0", "autoMapperVersion": "14.0.0" },
                { "id": "net8-am14", "runner": "ubuntu-latest", "targetFramework": "net9.0", "autoMapperVersion": "14.0.0" }
              ]
            }
            """;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => CompatibilityContract.Parse(json));

        Assert.Contains("Duplicate compatibility case id 'net8-am14'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bad id", "ubuntu-latest", "net8.0", "14.0.0", "id")]
    [InlineData("net8-am14", "macos-latest", "net8.0", "14.0.0", "runner")]
    [InlineData("net8-am14", "ubuntu-latest", "netstandard2.0", "14.0.0", "targetFramework")]
    [InlineData("net8-am14", "ubuntu-latest", "net8.0", "latest", "autoMapperVersion")]
    public void Manifest_ShouldRejectInvalidCases(
        string id,
        string runner,
        string targetFramework,
        string autoMapperVersion,
        string expectedField)
    {
        string json = JsonSerializer.Serialize(new
        {
            include = new[] { new { id, runner, targetFramework, autoMapperVersion } }
        });

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => CompatibilityContract.Parse(json));

        Assert.Contains(expectedField, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubMatrix_ShouldRenderDirectlyFromTheManifest()
    {
        CompatibilityContract contract = CompatibilityContract.Load(GetManifestPath());

        using JsonDocument matrix = JsonDocument.Parse(contract.ToGitHubMatrixJson());
        JsonElement cases = matrix.RootElement.GetProperty("include");

        Assert.Equal(5, cases.GetArrayLength());
        Assert.Equal("net48-am10", cases[0].GetProperty("id").GetString());
        Assert.Equal("windows-latest", cases[0].GetProperty("runner").GetString());
        Assert.Equal("net10.0", cases[4].GetProperty("targetFramework").GetString());
    }

    [Fact]
    public void PackageIdentity_ShouldComeFromTheNuspecInsideThePackage()
    {
        string packagePath = Path.Combine(Path.GetTempPath(), $"compatibility-{Guid.NewGuid():N}.nupkg");
        try
        {
            using (ZipArchive package = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry nuspec = package.CreateEntry("AutoMapperAnalyzer.Analyzers.nuspec");
                using StreamWriter writer = new(nuspec.Open(), new UTF8Encoding(false));
                writer.Write("""
                    <?xml version="1.0"?>
                    <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                      <metadata>
                        <id>AutoMapperAnalyzer.Analyzers</id>
                        <version>42.7.3-smoke</version>
                      </metadata>
                    </package>
                    """);
            }

            PackageIdentity identity = PackageCompatibilityVerifier.ReadPackageIdentity(packagePath);

            Assert.Equal("AutoMapperAnalyzer.Analyzers", identity.Id);
            Assert.Equal("42.7.3-smoke", identity.Version);
        }
        finally
        {
            File.Delete(packagePath);
        }
    }

    [Theory]
    [InlineData(0, "Build succeeded.\n", true)]
    [InlineData(1, "Build FAILED.\n", false)]
    [InlineData(0, "warning AM031: analyzer diagnostic\n", false)]
    [InlineData(0, "warning AD0001: Analyzer threw an exception\n", false)]
    [InlineData(0, "warning CS8032: An instance of analyzer cannot be created\n", false)]
    [InlineData(0, "warning NETSDK1138: target framework is out of support\nwarning NU1903: advisory\n", true)]
    public void HealthyOutput_ShouldRejectBuildAndAnalyzerFailures(int exitCode, string output, bool expectedSuccess)
    {
        CompatibilityValidation validation = CompatibilityOutputClassifier.ValidateHealthy(exitCode, output);

        Assert.Equal(expectedSuccess, validation.IsSuccess);
    }

    [Theory]
    [InlineData(1, "error AM001: incompatible member mapping\n", true)]
    [InlineData(0, "warning AM001: incompatible member mapping\n", false)]
    [InlineData(1, "error CS0029: cannot convert\n", false)]
    [InlineData(1, "error AM001: incompatible member mapping\nwarning AD0001: analyzer failure\n", false)]
    [InlineData(1, "error AM001: incompatible member mapping\nwarning CS8032: analyzer load failure\n", false)]
    public void BrokenOutput_ShouldRequireAm001WithoutAnalyzerLoadFailures(
        int exitCode,
        string output,
        bool expectedSuccess)
    {
        CompatibilityValidation validation = CompatibilityOutputClassifier.ValidateBroken(exitCode, output);

        Assert.Equal(expectedSuccess, validation.IsSuccess);
    }

    [Fact]
    public void CompatibilityDocumentationTable_ShouldMatchTheManifest()
    {
        CompatibilityContract contract = CompatibilityContract.Load(GetManifestPath());
        string documentation = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "docs", "COMPATIBILITY.md"));

        string table = CompatibilityDocumentation.ExtractGeneratedTable(documentation);

        Assert.Equal(contract.ToMarkdownTable(), table);
    }

    [Fact]
    public void SnapshotBaseline_ShouldRejectAnEmptyDiagnosticSet()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => SnapshotBaseline.EnsurePopulated(0, "sample project was not restored"));

        Assert.Contains("Refusing to replace", exception.Message, StringComparison.Ordinal);
        Assert.Contains("sample project was not restored", exception.Message, StringComparison.Ordinal);
    }

    private static string GetManifestPath() =>
        Path.Combine(GetRepositoryRoot(), "tools", "package-compatibility.json");

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "automapper-analyser.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
