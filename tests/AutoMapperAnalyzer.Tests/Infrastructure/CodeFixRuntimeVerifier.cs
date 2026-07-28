using System.Collections.Immutable;
using System.Reflection;
using AutoMapper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

/// <summary>
///     Executes code-fix output instead of comparing it to expected text.
///     <para>
///     Every fixer in this repository is verified by string equality against a hand-written expected
///     document. That proves the fix matches what the test author wrote down; it does not prove the
///     result is a mapping AutoMapper accepts. The 2.30.83 <c>Stack&lt;T&gt;</c> ordering defect is the
///     failure class this closes: syntactically valid, compile-clean, and wrong at runtime.
///     </para>
///     <para>
///     Source is compiled against the real AutoMapper assembly, loaded, and every discovered
///     <see cref="Profile" /> is registered and validated with <c>AssertConfigurationIsValid()</c>.
///     </para>
/// </summary>
internal static class CodeFixRuntimeVerifier
{
    /// <summary>
    ///     Compiles the source, registers every Profile it declares, and asserts AutoMapper accepts the
    ///     resulting configuration. Fails with the compiler or AutoMapper diagnostics when it does not.
    /// </summary>
    public static void AssertConfiguresValidMapper(string source, string? because = null)
    {
        Assembly assembly = CompileAndLoad(source, because);
        MapperConfiguration configuration = BuildConfiguration(assembly);

        try
        {
            configuration.AssertConfigurationIsValid();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                Describe("AutoMapper rejected the configuration produced by the code fix", because)
                    + Environment.NewLine
                    + exception.Message,
                exception
            );
        }
    }

    /// <summary>
    ///     Compiles and validates the source, then maps <paramref name="sourceValue" /> to
    ///     <typeparamref name="TResult" /> so callers can assert on the mapped values themselves.
    ///     Ordering and conversion defects only surface when the mapping actually runs.
    /// </summary>
    public static object MapThroughFixedCode(
        string source,
        string sourceTypeName,
        string destinationTypeName,
        Func<Type, object> sourceValue,
        string? because = null
    )
    {
        Assembly assembly = CompileAndLoad(source, because);
        MapperConfiguration configuration = BuildConfiguration(assembly);
        configuration.AssertConfigurationIsValid();

        Type sourceType = ResolveType(assembly, sourceTypeName);
        Type destinationType = ResolveType(assembly, destinationTypeName);

        return configuration
            .CreateMapper()
            .Map(sourceValue(sourceType), sourceType, destinationType);
    }

    private static MapperConfiguration BuildConfiguration(Assembly assembly)
    {
        List<Profile> profiles = assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(Profile).IsAssignableFrom(type))
            .Select(type => (Profile)Activator.CreateInstance(type)!)
            .ToList();

        Assert.True(profiles.Count > 0, "Fixed code declared no AutoMapper Profile to validate.");

        return new MapperConfiguration(configuration =>
        {
            foreach (Profile profile in profiles)
            {
                configuration.AddProfile(profile);
            }
        });
    }

    private static Type ResolveType(Assembly assembly, string typeName)
    {
        Type? type = assembly
            .GetTypes()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, typeName, StringComparison.Ordinal)
                || string.Equals(candidate.FullName, typeName, StringComparison.Ordinal)
            );

        Assert.True(type != null, $"Fixed code does not declare a type named '{typeName}'.");
        return type!;
    }

    private static Assembly CompileAndLoad(string source, string? because)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "AutoMapperAnalyzer.RuntimeFixVerification." + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            BuildReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        using var peStream = new MemoryStream();
        EmitResult emitResult = compilation.Emit(peStream);

        if (!emitResult.Success)
        {
            ImmutableArray<Diagnostic> errors = emitResult
                .Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();

            throw new InvalidOperationException(
                Describe("Code-fix output did not compile", because)
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        errors.Select(diagnostic => diagnostic.ToString())
                    )
            );
        }

        peStream.Position = 0;
        return Assembly.Load(peStream.ToArray());
    }

    private static List<MetadataReference> BuildReferences()
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
        return references;
    }

    private static string Describe(string headline, string? because)
    {
        return because == null ? headline + "." : headline + " (" + because + ").";
    }
}
