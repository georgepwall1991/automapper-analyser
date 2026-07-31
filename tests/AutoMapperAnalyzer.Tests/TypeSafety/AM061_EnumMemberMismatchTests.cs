using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis.Testing;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM061_EnumMemberMismatchTests
{
    [Fact]
    public async Task Should_ReportDiagnostic_When_EnumMembersAreReordered()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

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

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_PublicEnumFieldsAreReordered()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status;
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status;
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_PublicEnumFieldMapsToProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status;
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

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_ForNonMappableEnumFields()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus StaticStatus = default;
                                    public SourceStatus PrivateStatus = default;
                                    public SourceStatus ReadOnlyStatus = default;
                                    public SourceStatus ConstantStatus = default;
                                }

                                public class Destination
                                {
                                    public static DestinationStatus StaticStatus = default;
                                    private DestinationStatus PrivateStatus = default;
                                    public readonly DestinationStatus ReadOnlyStatus = default;
                                    public const DestinationStatus ConstantStatus = DestinationStatus.Active;
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ShouldMapFieldDisablesFieldMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status;
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status;
                                }

                                public static class MappingConfiguration
                                {
                                    public static MapperConfiguration Create()
                                    {
                                        return new MapperConfiguration(cfg =>
                                        {
                                            cfg.ShouldMapField = _ => false;
                                            cfg.CreateMap<Source, Destination>();
                                        });
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ShouldMapPropertyDisablesPropertyMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status { get; set; }
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status { get; set; }
                                }

                                public static class MappingConfiguration
                                {
                                    public static MapperConfiguration Create()
                                    {
                                        return new MapperConfiguration(cfg =>
                                        {
                                            cfg.ShouldMapProperty = _ => false;
                                            cfg.CreateMap<Source, Destination>();
                                        });
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportFieldPair_When_ShouldMapPropertyDisablesPropertyMapping()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status;
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status;
                                }

                                public static class MappingConfiguration
                                {
                                    public static MapperConfiguration Create()
                                    {
                                        return new MapperConfiguration(cfg =>
                                        {
                                            cfg.ShouldMapProperty = _ => false;
                                            cfg.CreateMap<Source, Destination>();
                                        });
                                    }
                                }
                                """;

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(23, 13)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(23, 13)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ProfileCustomizesShouldMapField()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status;
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status;
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        ShouldMapField = _ => false;
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ProfileCustomizesShouldMapProperty()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

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
                                        ShouldMapProperty = _ => false;
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_LookalikeShouldMapFieldIsCustomized()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status;
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status;
                                }

                                public class MappingOptions
                                {
                                    public Func<object, bool> ShouldMapField { get; set; } = _ => true;
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        var options = new MappingOptions();
                                        options.ShouldMapField = _ => false;
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(28, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(28, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_LookalikeShouldMapPropertyIsCustomized()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status { get; set; }
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status { get; set; }
                                }

                                public class MappingOptions
                                {
                                    public Func<object, bool> ShouldMapProperty { get; set; } = _ => true;
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        var options = new MappingOptions();
                                        options.ShouldMapProperty = _ => false;
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(28, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(28, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_LookalikeShouldMapFieldIsCustomizedInSeparateFile()
    {
        const string mappingCode = """
                                   using AutoMapper;

                                   public enum SourceStatus { Active = 1, Inactive = 2 }
                                   public enum DestinationStatus { Inactive = 1, Active = 2 }

                                   public class Source
                                   {
                                       public SourceStatus Status;
                                   }

                                   public class Destination
                                   {
                                       public DestinationStatus Status;
                                   }

                                   public class MyProfile : Profile
                                   {
                                       public MyProfile()
                                       {
                                           CreateMap<Source, Destination>();
                                       }
                                   }
                                   """;

        const string lookalikeCode = """
                                     using System;

                                     public class MappingOptions
                                     {
                                         public Func<object, bool> ShouldMapField { get; set; } = _ => true;
                                     }

                                     public static class ConfigureOptions
                                     {
                                         public static void Apply()
                                         {
                                             var options = new MappingOptions();
                                             options.ShouldMapField = _ => false;
                                         }
                                     }
                                     """;

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            [
                ("Mapping.cs", mappingCode),
                ("Options.cs", lookalikeCode),
            ],
            active,
            inactive);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ShouldMapFieldIsCustomizedInSeparateFile()
    {
        const string mappingCode = """
                                   using AutoMapper;

                                   public enum SourceStatus { Active = 1, Inactive = 2 }
                                   public enum DestinationStatus { Inactive = 1, Active = 2 }

                                   public class Source
                                   {
                                       public SourceStatus Status;
                                   }

                                   public class Destination
                                   {
                                       public DestinationStatus Status;
                                   }

                                   public class MyProfile : Profile
                                   {
                                       public MyProfile()
                                       {
                                           CreateMap<Source, Destination>();
                                       }
                                   }
                                   """;

        const string configurationCode = """
                                         using AutoMapper;

                                         public static class RootConfiguration
                                         {
                                             public static MapperConfiguration Create()
                                             {
                                                 return new MapperConfiguration(cfg =>
                                                 {
                                                     cfg.ShouldMapField = _ => false;
                                                     cfg.AddProfile<MyProfile>();
                                                 });
                                             }
                                         }
                                         """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
        [
            ("Mapping.cs", mappingCode),
            ("Configuration.cs", configurationCode),
        ]);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_ShouldMapPropertyIsCustomizedInSeparateFile()
    {
        const string mappingCode = """
                                   using AutoMapper;

                                   public enum SourceStatus { Active = 1, Inactive = 2 }
                                   public enum DestinationStatus { Inactive = 1, Active = 2 }

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

        const string configurationCode = """
                                         using AutoMapper;

                                         public static class RootConfiguration
                                         {
                                             public static MapperConfiguration Create()
                                             {
                                                 return new MapperConfiguration(cfg =>
                                                 {
                                                     cfg.ShouldMapProperty = _ => false;
                                                     cfg.AddProfile<MyProfile>();
                                                 });
                                             }
                                         }
                                         """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
        [
            ("Mapping.cs", mappingCode),
            ("Configuration.cs", configurationCode),
        ]);
    }

    [Fact]
    public async Task Should_ReportExplicitPair_When_ShouldMapPropertyDisablesConventionMapping()
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
                                        ShouldMapProperty = _ => false;
                                        CreateMap<Source, Destination>()
                                            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.CurrentState));
                                    }
                                }
                                """;

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(21, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(21, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_DestinationLacksSourceValue()
    {
        const string testCode = """
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

        DiagnosticResult expected = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_OnReverseMapDirection()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

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
                                        CreateMap<Source, Destination>().ReverseMap();
                                    }
                                }
                                """;

        // Forward direction reports on the CreateMap invocation; reverse direction reports on the
        // ReverseMap token.
        DiagnosticResult forwardActive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult forwardInactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        DiagnosticResult reverseInactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 42)
            .WithArguments("Status", "DestinationStatus", "SourceStatus", "Inactive", "1");

        DiagnosticResult reverseActive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 42)
            .WithArguments("Status", "DestinationStatus", "SourceStatus", "Active", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            forwardActive,
            forwardInactive,
            reverseInactive,
            reverseActive);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_ExplicitMapFromPairIsMisaligned()
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

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_EnumMembersAlign()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Active = 1, Inactive = 2 }

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

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_SameEnumTypeIsMapped()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }

                                public class Source
                                {
                                    public SourceStatus Status { get; set; }
                                }

                                public class Destination
                                {
                                    public SourceStatus Status { get; set; }
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MemberIsIgnored()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

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
                                        CreateMap<Source, Destination>()
                                            .ForMember(dest => dest.Status, opt => opt.Ignore());
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MapUsesConvertUsing()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status { get; set; }
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status { get; set; }
                                }

                                public class Converter : ITypeConverter<Source, Destination>
                                {
                                    public Destination Convert(Source source, Destination destination, ResolutionContext context)
                                        => new Destination();
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>().ConvertUsing<Converter>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MapFromIsComputed()
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
                                            .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                                                src.CurrentState == SourceStatus.Active ? DestinationStatus.Active : DestinationStatus.Inactive));
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_ForFlagsEnums()
    {
        const string testCode = """
                                using System;
                                using AutoMapper;

                                [Flags]
                                public enum SourcePermissions { None = 0, Read = 1, Write = 2 }

                                [Flags]
                                public enum DestinationPermissions { None = 0, Write = 1, Read = 2 }

                                public class Source
                                {
                                    public SourcePermissions Permissions { get; set; }
                                }

                                public class Destination
                                {
                                    public DestinationPermissions Permissions { get; set; }
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_ReverseMapInheritsForwardDirectPair()
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
                                            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.CurrentState))
                                            .ReverseMap();
                                    }
                                }
                                """;

        // Forward direction (CreateMap token): Status <- CurrentState misaligns both members.
        DiagnosticResult forwardActive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult forwardInactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        // Reverse direction (ReverseMap token): AutoMapper reverses the direct path, so
        // CurrentState <- Status inherits the same misalignment, inverted.
        DiagnosticResult reverseInactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(22, 14)
            .WithArguments("CurrentState", "DestinationStatus", "SourceStatus", "Inactive", "1");

        DiagnosticResult reverseActive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(22, 14)
            .WithArguments("CurrentState", "DestinationStatus", "SourceStatus", "Active", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            forwardActive,
            forwardInactive,
            reverseInactive,
            reverseActive);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_ForDirectForPathPair()
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
                                            .ForPath(dest => dest.Status, opt => opt.MapFrom(src => src.CurrentState));
                                    }
                                }
                                """;

        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(20, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_DeferredLocalConfigurationExists()
    {
        // var map = CreateMap<...>(); map.ForMember(...); configuration is not visible from the
        // fluent chain, so the rule fails closed instead of risking a false positive.
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

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
                                        var map = CreateMap<Source, Destination>();
                                        map.ForMember(dest => dest.Status, opt => opt.Ignore());
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_DeferredLocalReverseMapExists()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

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
                                        var map = CreateMap<Source, Destination>();
                                        map.ForMember(dest => dest.Status, opt => opt.Ignore());
                                        map.ReverseMap();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_DeferredEnumPairConverterExists()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

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
                                        var enumMap = CreateMap<SourceStatus, DestinationStatus>();
                                        enumMap.ConvertUsing(src => src == SourceStatus.Active
                                            ? DestinationStatus.Active
                                            : DestinationStatus.Inactive);

                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_DedicatedEnumPairConverterExists()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

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
                                        CreateMap<SourceStatus, DestinationStatus>()
                                            .ConvertUsing(src => src == SourceStatus.Active
                                                ? DestinationStatus.Active
                                                : DestinationStatus.Inactive);

                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotSuppressTopLevelProperty_ForNestedForPathWithSameLeafName()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status { get; set; }
                                }

                                public class Inner
                                {
                                    public int Status { get; set; }
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status { get; set; }
                                    public Inner Inner { get; set; } = new();
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>()
                                            .ForPath(dest => dest.Inner.Status, opt => opt.Ignore());
                                    }
                                }
                                """;

        // The nested path targets Inner.Status; the misaligned top-level Status still reports.
        DiagnosticResult active = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(26, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Active", "1");

        DiagnosticResult inactive = new DiagnosticResult(AM061_EnumMemberMismatchAnalyzer.EnumMemberMismatchRule)
            .WithLocation(26, 9)
            .WithArguments("Status", "SourceStatus", "DestinationStatus", "Inactive", "2");

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(
            testCode,
            active,
            inactive);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_UnderlyingTypesDifferButMembersAlign()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus : byte { Active = 1, Inactive = 2 }
                                public enum DestinationStatus : int { Active = 1, Inactive = 2 }

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

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_IdenticalEnumsHaveValueAliases()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Running = 1, Stopped = 2 }
                                public enum DestinationStatus { Active = 1, Running = 1, Stopped = 2 }

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

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_MemberIsIgnoredViaNameof()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

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
                                        CreateMap<Source, Destination>()
                                            .ForMember(nameof(Destination.Status), opt => opt.Ignore());
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_DestinationPropertyIsReadOnly()
    {
        const string testCode = """
                                using AutoMapper;

                                public enum SourceStatus { Active = 1, Inactive = 2 }
                                public enum DestinationStatus { Inactive = 1, Active = 2 }

                                public class Source
                                {
                                    public SourceStatus Status { get; set; }
                                }

                                public class Destination
                                {
                                    public DestinationStatus Status { get; }
                                }

                                public class MyProfile : Profile
                                {
                                    public MyProfile()
                                    {
                                        CreateMap<Source, Destination>();
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM061_EnumMemberMismatchAnalyzer>.VerifyAnalyzerAsync(testCode);
    }
}
