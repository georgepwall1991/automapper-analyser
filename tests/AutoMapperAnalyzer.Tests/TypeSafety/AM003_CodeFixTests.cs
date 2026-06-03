using System.Collections.Immutable;
using System.IO;
using AutoMapper;
using AutoMapperAnalyzer.Analyzers.TypeSafety;
using AutoMapperAnalyzer.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Tests.TypeSafety;

public class AM003_CodeFixTests
{
    private static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor, int line, int column,
        params object[] messageArgs)
    {
        DiagnosticResult result = new DiagnosticResult(descriptor).WithLocation(line, column);
        if (messageArgs.Length > 0)
        {
            result = result.WithArguments(messageArgs);
        }

        return result;
    }

    private static Task VerifyFixAsync(string source, DiagnosticDescriptor descriptor, int line, int column,
        string fixedCode, params object[] messageArgs)
    {
        return CodeFixVerifier<AM003_CollectionTypeIncompatibilityAnalyzer,
                AM003_CollectionTypeIncompatibilityCodeFixProvider>
            .VerifyFixAsync(source, Diagnostic(descriptor, line, column, messageArgs), fixedCode, codeActionIndex: 0);
    }

    [Fact]
    public async Task AM003_ShouldFixHashSetToListWithToList()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public HashSet<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Tags { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """

                                         using AutoMapper;
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public HashSet<string> Tags { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<string> Tags { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            21,
            13,
            expectedFixedCode,
            "Tags",
            "Source",
            "System.Collections.Generic.HashSet<string>",
            "Destination",
            "System.Collections.Generic.List<string>");
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenElementTypesAreIncompatible()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Items { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        // AM021 owns collection element mismatch diagnostics.
        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldFixStackToListWithConstructor()
    {
        const string testCode = """

                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Stack<int> Numbers { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Numbers { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """

                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Stack<int> Numbers { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<int> Numbers { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Numbers, opt => opt.MapFrom(src => new List<int>(src.Numbers)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            21,
            13,
            expectedFixedCode,
            "Numbers",
            "Source",
            "System.Collections.Generic.Stack<int>",
            "Destination",
            "System.Collections.Generic.List<int>");
    }

    [Fact]
    public async Task AM003_ShouldNotOfferFix_WhenSourceCollectionAssignableToDestinationInterface()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public string[] Tags { get; set; }
                                        public HashSet<int> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IEnumerable<string> Tags { get; set; }
                                        public IReadOnlyCollection<int> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldFixICollectionToHashSetConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public ICollection<int> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public HashSet<int> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public ICollection<int> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public HashSet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new HashSet<int>(src.Values)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Values",
            "Source",
            "System.Collections.Generic.ICollection<int>",
            "Destination",
            "System.Collections.Generic.HashSet<int>");
    }

    [Fact]
    public async Task AM003_ShouldFixQueueToListConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<string> Messages { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Messages { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Queue<string> Messages { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public List<string> Messages { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Messages, opt => opt.MapFrom(src => new List<string>(src.Messages)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Messages",
            "Source",
            "System.Collections.Generic.Queue<string>",
            "Destination",
            "System.Collections.Generic.List<string>");
    }

    [Fact]
    public async Task AM003_ShouldFixQueueToIListConversion_WithConcreteToList()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<string> Messages { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IList<string> Messages { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Queue<string> Messages { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public IList<string> Messages { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Messages, opt => opt.MapFrom(src => src.Messages.ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Messages",
            "Source",
            "System.Collections.Generic.Queue<string>",
            "Destination",
            "System.Collections.Generic.IList<string>");
    }

    [Fact]
    public async Task AM003_ShouldOfferOnlyManualIgnore_WhenElementsHaveNoKnownConversion()
    {
        // HashSet<CustomA> -> List<CustomB>: the containers differ (so AM003 fires) but there is no
        // known element conversion. The fixer must not offer a speculative (CustomB)x cast that would
        // throw InvalidCastException at runtime — only the manual-review ignore action.
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class CustomA { public int Id { get; set; } }

                                    public class CustomB { public int Id { get; set; } }

                                    public class Source
                                    {
                                        public HashSet<CustomA> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<CustomB> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("Ignore property 'Values' (manual review)", action.Title);
        Assert.Equal("AM003_Ignore_Values", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM003_ShouldOfferConversionFix_WhenElementsAreImplicitlyConvertible()
    {
        // HashSet<Derived> -> List<Base>: Derived is implicitly convertible to Base, so a
        // Select(x => (Base)x).ToList() conversion is safe and compilable and must still be offered
        // alongside manual review (it must NOT be withheld like an unrelated-type conversion).
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Base { public int Id { get; set; } }

                                    public class Derived : Base { }

                                    public class Source
                                    {
                                        public HashSet<Derived> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<Base> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Contains(actions,
            a => a.EquivalenceKey != null && a.EquivalenceKey.StartsWith("AM003_ToList", StringComparison.Ordinal));
        Assert.Contains(actions, a => a.EquivalenceKey == "AM003_Ignore_Values");
    }

    [Fact]
    public async Task AM003_ShouldOfferOnlyManualIgnoreForUnsupportedDestinationCollectionConstructor()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class CustomStringCollection : List<string>
                                    {
                                    }

                                    public class Source
                                    {
                                        public HashSet<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public CustomStringCollection Tags { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("Ignore property 'Tags' (manual review)", action.Title);
        Assert.Equal("AM003_Ignore_Tags", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM003_ShouldNotOfferConstructorForCustomDestinationNameContainingList()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class TagList : List<string>
                                    {
                                    }

                                    public class Source
                                    {
                                        public Queue<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public TagList Tags { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        CodeAction action = Assert.Single(actions);
        Assert.Equal("Ignore property 'Tags' (manual review)", action.Title);
        Assert.Equal("AM003_Ignore_Tags", action.EquivalenceKey);
    }

    [Fact]
    public async Task AM003_ShouldFixQueueToISetConversion_WithConcreteHashSet()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<int> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ISet<int> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Queue<int> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public ISet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new global::System.Collections.Generic.HashSet<int>(src.Values)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Values",
            "Source",
            "System.Collections.Generic.Queue<int>",
            "Destination",
            "System.Collections.Generic.ISet<int>");
    }

    [Fact]
    public async Task AM003_ShouldFixQueueToIListConversion_WithElementConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IList<int> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Queue<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public IList<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => src.Values.Select(x => int.Parse(x)).ToList()));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Values",
            "Source",
            "System.Collections.Generic.Queue<string>",
            "Destination",
            "System.Collections.Generic.IList<int>");
    }

    [Fact]
    public async Task AM003_ShouldFixQueueToReadOnlySetConversion_WithElementConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public IReadOnlySet<int> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Queue<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public IReadOnlySet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => new global::System.Collections.Generic.HashSet<int>(src.Values.Select(x => int.Parse(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Values",
            "Source",
            "System.Collections.Generic.Queue<string>",
            "Destination",
            "System.Collections.Generic.IReadOnlySet<int>");
    }

    [Fact]
    public async Task AM003_ShouldStillOfferHashSetConstructorForKnownBclDestinationCollection()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public ICollection<int> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public HashSet<int> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Contains(actions, action =>
            action.Title == "Convert Values using collection constructor" &&
            action.EquivalenceKey == "AM003_Constructor_Values");
        Assert.Contains(actions, action =>
            action.Title == "Ignore property 'Values' (manual review)" &&
            action.EquivalenceKey == "AM003_Ignore_Values");
    }

    [Fact]
    public async Task AM003_ShouldStillOfferConstructorForKnownBclSortedSetDestinationCollection()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public HashSet<int> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public SortedSet<int> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        Document document = CreateDocument(testCode);
        Diagnostic diagnostic = Assert.Single(await GetDiagnosticsAsync(document));

        List<CodeAction> actions = await RegisterActionsAsync(document, diagnostic);

        Assert.Contains(actions, action =>
            action.Title == "Convert Values using collection constructor" &&
            action.EquivalenceKey == "AM003_Constructor_Values");
        Assert.Contains(actions, action =>
            action.Title == "Ignore property 'Values' (manual review)" &&
            action.EquivalenceKey == "AM003_Ignore_Values");
    }

    [Fact]
    public async Task AM003_ShouldFixListToImmutableListWithCreateRange()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Collections.Immutable;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Tags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ImmutableList<string> Tags { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;
                                         using System.Collections.Immutable;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<string> Tags { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public ImmutableList<string> Tags { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Tags, opt => opt.MapFrom(src => global::System.Collections.Immutable.ImmutableList.CreateRange(src.Tags)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            21,
            13,
            expectedFixedCode,
            "Tags",
            "Source",
            "System.Collections.Generic.List<string>",
            "Destination",
            "System.Collections.Immutable.ImmutableList<string>");
    }

    [Fact]
    public async Task AM003_ShouldFixListToImmutableHashSetWithCreateRange()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;
                                using System.Collections.Immutable;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<int> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public ImmutableHashSet<int> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;
                                         using System.Collections.Immutable;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<int> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public ImmutableHashSet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => global::System.Collections.Immutable.ImmutableHashSet.CreateRange(src.Values)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            21,
            13,
            expectedFixedCode,
            "Values",
            "Source",
            "System.Collections.Generic.List<int>",
            "Destination",
            "System.Collections.Immutable.ImmutableHashSet<int>");
    }

    [Fact]
    public async Task AM003_ShouldFixQueueToFrozenSetWithElementConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Frozen;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public Queue<string> Values { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public FrozenSet<int> Values { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Frozen;
                                         using System.Collections.Generic;
                                         using System.Linq;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public Queue<string> Values { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public FrozenSet<int> Values { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Values, opt => opt.MapFrom(src => global::System.Collections.Frozen.FrozenSet.ToFrozenSet(src.Values.Select(x => int.Parse(x)))));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            21,
            13,
            expectedFixedCode,
            "Values",
            "Source",
            "System.Collections.Generic.Queue<string>",
            "Destination",
            "System.Collections.Frozen.FrozenSet<int>");
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenComplexElementTypesDiffer()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<object> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<string> Data { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        // AM021 owns collection element mismatch diagnostics.
        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenPrimitiveElementTypesDiffer()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<string> Flags { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<bool> Flags { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        // AM021 owns collection element mismatch diagnostics.
        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldHandleListToQueueConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<int> Items { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Queue<int> Items { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public List<int> Items { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Queue<int> Items { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Items, opt => opt.MapFrom(src => new Queue<int>(src.Items)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Items",
            "Source",
            "System.Collections.Generic.List<int>",
            "Destination",
            "System.Collections.Generic.Queue<int>");
    }

    [Fact]
    public async Task AM003_ShouldHandleIEnumerableToStackConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public IEnumerable<string> Data { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public Stack<string> Data { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public IEnumerable<string> Data { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public Stack<string> Data { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.Data, opt => opt.MapFrom(src => new Stack<string>(src.Data)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "Data",
            "Source",
            "System.Collections.Generic.IEnumerable<string>",
            "Destination",
            "System.Collections.Generic.Stack<string>");
    }

    [Fact]
    public async Task AM003_ShouldNotReportDiagnostic_WhenNumericElementTypesDiffer()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public List<double> Measurements { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public List<int> Measurements { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        // AM021 owns collection element mismatch diagnostics.
        await AnalyzerVerifier<AM003_CollectionTypeIncompatibilityAnalyzer>.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AM003_ShouldHandleEnumerableToHashSetWithTypeConversion()
    {
        const string testCode = """
                                using AutoMapper;
                                using System.Collections.Generic;

                                namespace TestNamespace
                                {
                                    public class Source
                                    {
                                        public IEnumerable<int> UniqueIds { get; set; }
                                    }

                                    public class Destination
                                    {
                                        public HashSet<int> UniqueIds { get; set; }
                                    }

                                    public class TestProfile : Profile
                                    {
                                        public TestProfile()
                                        {
                                            CreateMap<Source, Destination>();
                                        }
                                    }
                                }
                                """;

        const string expectedFixedCode = """
                                         using AutoMapper;
                                         using System.Collections.Generic;

                                         namespace TestNamespace
                                         {
                                             public class Source
                                             {
                                                 public IEnumerable<int> UniqueIds { get; set; }
                                             }

                                             public class Destination
                                             {
                                                 public HashSet<int> UniqueIds { get; set; }
                                             }

                                             public class TestProfile : Profile
                                             {
                                                 public TestProfile()
                                                 {
                                                     CreateMap<Source, Destination>().ForMember(dest => dest.UniqueIds, opt => opt.MapFrom(src => new HashSet<int>(src.UniqueIds)));
                                                 }
                                             }
                                         }
                                         """;

        await VerifyFixAsync(
            testCode,
            AM003_CollectionTypeIncompatibilityAnalyzer.CollectionTypeIncompatibilityRule,
            20,
            13,
            expectedFixedCode,
            "UniqueIds",
            "Source",
            "System.Collections.Generic.IEnumerable<int>",
            "Destination",
            "System.Collections.Generic.HashSet<int>");
    }

    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "AM003Tests", "AM003Tests", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectParseOptions(projectId, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));

        string trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (string assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                solution = solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        solution = solution
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Profile).Assembly.Location))
            .AddDocument(documentId, "Test0.cs", SourceText.From(source));

        return solution.GetDocument(documentId)!;
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Document document)
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        return (await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new AM003_CollectionTypeIncompatibilityAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(diagnostic => diagnostic.GetMessage(), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static async Task<List<CodeAction>> RegisterActionsAsync(Document document, params Diagnostic[] diagnostics)
    {
        var actions = new List<CodeAction>();
        var provider = new AM003_CollectionTypeIncompatibilityCodeFixProvider();

        var context = new CodeFixContext(
            document,
            diagnostics[0].Location.SourceSpan,
            diagnostics.ToImmutableArray(),
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }
}
