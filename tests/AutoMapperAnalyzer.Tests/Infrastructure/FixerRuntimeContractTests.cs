using System.Collections.Immutable;
using AutoMapperAnalyzer.Analyzers.ComplexMappings;
using AutoMapperAnalyzer.Analyzers.Configuration;
using AutoMapperAnalyzer.Analyzers.DataIntegrity;
using AutoMapperAnalyzer.Analyzers.TypeSafety;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapperAnalyzer.Tests.Infrastructure;

/// <summary>
///     Runs each fixer for real and executes what it produced against AutoMapper.
///     <para>
///     Every other fixer test in this repository compares the fixed document to expected text. That
///     cannot distinguish a fix AutoMapper accepts from one that merely looks like the string the test
///     author wrote down - the 2.30.83 <c>Stack&lt;T&gt;</c> ordering defect passed exactly that check.
///     <see cref="CodeFixRuntimeVerificationTests" /> closed the gap for AM011 only; this generalises it.
///     </para>
///     <para>
///     Each scenario is minimal: its only defect is the one the rule reports. So the contract is strict
///     - <b>every</b> offered action must produce code that compiles and configures a mapper AutoMapper
///     accepts. Actions that deliberately cannot meet that (advisory scaffolds which leave work for a
///     human) are named in <see cref="FixerScenario.AdvisoryKeyFragments" /> with the reason, rather than
///     the assertion being loosened for everything.
///     </para>
/// </summary>
public class FixerRuntimeContractTests
{
    public static TheoryData<string> ScenarioNames()
    {
        var data = new TheoryData<string>();
        foreach (string name in Scenarios.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public async Task EveryOfferedFix_ShouldProduceAMapperAutoMapperAccepts(string scenarioName)
    {
        FixerScenario scenario = Scenarios[scenarioName];

        Document document = AggregateFixTestHarness.CreateDocument(
            scenario.Source,
            "FixerRuntimeContract"
                + scenarioName.Replace(" ", string.Empty).Replace("/", string.Empty)
        );

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(
            document,
            scenario.CreateAnalyzer()
        );

        Assert.True(
            diagnostics.Length > 0,
            $"{scenarioName}: the scenario reported no diagnostic, so it verifies nothing."
        );

        IReadOnlyList<CodeFixActionInspector.ActionInfo> actions =
            await CodeFixActionInspector.GetActionsAsync(
                document,
                scenario.CreateProvider(),
                diagnostics
            );

        // A leaf action is one a user can actually invoke; parents only open a sub-menu.
        List<string> applicableKeys = actions
            .Where(action => !action.HasChildren && action.EquivalenceKey != null)
            .Select(action => action.EquivalenceKey!)
            .Distinct(StringComparer.Ordinal)
            .Where(key => !scenario.IsAdvisory(key))
            .ToList();

        Assert.True(
            applicableKeys.Count > 0,
            $"{scenarioName}: no non-advisory action was offered, so nothing was executed."
        );

        int verifiedBehaviours = 0;

        foreach (string key in applicableKeys)
        {
            Document fixedDocument = await CodeFixActionInspector.ApplyActionByKeyAsync(
                document,
                scenario.CreateProvider(),
                diagnostics,
                key
            );

            string fixedText = (await fixedDocument.GetTextAsync()).ToString();
            string because = $"{scenarioName} action '{key}'";

            Action<object>? assertMapped = scenario.BehaviourFor(key);
            if (assertMapped == null)
            {
                CodeFixRuntimeVerifier.AssertConfiguresValidMapper(fixedText, because);
                continue;
            }

            // Configuration validity does not discriminate here - AutoMapper accepts the unfixed code
            // for most of these rules - so the fix is executed and its output inspected instead.
            object mapped = CodeFixRuntimeVerifier.MapThroughFixedCode(
                fixedText,
                "Source",
                "Destination",
                scenario.PopulateSource!,
                because
            );

            assertMapped(mapped);
            verifiedBehaviours++;
        }

        Assert.True(
            scenario.Behaviours.Count == 0 || verifiedBehaviours > 0,
            $"{scenarioName}: declares behavioural expectations but no offered action matched one, so "
                + "only configuration validity was checked."
        );
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        Document document,
        DiagnosticAnalyzer analyzer
    )
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create(analyzer)
        );

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private sealed record FixerScenario(
        Func<DiagnosticAnalyzer> CreateAnalyzer,
        Func<CodeFixProvider> CreateProvider,
        string Source,
        string[] AdvisoryKeyFragments
    )
    {
        /// <summary>
        ///     Builds the source instance fed through the fixed mapping. Required when
        ///     <see cref="Behaviours" /> is non-empty.
        /// </summary>
        public Func<Type, object>? PopulateSource { get; init; }

        /// <summary>
        ///     Value-level expectations keyed by equivalence-key fragment. Needed because
        ///     <c>AssertConfigurationIsValid()</c> accepts the unfixed code for most rules here, so
        ///     configuration validity alone would assert nothing about what the fixer produced. A fix
        ///     that converts, renames, or removes a mapping is only verified by running it.
        /// </summary>
        public IReadOnlyDictionary<string, Action<object>> Behaviours { get; init; } =
            new Dictionary<string, Action<object>>(StringComparer.Ordinal);

        public bool IsAdvisory(string equivalenceKey)
        {
            return AdvisoryKeyFragments.Any(fragment =>
                equivalenceKey.Contains(fragment, StringComparison.OrdinalIgnoreCase)
            );
        }

        public Action<object>? BehaviourFor(string equivalenceKey)
        {
            foreach (KeyValuePair<string, Action<object>> behaviour in Behaviours)
            {
                if (equivalenceKey.Contains(behaviour.Key, StringComparison.Ordinal))
                {
                    return behaviour.Value;
                }
            }

            return null;
        }
    }

    private static object Property(object instance, string name)
    {
        return instance.GetType().GetProperty(name)!.GetValue(instance)!;
    }

    private static object Populate(Type sourceType, params (string Name, object Value)[] values)
    {
        object instance = Activator.CreateInstance(sourceType)!;
        foreach ((string name, object value) in values)
        {
            sourceType.GetProperty(name)!.SetValue(instance, value);
        }

        return instance;
    }

    private static List<string> StringsOf(object collection)
    {
        return ((System.Collections.IEnumerable)collection)
            .Cast<object>()
            .Select(value => value.ToString()!)
            .ToList();
    }

    private static readonly Dictionary<string, FixerScenario> Scenarios = new(
        StringComparer.Ordinal
    )
    {
        ["AM001 property type mismatch"] = new(
            () => new AM001_PropertyTypeMismatchAnalyzer(),
            () => new AM001_PropertyTypeMismatchCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Amount { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public int Amount { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        )
        {
            PopulateSource = type => Populate(type, ("Amount", "42")),
            Behaviours = new Dictionary<string, Action<object>>(StringComparer.Ordinal)
            {
                // A fix that compiles and reads plausibly can still convert wrongly; only running it
                // distinguishes int.Parse from, say, a silent default.
                ["AM001_MapWithConversion"] = mapped => Assert.Equal(42, Property(mapped, "Amount")),
                ["AM001_Ignore"] = mapped => Assert.Equal(0, Property(mapped, "Amount")),
            },
        },
        ["AM002 nullable compatibility"] = new(
            () => new AM002_NullableCompatibilityAnalyzer(),
            () => new AM002_NullableCompatibilityCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string? Name { get; set; }
                }

                public class Destination
                {
                    public string Name { get; set; } = string.Empty;
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        )
        {
            PopulateSource = type => Populate(type, ("Name", null!)),
            Behaviours = new Dictionary<string, Action<object>>(StringComparer.Ordinal)
            {
                // The point of the default-value fix is that a null source never reaches a non-nullable
                // destination member. Asserting the substituted value is the only way to know it did.
                ["AM002_DefaultValue"] = mapped =>
                    Assert.Equal(string.Empty, Property(mapped, "Name")),
            },
        },
        ["AM003 collection type incompatibility"] = new(
            () => new AM003_CollectionTypeIncompatibilityAnalyzer(),
            () => new AM003_CollectionTypeIncompatibilityCodeFixProvider(),
            """
            using System.Collections.Generic;
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public List<int> Values { get; set; } = new List<int>();
                }

                public class Destination
                {
                    public HashSet<string> Values { get; set; } = new HashSet<string>();
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        )
        {
            PopulateSource = type => Populate(type, ("Values", new List<int> { 1, 2 })),
            Behaviours = new Dictionary<string, Action<object>>(StringComparer.Ordinal)
            {
                ["AM003_Constructor"] = mapped =>
                    Assert.Equal(
                        ["1", "2"],
                        StringsOf(Property(mapped, "Values")).OrderBy(value => value, StringComparer.Ordinal)
                    ),
            },
        },
        ["AM004 missing destination property"] = new(
            () => new AM004_MissingDestinationPropertyAnalyzer(),
            () => new AM004_MissingDestinationPropertyCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Name { get; set; } = string.Empty;
                    public string Dropped { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public string Name { get; set; } = string.Empty;
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        )
        {
            PopulateSource = type => Populate(type, ("Name", "kept"), ("Dropped", "gone")),
            Behaviours = new Dictionary<string, Action<object>>(StringComparer.Ordinal)
            {
                // Silencing a dropped source member must not disturb the members that do map.
                ["AM004_DoNotValidate"] = mapped => Assert.Equal("kept", Property(mapped, "Name")),
            },
        },
        ["AM005 case sensitivity mismatch"] = new(
            () => new AM005_CaseSensitivityMismatchAnalyzer(),
            () => new AM005_CaseSensitivityMismatchCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string UserName { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public string Username { get; set; } = string.Empty;
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        )
        {
            PopulateSource = type => Populate(type, ("UserName", "george")),
            Behaviours = new Dictionary<string, Action<object>>(StringComparer.Ordinal)
            {
                ["AM005_ExplicitMapping"] = mapped =>
                    Assert.Equal("george", Property(mapped, "Username")),
            },
        },
        ["AM006 unmapped destination property"] = new(
            () => new AM006_UnmappedDestinationPropertyAnalyzer(),
            () => new AM006_UnmappedDestinationPropertyCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Name { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public string Name { get; set; } = string.Empty;
                    public string Extra { get; set; } = string.Empty;
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        ),
        ["AM011 unmapped required property"] = new(
            () => new AM011_UnmappedRequiredPropertyAnalyzer(),
            () => new AM011_UnmappedRequiredPropertyCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Email { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public required string Email { get; set; }
                    public required string ContactName { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        ),
        ["AM020 nested object mapping"] = new(
            () => new AM020_NestedObjectMappingAnalyzer(),
            () => new AM020_NestedObjectMappingCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public class SourceAddress
                {
                    public string City { get; set; } = string.Empty;
                }

                public class DestinationAddress
                {
                    public string City { get; set; } = string.Empty;
                }

                public class Source
                {
                    public SourceAddress Address { get; set; } = new SourceAddress();
                }

                public class Destination
                {
                    public DestinationAddress Address { get; set; } = new DestinationAddress();
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        ),
        ["AM021 collection element mismatch"] = new(
            () => new AM021_CollectionElementMismatchAnalyzer(),
            () => new AM021_CollectionElementMismatchCodeFixProvider(),
            """
            using System.Collections.Generic;
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public List<int> Values { get; set; } = new List<int>();
                }

                public class Destination
                {
                    public List<string> Values { get; set; } = new List<string>();
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        )
        {
            PopulateSource = type => Populate(type, ("Values", new List<int> { 1, 2, 3 })),
            Behaviours = new Dictionary<string, Action<object>>(StringComparer.Ordinal)
            {
                // The .ToList() branch. Ordering is incidental here; the Stack<T> scenario below is
                // the one that guards the 2.30.83 LIFO defect.
                ["AM021_SimpleConversion"] = mapped =>
                    Assert.Equal(["1", "2", "3"], StringsOf(Property(mapped, "Values"))),
            },
        },
        ["AM021 stack element mismatch"] = new(
            () => new AM021_CollectionElementMismatchAnalyzer(),
            () => new AM021_CollectionElementMismatchCodeFixProvider(),
            """
            using System.Collections.Generic;
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public Stack<int> Values { get; set; } = new Stack<int>();
                }

                public class Destination
                {
                    public Stack<string> Values { get; set; } = new Stack<string>();
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        )
        {
            PopulateSource = type =>
            {
                var values = new Stack<int>();
                values.Push(1);
                values.Push(2);
                values.Push(3);
                return Populate(type, ("Values", values));
            },
            Behaviours = new Dictionary<string, Action<object>>(StringComparer.Ordinal)
            {
                // The 2.30.83 defect exactly. It lives in the fixer's Stack<T> branch, which appends
                // .Reverse() - a branch the List<T> scenario above never selects, so only this one
                // fails if the LIFO correction regresses. Source pops 3,2,1; so must the destination.
                ["AM021_SimpleConversion"] = mapped =>
                    Assert.Equal(["3", "2", "1"], StringsOf(Property(mapped, "Values"))),
            },
        },
        ["AM041 duplicate mapping"] = new(
            () => new AM041_DuplicateMappingAnalyzer(),
            () => new AM041_DuplicateMappingCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Name { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public string Name { get; set; } = string.Empty;
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        )
        {
            PopulateSource = type => Populate(type, ("Name", "kept")),
            Behaviours = new Dictionary<string, Action<object>>(StringComparer.Ordinal)
            {
                // Removing a duplicate registration must leave the surviving one mapping.
                ["AM041_RemoveDuplicateMapping"] = mapped =>
                    Assert.Equal("kept", Property(mapped, "Name")),
            },
        },
        ["AM050 redundant MapFrom"] = new(
            () => new AM050_RedundantMapFromAnalyzer(),
            () => new AM050_RedundantMapFromCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public class Source
                {
                    public string Name { get; set; } = string.Empty;
                }

                public class Destination
                {
                    public string Name { get; set; } = string.Empty;
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>()
                            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));
                    }
                }
            }
            """,
            []
        )
        {
            PopulateSource = type => Populate(type, ("Name", "kept")),
            Behaviours = new Dictionary<string, Action<object>>(StringComparer.Ordinal)
            {
                // Deleting a redundant ForMember is only safe if convention still maps the member.
                ["AM050_RemoveRedundantMapping"] = mapped =>
                    Assert.Equal("kept", Property(mapped, "Name")),
            },
        },
        ["AM061 enum member mismatch"] = new(
            () => new AM061_EnumMemberMismatchAnalyzer(),
            () => new AM061_EnumMemberMismatchCodeFixProvider(),
            """
            using AutoMapper;

            namespace TestNamespace
            {
                public enum SourceStatus
                {
                    Active,
                    Archived
                }

                public enum DestinationStatus
                {
                    Active
                }

                public class Source
                {
                    public SourceStatus Status { get; set; }
                }

                public class Destination
                {
                    public DestinationStatus Status { get; set; }
                }

                public class TestProfile : Profile
                {
                    public TestProfile()
                    {
                        CreateMap<Source, Destination>();
                    }
                }
            }
            """,
            []
        ),
    };
}
