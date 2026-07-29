using System.Collections.Immutable;
using System.Text;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit.Abstractions;

namespace AutoMapperAnalyzer.Tests.Robustness;

/// <summary>
///     Enumerates the type-shape space the analyzers reason about and asserts invariants over it.
///     <para>
///     These rules reason about nullability × collection kind × declaration form × inheritance depth.
///     Hand-written cases sample that space sparsely and, unavoidably, only at the points someone
///     already thought of. This walks it systematically instead.
///     </para>
///     <para>
///     It asserts <b>invariants</b>, never expected diagnostics. Predicting the exact diagnostic for
///     every generated combination would mean reimplementing the analyzers in the test, and the
///     reimplementation would be wrong in the same places. Invariants — no crash, no contradictory
///     claims about one member, no diagnostic on a shape that is identical on both sides — hold
///     regardless of which rule fires, and a violation is a real defect rather than a stale expectation.
///     </para>
/// </summary>
public class GeneratedTypeShapeTests(ITestOutputHelper output)
{
    private static readonly string[] ElementTypes = ["string", "int", "System.DateTime"];

    private static readonly (string Name, string Format)[] Containers =
    [
        ("scalar", "{0}"),
        ("list", "System.Collections.Generic.List<{0}>"),
        ("array", "{0}[]"),
        ("readonlyList", "System.Collections.Generic.IReadOnlyList<{0}>"),
        ("hashSet", "System.Collections.Generic.HashSet<{0}>"),
    ];

    private static readonly string[] DeclarationForms = ["class", "record", "record struct"];

    /// <summary>
    ///     A mapping whose source and destination are structurally identical must never be reported as
    ///     a type or nullability problem. Whatever a rule believes about the shape, it believes the same
    ///     thing on both sides, so a mismatch diagnostic is self-contradictory.
    /// </summary>
    [Theory]
    [MemberData(nameof(IdenticalShapeCases))]
    public async Task IdenticalShapes_ShouldNotReportMismatchDiagnostics(
        string containerName,
        string elementType,
        string declarationForm,
        bool nullable
    )
    {
        string container = Containers.First(c => c.Name == containerName).Format;
        string memberType = string.Format(container, elementType) + (nullable ? "?" : string.Empty);

        string source = BuildMapping(declarationForm, memberType, memberType);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

        string[] mismatchRules = ["AM001", "AM002", "AM003", "AM021"];
        Diagnostic[] offending = diagnostics
            .Where(d => mismatchRules.Contains(d.Id, StringComparer.Ordinal))
            .ToArray();

        Assert.True(
            offending.Length == 0,
            $"Identical shapes ({declarationForm}, {memberType}) reported a mismatch: "
                + string.Join("; ", offending.Select(d => $"{d.Id} {d.GetMessage()}"))
        );
    }

    /// <summary>
    ///     No analyzer may crash on any generated shape. This is the same guarantee as the crash-safety
    ///     suite, applied to well-formed but combinatorially varied input rather than malformed input.
    /// </summary>
    [Theory]
    [MemberData(nameof(CrossShapeCases))]
    public async Task CrossShapeMappings_ShouldNeverCrashAnAnalyzer(
        string sourceType,
        string destinationType,
        string declarationForm
    )
    {
        string source = BuildMapping(declarationForm, sourceType, destinationType);
        (ImmutableArray<Diagnostic> _, IReadOnlyList<string> crashes) =
            await AnalyzeCapturingCrashesAsync(source);

        Assert.True(
            crashes.Count == 0,
            $"Analyzers threw mapping {sourceType} -> {destinationType} ({declarationForm}):"
                + Environment.NewLine
                + string.Join(Environment.NewLine, crashes)
        );
    }

    /// <summary>
    ///     One destination member must not be simultaneously reported as unmapped (AM006/AM011) and as
    ///     mapped-but-incompatible (AM001/AM003/AM021). Those claims contradict each other, and a user
    ///     shown both has no coherent action to take.
    /// </summary>
    [Theory]
    [MemberData(nameof(CrossShapeCases))]
    public async Task NoMemberIsBothUnmappedAndIncompatible(
        string sourceType,
        string destinationType,
        string declarationForm
    )
    {
        string source = BuildMapping(declarationForm, sourceType, destinationType);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source);

        string[] unmapped = ["AM006", "AM011"];
        string[] incompatible = ["AM001", "AM003", "AM021"];

        bool claimsUnmapped = diagnostics.Any(d =>
            unmapped.Contains(d.Id, StringComparer.Ordinal)
            && d.GetMessage().Contains("Value", StringComparison.Ordinal)
        );
        bool claimsIncompatible = diagnostics.Any(d =>
            incompatible.Contains(d.Id, StringComparer.Ordinal)
            && d.GetMessage().Contains("Value", StringComparison.Ordinal)
        );

        Assert.False(
            claimsUnmapped && claimsIncompatible,
            $"Member 'Value' reported as both unmapped and incompatible mapping {sourceType} -> "
                + $"{destinationType} ({declarationForm}): "
                + string.Join("; ", diagnostics.Select(d => $"{d.Id} {d.GetMessage()}"))
        );
    }

    public static TheoryData<string, string, string, bool> IdenticalShapeCases()
    {
        var data = new TheoryData<string, string, string, bool>();
        foreach ((string name, string _) in Containers)
        {
            foreach (string element in ElementTypes)
            {
                foreach (string form in DeclarationForms)
                {
                    // A nullable annotation on a value-type container is a different type, not the same
                    // shape, so only reference-shaped members carry the nullable variant here.
                    data.Add(name, element, form, false);
                    if (name != "scalar" || element == "string")
                    {
                        data.Add(name, element, form, true);
                    }
                }
            }
        }

        return data;
    }

    public static TheoryData<string, string, string> CrossShapeCases()
    {
        var data = new TheoryData<string, string, string>();
        string[] shapes = Containers
            .SelectMany(container =>
                ElementTypes.Select(element => string.Format(container.Format, element))
            )
            .ToArray();

        // Every container/element shape against every other, on one declaration form, plus the full
        // shape set against itself on the remaining forms. Full cross-product across all three forms
        // would triple the runtime without exercising materially different analyzer paths.
        foreach (string sourceShape in shapes)
        {
            foreach (string destinationShape in shapes)
            {
                data.Add(sourceShape, destinationShape, "class");
            }
        }

        foreach (string shape in shapes)
        {
            data.Add(shape, shape + "?", "record");
            data.Add(shape, shape, "record struct");
        }

        return data;
    }

    private static string BuildMapping(
        string declarationForm,
        string sourceMemberType,
        string destinationMemberType
    )
    {
        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using AutoMapper;");
        builder.AppendLine();
        builder.AppendLine("namespace GeneratedFixture");
        builder.AppendLine("{");
        builder.AppendLine(
            $"    public {declarationForm} Source {{ public {sourceMemberType} Value {{ get; set; }} = default!; }}"
        );
        builder.AppendLine(
            $"    public {declarationForm} Destination {{ public {destinationMemberType} Value {{ get; set; }} = default!; }}"
        );
        builder.AppendLine();
        builder.AppendLine("    public class GeneratedProfile : Profile");
        builder.AppendLine("    {");
        builder.AppendLine("        public GeneratedProfile()");
        builder.AppendLine("        {");
        builder.AppendLine("            CreateMap<Source, Destination>();");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        (ImmutableArray<Diagnostic> diagnostics, IReadOnlyList<string> crashes) =
            await AnalyzeCapturingCrashesAsync(source);

        Assert.True(crashes.Count == 0, "Analyzer crashed: " + string.Join("; ", crashes));
        return diagnostics;
    }

    private static async Task<(
        ImmutableArray<Diagnostic>,
        IReadOnlyList<string>
    )> AnalyzeCapturingCrashesAsync(string source)
    {
        var references = new List<MetadataReference>();
        string trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;

        foreach (string assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                references.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        references.Add(MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "AutoMapperAnalyzer.Generated",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        ImmutableArray<DiagnosticAnalyzer> analyzers = RuleCatalog
            .Rules.Select(rule => rule.AnalyzerType)
            .Distinct()
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .ToImmutableArray();

        var crashes = new List<string>();
        var crashLock = new object();

        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: (exception, analyzer, _) =>
            {
                lock (crashLock)
                {
                    crashes.Add(
                        $"{analyzer.GetType().Name}: {exception.GetType().Name}: {exception.Message}"
                    );
                }
            },
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false
        );

        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(analyzers, options)
            .GetAnalyzerDiagnosticsAsync();

        return (
            diagnostics
                .Where(d => d.Id.StartsWith("AM", StringComparison.Ordinal))
                .ToImmutableArray(),
            crashes
        );
    }
}
