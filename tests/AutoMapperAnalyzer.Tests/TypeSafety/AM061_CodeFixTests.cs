using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM061_CodeFixTests
{
    // Single misaligned member (Pending has no destination member with value 3) while every
    // source member NAME exists in the destination enum — so the Enum.Parse fix cannot throw.
    private const string SafeMapByNameTestCode = """
                                                 using AutoMapper;

                                                 public enum SourceStatus { Active = 1, Pending = 3 }
                                                 public enum DestinationStatus { Active = 1, Pending = 2, Archived = 5 }

                                                 public class Source
                                                 {
                                                     public SourceStatus Status { get; set; }
                                                 }

                                                 public class Destination
                                                 {
                                                     public DestinationStatus Status { get; set; }
                                                 }

                                                 public class MyProfile : Profile
                                                 {
                                                     public MyProfile()
                                                     {
                                                         CreateMap<Source, Destination>();
                                                     }
                                                 }
                                                 """;

    // Source member 'Inactive' (2) has no destination member at all — the Enum.Parse fix would
    // guarantee a runtime exception, so only the Ignore action may be offered here.
    private const string MissingNameTestCode = """
                                               using AutoMapper;

                                               public enum SourceStatus { Active = 1, Inactive = 2 }
                                               public enum DestinationStatus { Active = 1, Archived = 5 }

                                               public class Source
                                               {
                                                   public SourceStatus Status { get; set; }
                                               }

                                               public class Destination
                                               {
                                                   public DestinationStatus Status { get; set; }
                                               }

                                               public class MyProfile : Profile
                                               {
                                                   public MyProfile()
                                                   {
                                                       CreateMap<Source, Destination>();
                                                   }
                                               }
                                               """;

    [Fact]
    public async Task AM061_CodeFix_ShouldMapByNameViaEnumParse_When_AllSourceNamesExistInDestination()
    {
        const string fixedCode = """
                                 using AutoMapper;

                                 public enum SourceStatus { Active = 1, Pending = 3 }
                                 public enum DestinationStatus { Active = 1, Pending = 2, Archived = 5 }

                                 public class Source
                                 {
                                     public SourceStatus Status { get; set; }
                                 }

                                 public class Destination
                                 {
                                     public DestinationStatus Status { get; set; }
                                 }

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>().ForMember(dest => dest.Status, opt => opt.MapFrom(src => (DestinationStatus)global::System.Enum.Parse(typeof(DestinationStatus), src.Status.ToString())));
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Pending", "3");

        await CodeFixVerifier<AM061_EnumMemberMismatchAnalyzer, AM061_EnumMemberMismatchCodeFixProvider>
            .VerifyFixByKeyAsync(SafeMapByNameTestCode, expected, fixedCode, "AM061_MapByName_Status");
    }

    [Fact]
    public async Task AM061_CodeFix_ShouldIgnoreEnumProperty_When_DestinationLacksSourceName()
    {
        const string fixedCode = """
                                 using AutoMapper;

                                 public enum SourceStatus { Active = 1, Inactive = 2 }
                                 public enum DestinationStatus { Active = 1, Archived = 5 }

                                 public class Source
                                 {
                                     public SourceStatus Status { get; set; }
                                 }

                                 public class Destination
                                 {
                                     public DestinationStatus Status { get; set; }
                                 }

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>().ForMember(dest => dest.Status, opt => opt.Ignore());
                                     }
                                 }
                                 """;

        DiagnosticResult expected = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await CodeFixVerifier<AM061_EnumMemberMismatchAnalyzer, AM061_EnumMemberMismatchCodeFixProvider>
            .VerifyFixByKeyAsync(MissingNameTestCode, expected, fixedCode, "AM061_Ignore_Status");
    }

    [Fact]
    public async Task AM061_CodeFix_ShouldRewriteExplicitMapFromInPlace()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus CurrentState { get; set; }
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status { get; set; }
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.CurrentState));
                                    }
                                }
                                """;

        // The owning ForMember is rewritten in place; appending a second ForMember would be
        // overridden by the original MapFrom (last configuration wins).
        const string fixedCode = """
                                 using AutoMapper;

                                 public enum SourceStatus { Active = 1, Inactive = 2 }
                                 public enum DestinationStatus { Inactive = 1, Active = 2 }

                                 public class Source
                                 {
                                     public SourceStatus CurrentState { get; set; }
                                 }

                                 public class Destination
                                 {
                                     public DestinationStatus Status { get; set; }
                                 }

                                 public class MyProfile : Profile
                                 {
                                     public MyProfile()
                                     {
                                         CreateMap<Source, Destination>()
                                             .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (DestinationStatus)global::System.Enum.Parse(typeof(DestinationStatus), src.CurrentState.ToString())));
                                     }
                                 }
                                 """;

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await CodeFixVerifier<AM061_EnumMemberMismatchAnalyzer, AM061_EnumMemberMismatchCodeFixProvider>
            .VerifyFixByKeyAsync(
                testCode,
                new[] { active, inactive },
                fixedCode,
                "AM061_MapByName_Status",
                iterations: 1);
    }
}
