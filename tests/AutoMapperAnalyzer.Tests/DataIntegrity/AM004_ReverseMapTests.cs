using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.DataIntegrity;

public class AM004_ReverseMapTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        return new DiagnosticResult(descriptor).WithLocation(line, column).WithArguments(messageArgs);
    }

    [Fact]
    public async Task AM004_ShouldReportDiagnostic_WhenReverseMapMissingPropertyInSource()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                        // Missing 'Description' which is in Destination
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Description { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            // ReverseMap implies Destination -> Source
                                            // Destination has 'Description', Source does not.
                                            // So mapping Destination -> Source should fail for 'Description'
                                            CreateMap<Source, Destination>().ReverseMap();
                                        }
                                    }
                                }
                                """;

        // The diagnostic is reported on the ReverseMap invocation (or CreateMap, based on my implementation I used ReverseMap location)
        // ReverseMap is at line 24.
        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            Diagnostic(AM004_MissingDestinationPropertyAnalyzer.MissingDestinationPropertyRule, 24, 13, "Description"));
    }

    [Fact]
    public async Task AM004_ShouldNotReportDiagnostic_WhenReverseMapConfigured()
    {
        const string testCode = """
                                using AutoMapper;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string Name { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public string Name { get; set; }
                                        public string Description { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            // ReverseMap followed by ForMember to ignore Description in reverse map (Dest -> Source)
                                            // Wait, ForMember on ReverseMap configures Destination -> Source
                                            // But ForMember usually configures DESTINATION members.
                                            // In Dest -> Source, 'Source' is the destination type.
                                            // 'Source' class does NOT have 'Description'.
                                            // So we are missing a property on Source?
                                            // Wait. Destination has 'Description'. Source does NOT.
                                            // Mapping Dest -> Source.
                                            // Source is the target.
                                            // Does Source have missing property?
                                            // If Dest has 'Description', and Source doesn't.
                                            // AutoMapper maps FROM Source properties TO Destination properties.
                                            // In Dest -> Source: From Dest.Description TO Source.???
                                            // If Source doesn't have it, it's just unmapped source property (in the context of reverse map).
                                            // AM004 checks "Source property has no corresponding destination property".
                                            // In Dest -> Source: Source is Dest (provider), Destination is Source (receiver).
                                            // So "Provider property 'Description' has no corresponding Receiver property".
                                            // Yes, Dest.Description is not mapped to anything in Source.
                                            // So diagnostic should be fired.
                                            
                                            // To fix, we should Ignore it?
                                            // ForSourceMember(s => s.Description, opt => opt.DoNotValidate())
                                            // This should be applied to the ReverseMap.
                                            
                                            CreateMap<Source, Destination>()
                                                .ReverseMap()
                                                .ForSourceMember(d => d.Description, opt => opt.DoNotValidate());
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM004_MissingDestinationPropertyAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}
