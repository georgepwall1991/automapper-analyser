using AutoMapper;
using AutoMapperAnalyzer.Samples.TypeSafety;
using AutoMapperAnalyzer.Samples.MissingProperties;
using AutoMapperAnalyzer.Samples.Configuration;
using AutoMapperAnalyzer.Samples.Performance;

namespace AutoMapperAnalyzer.Samples;

/// <summary>
/// Sample application demonstrating AutoMapper scenarios that the analyzer will catch
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("🔍 AutoMapper Analyzer Sample Scenarios");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        // These scenarios will be flagged by our analyzer
        Console.WriteLine("⚠️  Type Safety Issues:");
        RunTypeSafetyExamples();

        Console.WriteLine("\n⚠️  Missing Property Issues:");
        RunMissingPropertyExamples();

        Console.WriteLine("\n⚠️  Configuration Issues:");
        RunConfigurationExamples();

        Console.WriteLine("\n⚠️  Performance Issues:");
        RunPerformanceExamples();

        Console.WriteLine("\n✅ Analysis complete. See analyzer diagnostics for issues.");
    }

    private static void RunTypeSafetyExamples()
    {
        var typeSafetyExamples = new TypeSafetyExamples();
        
        Console.WriteLine("  - Property type mismatch (string -> int)");
        typeSafetyExamples.PropertyTypeMismatchExample();
        
        Console.WriteLine("  - Nullable to non-nullable assignment");
        typeSafetyExamples.NullableToNonNullableExample();
        
        Console.WriteLine("  - Collection type incompatibility");
        typeSafetyExamples.CollectionTypeIncompatibilityExample();
    }

    private static void RunMissingPropertyExamples()
    {
        var missingPropertyExamples = new MissingPropertyExamples();
        
        Console.WriteLine("  - Missing destination property (data loss)");
        missingPropertyExamples.MissingDestinationPropertyExample();
        
        Console.WriteLine("  - Unmapped required property");
        missingPropertyExamples.UnmappedRequiredPropertyExample();
        
        Console.WriteLine("  - Case sensitivity mismatch");
        missingPropertyExamples.CaseSensitivityMismatchExample();
    }

    private static void RunConfigurationExamples()
    {
        var configExamples = new ConfigurationExamples();
        
        Console.WriteLine("  - Missing profile registration");
        configExamples.MissingProfileRegistrationExample();
        
        Console.WriteLine("  - Conflicting mapping rules");
        configExamples.ConflictingMappingRulesExample();
    }

    private static void RunPerformanceExamples()
    {
        var performanceExamples = new PerformanceExamples();
        
        Console.WriteLine("  - Static mapper usage");
        performanceExamples.StaticMapperUsageExample();
        
        Console.WriteLine("  - Missing null propagation");
        performanceExamples.MissingNullPropagationExample();
    }
}
