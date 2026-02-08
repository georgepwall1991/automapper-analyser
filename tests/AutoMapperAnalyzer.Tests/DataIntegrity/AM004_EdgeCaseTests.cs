using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM004_EdgeCaseTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        return new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenSourceTypeIsEmpty()
    {
        // Empty source type has no properties to be unmapped, so no diagnostics expected.
        const string testCode = """
                                using AutoMapper;

                                public class Source
                                {
                                    // No properties
                                }

                                public class Destination
                                {
                                    public string Name { get; set; }
                                    public string Email { get; set; }
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenDestinationTypeIsEmpty()
    {
        // Destination is empty; source has 'Name'. Should report 'Name' is unmapped.
        const string testCode = """
                                using AutoMapper;

                                public class Source
                                {
                                    public string Name { get; set; }
                                }

                                public class Destination
                                {
                                    // No properties
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 17, 9, "Name"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenInterfaceIsSourceAndPropertyMissing()
    {
        // Interface as source: ISource has Name and Extra. Destination has Name only.
        // Should report 'Extra' is unmapped.
        const string testCode = """
                                using AutoMapper;

                                public interface ISource
                                {
                                    string Name { get; set; }
                                    string Extra { get; set; }
                                }

                                public class Destination
                                {
                                    public string Name { get; set; }
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<ISource, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 18, 9, "Extra"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenInterfaceIsDestinationAndPropertyMissing()
    {
        // Interface as destination: Source has Name and Extra. IDest has Name only.
        // Should report 'Extra' is unmapped.
        const string testCode = """
                                using AutoMapper;

                                public class Source
                                {
                                    public string Name { get; set; }
                                    public string Extra { get; set; }
                                }

                                public interface IDest
                                {
                                    string Name { get; set; }
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, IDest>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 18, 9, "Extra"));
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenDeepInheritanceChainHasUnmappedProperty()
    {
        // Deep inheritance: Derived : Middle : Base
        // Base has Id, Middle has Name, Derived has Data and Extra.
        // Destination has Id, Name, Data but not Extra.
        // Should report 'Extra' is unmapped.
        const string testCode = """
                                using AutoMapper;

                                public class Base
                                {
                                    public int Id { get; set; }
                                }

                                public class Middle : Base
                                {
                                    public string Name { get; set; }
                                }

                                public class Derived : Middle
                                {
                                    public string Data { get; set; }
                                    public string Extra { get; set; }
                                }

                                public class Destination
                                {
                                    public int Id { get; set; }
                                    public string Name { get; set; }
                                    public string Data { get; set; }
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Derived, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 30, 9, "Extra"));
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenIndexersArePresent()
    {
        // Source has Name and an indexer property. Indexers should be filtered out.
        // No diagnostics expected since Name is mapped and indexers are ignored.
        const string testCode = """
                                using AutoMapper;

                                public class Source
                                {
                                    public string Name { get; set; }

                                    public string this[int index]
                                    {
                                        get { return ""; }
                                        set { }
                                    }
                                }

                                public class Destination
                                {
                                    public string Name { get; set; }
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenUsingGenericTypeParameters()
    {
        // Generic profile with unconstrained type parameters.
        // Analyzer cannot resolve the types at compile time, so no diagnostics.
        const string testCode = """
                                using AutoMapper;

                                public class GenericProfile<TSource, TDest> : Profile
                                    where TSource : class
                                    where TDest : class
                                {
                                    public GenericProfile()
                                    {
                                        CreateMap<TSource, TDest>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_OnlyForFirstUnmappedProperty_WhenMultipleCreateMapsInProfile()
    {
        // Two independent CreateMap calls in same profile.
        // First CreateMap is valid (all properties mapped).
        // Second CreateMap has unmapped property 'Extra'.
        // Only second CreateMap should report a diagnostic.
        const string testCode = """
                                using AutoMapper;

                                public class Source1
                                {
                                    public string Name { get; set; }
                                }

                                public class Destination1
                                {
                                    public string Name { get; set; }
                                }

                                public class Source2
                                {
                                    public string Title { get; set; }
                                    public string Extra { get; set; }
                                }

                                public class Destination2
                                {
                                    public string Title { get; set; }
                                }

                                public class TestProfile : Profile
                                {
                                    public TestProfile()
                                    {
                                        CreateMap<Source1, Destination1>();
                                        CreateMap<Source2, Destination2>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 29, 9, "Extra"));
    }
}
