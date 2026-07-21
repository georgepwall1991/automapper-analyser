using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AnalyzerVerifier;

public sealed record PackageIdentity(string Id, string Version);

public sealed record CompatibilityValidation(bool IsSuccess, string Message);

public static partial class CompatibilityOutputClassifier
{
    public static CompatibilityValidation ValidateHealthy(int exitCode, string output)
    {
        if (exitCode != 0)
        {
            return new CompatibilityValidation(false, $"Healthy consumer build exited with code {exitCode}.");
        }

        string[] diagnostics = AnalyzerDiagnosticPattern().Matches(output)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (diagnostics.Length > 0)
        {
            return new CompatibilityValidation(
                false,
                $"Healthy consumer emitted analyzer diagnostic(s): {string.Join(", ", diagnostics)}.");
        }

        return ValidateAnalyzerLoad(output, "Healthy consumer built without analyzer or load diagnostics.");
    }

    public static CompatibilityValidation ValidateBroken(int exitCode, string output)
    {
        if (exitCode == 0)
        {
            return new CompatibilityValidation(false, "Broken consumer unexpectedly built successfully.");
        }

        CompatibilityValidation loadValidation = ValidateAnalyzerLoad(output, string.Empty);
        if (!loadValidation.IsSuccess)
        {
            return loadValidation;
        }

        string[] diagnostics = AnalyzerDiagnosticPattern().Matches(output)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (!diagnostics.Contains("AM001", StringComparer.Ordinal))
        {
            return new CompatibilityValidation(false, "Broken consumer did not emit AM001.");
        }

        string[] unexpected = diagnostics.Where(id => !string.Equals(id, "AM001", StringComparison.Ordinal)).ToArray();
        if (unexpected.Length > 0)
        {
            return new CompatibilityValidation(
                false,
                $"Broken consumer emitted unexpected analyzer diagnostic(s): {string.Join(", ", unexpected)}.");
        }

        return new CompatibilityValidation(true, "Broken consumer failed specifically with AM001.");
    }

    private static CompatibilityValidation ValidateAnalyzerLoad(string output, string successMessage)
    {
        if (output.Contains("AD0001", StringComparison.OrdinalIgnoreCase))
        {
            return new CompatibilityValidation(false, "Consumer emitted analyzer exception diagnostic AD0001.");
        }

        if (output.Contains("CS8032", StringComparison.OrdinalIgnoreCase))
        {
            return new CompatibilityValidation(false, "Consumer emitted analyzer load diagnostic CS8032.");
        }

        return new CompatibilityValidation(true, successMessage);
    }

    [GeneratedRegex("\\bAM[0-9]{3}\\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AnalyzerDiagnosticPattern();
}

public static class PackageCompatibilityVerifier
{
    private const string NuGetSource = "https://api.nuget.org/v3/index.json";

    public static PackageIdentity ReadPackageIdentity(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Compatibility package does not exist.", packagePath);
        }

        using ZipArchive package = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry nuspec = package.Entries.SingleOrDefault(
            entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Package does not contain a .nuspec file.");
        using Stream nuspecStream = nuspec.Open();
        XDocument document = XDocument.Load(nuspecStream, LoadOptions.None);
        XElement metadata = document.Descendants().SingleOrDefault(element => element.Name.LocalName == "metadata")
                            ?? throw new InvalidDataException("Package .nuspec does not contain metadata.");
        string? id = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == "id")?.Value;
        string? version = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == "version")?.Value;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidDataException("Package .nuspec must contain non-empty id and version metadata.");
        }

        return new PackageIdentity(id, version);
    }

    public static async Task<PackageCompatibilityResult> VerifyAsync(
        string repositoryRoot,
        string packagePath,
        CompatibilityCase compatibilityCase,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(compatibilityCase);

        string fullPackagePath = Path.GetFullPath(packagePath);
        PackageIdentity identity = ReadPackageIdentity(fullPackagePath);
        string sha256;
        await using (FileStream packageStream = File.OpenRead(fullPackagePath))
        {
            sha256 = Convert.ToHexString(
                await SHA256.HashDataAsync(packageStream, cancellationToken)).ToLowerInvariant();
        }
        string workRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "package-compatibility",
            compatibilityCase.Id,
            sha256[..16]);

        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }

        Directory.CreateDirectory(workRoot);
        string packageSource = Path.GetDirectoryName(fullPackagePath)
                               ?? throw new InvalidDataException("Package path does not have a parent directory.");

        ConsumerBuild healthy = await BuildConsumerAsync(
            Path.Combine(workRoot, "healthy"),
            packageSource,
            identity,
            compatibilityCase,
            broken: false,
            cancellationToken);
        CompatibilityValidation healthyValidation =
            CompatibilityOutputClassifier.ValidateHealthy(healthy.ExitCode, healthy.Output);

        ConsumerBuild broken = await BuildConsumerAsync(
            Path.Combine(workRoot, "broken"),
            packageSource,
            identity,
            compatibilityCase,
            broken: true,
            cancellationToken);
        CompatibilityValidation brokenValidation =
            CompatibilityOutputClassifier.ValidateBroken(broken.ExitCode, broken.Output);

        return new PackageCompatibilityResult(
            compatibilityCase,
            identity,
            sha256,
            workRoot,
            healthy,
            healthyValidation,
            broken,
            brokenValidation);
    }

    private static async Task<ConsumerBuild> BuildConsumerAsync(
        string consumerDirectory,
        string packageSource,
        PackageIdentity analyzerPackage,
        CompatibilityCase compatibilityCase,
        bool broken,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(consumerDirectory);
        string projectPath = Path.Combine(consumerDirectory, "CompatibilityConsumer.csproj");
        await File.WriteAllTextAsync(
            projectPath,
            CreateProject(compatibilityCase.TargetFramework),
            new UTF8Encoding(false),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(consumerDirectory, "Program.cs"),
            CreateProgram(broken),
            new UTF8Encoding(false),
            cancellationToken);

        await RunRequiredAsync(
            consumerDirectory,
            cancellationToken,
            "add",
            projectPath,
            "package",
            "AutoMapper",
            "--version",
            compatibilityCase.AutoMapperVersion,
            "--no-restore");
        string packagesDirectory = Path.Combine(consumerDirectory, ".packages");
        await RunRequiredAsync(
            consumerDirectory,
            cancellationToken,
            "restore",
            projectPath,
            "--packages",
            packagesDirectory,
            "--source",
            NuGetSource,
            "--no-http-cache");
        await RunRequiredAsync(
            consumerDirectory,
            cancellationToken,
            "add",
            projectPath,
            "package",
            analyzerPackage.Id,
            "--version",
            analyzerPackage.Version,
            "--source",
            packageSource,
            "--no-restore");
        await RunRequiredAsync(
            consumerDirectory,
            cancellationToken,
            "restore",
            projectPath,
            "--packages",
            packagesDirectory,
            "--source",
            packageSource,
            "--no-http-cache",
            "--ignore-failed-sources");

        ProcessResult build = await RunAsync(
            consumerDirectory,
            cancellationToken,
            "build",
            projectPath,
            "--no-restore",
            "--configuration",
            "Release",
            "-p:ContinuousIntegrationBuild=true",
            "--verbosity",
            "minimal");
        return new ConsumerBuild(build.ExitCode, build.Output);
    }

    private static string CreateProject(string targetFramework) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>{{targetFramework}}</TargetFramework>
            <LangVersion>latest</LangVersion>
          </PropertyGroup>
        </Project>
        """;

    private static string CreateProgram(bool broken)
    {
        string destinationType = broken ? "int" : "string";
        return $$"""
            using System;
            using AutoMapper;

            namespace PackageCompatibilityConsumer
            {
                internal sealed class Source
                {
                    public string Age { get; set; }
                }

                internal sealed class Destination
                {
                    public {{destinationType}} Age { get; set; }
                }

                internal sealed class MappingProfile : Profile
                {
                    public MappingProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }

                internal static class Program
                {
                    public static void Main()
                    {
                        Console.WriteLine(typeof(MappingProfile).FullName);
                    }
                }
            }
            """;
    }

    private static async Task RunRequiredAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        ProcessResult result = await RunAsync(workingDirectory, cancellationToken, arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {string.Join(" ", arguments)} failed with exit code {result.ExitCode}:{Environment.NewLine}"
                + result.Output);
        }
    }

    private static async Task<ProcessResult> RunAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var startInfo = new ProcessStartInfo(dotnet)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string output = await standardOutput + await standardError;
        return new ProcessResult(process.ExitCode, output);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}

public sealed record ConsumerBuild(int ExitCode, string Output);

public sealed record PackageCompatibilityResult(
    CompatibilityCase Case,
    PackageIdentity Package,
    string Sha256,
    string WorkRoot,
    ConsumerBuild HealthyBuild,
    CompatibilityValidation HealthyValidation,
    ConsumerBuild BrokenBuild,
    CompatibilityValidation BrokenValidation)
{
    public bool IsSuccess => HealthyValidation.IsSuccess && BrokenValidation.IsSuccess;
}
