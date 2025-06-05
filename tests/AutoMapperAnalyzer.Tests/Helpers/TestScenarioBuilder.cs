using System.Text;

namespace AutoMapperAnalyzer.Tests.Helpers;

/// <summary>
///     Fluent builder for creating AutoMapper test scenarios
/// </summary>
public class TestScenarioBuilder
{
    private readonly List<string> _classes = new();
    private readonly List<string> _mappingCalls = new();
    private readonly List<string> _profiles = new();
    private readonly StringBuilder _sourceBuilder = new();
    private readonly List<string> _usings = new();

    public TestScenarioBuilder()
    {
        // Add default usings
        AddUsing("System");
        AddUsing("System.Collections.Generic");
        AddUsing("System.Linq");
        AddUsing("AutoMapper");
    }

    /// <summary>
    ///     Add a using statement
    /// </summary>
    public TestScenarioBuilder AddUsing(string usingStatement)
    {
        if (!_usings.Contains(usingStatement))
        {
            _usings.Add(usingStatement);
        }

        return this;
    }

    /// <summary>
    ///     Add a simple class with properties
    /// </summary>
    public TestScenarioBuilder AddClass(string className, params (string Type, string Name)[] properties)
    {
        var classBuilder = new StringBuilder();
        classBuilder.AppendLine($"public class {className}");
        classBuilder.AppendLine("{");

        foreach ((string type, string name) in properties)
        {
            classBuilder.AppendLine($"    public {type} {name} {{ get; set; }}");
        }

        classBuilder.AppendLine("}");

        _classes.Add(classBuilder.ToString());
        return this;
    }

    /// <summary>
    ///     Add a class with required properties
    /// </summary>
    public TestScenarioBuilder AddClassWithRequired(string className,
        params (string Type, string Name, bool Required)[] properties)
    {
        var classBuilder = new StringBuilder();
        classBuilder.AppendLine($"public class {className}");
        classBuilder.AppendLine("{");

        foreach ((string type, string name, bool required) in properties)
        {
            if (required)
            {
                classBuilder.AppendLine($"    public required {type} {name} {{ get; set; }}");
            }
            else
            {
                classBuilder.AppendLine($"    public {type} {name} {{ get; set; }}");
            }
        }

        classBuilder.AppendLine("}");

        _classes.Add(classBuilder.ToString());
        return this;
    }

    /// <summary>
    ///     Add a custom AutoMapper profile
    /// </summary>
    public TestScenarioBuilder AddProfile(string profileName, params string[] mappingConfigurations)
    {
        var profileBuilder = new StringBuilder();
        profileBuilder.AppendLine($"public class {profileName} : Profile");
        profileBuilder.AppendLine("{");
        profileBuilder.AppendLine($"    public {profileName}()");
        profileBuilder.AppendLine("    {");

        foreach (string config in mappingConfigurations)
        {
            profileBuilder.AppendLine($"        {config}");
        }

        profileBuilder.AppendLine("    }");
        profileBuilder.AppendLine("}");

        _profiles.Add(profileBuilder.ToString());
        return this;
    }

    /// <summary>
    ///     Add a simple mapping configuration
    /// </summary>
    public TestScenarioBuilder AddMapping(string sourceType, string destType, params string[] configurations)
    {
        var mappingBuilder = new StringBuilder();
        mappingBuilder.Append($"CreateMap<{sourceType}, {destType}>()");

        foreach (string config in configurations)
        {
            mappingBuilder.Append($".{config}");
        }

        mappingBuilder.Append(";");

        string profileName = $"TestProfile_{sourceType}To{destType}";
        return AddProfile(profileName, mappingBuilder.ToString());
    }

    /// <summary>
    ///     Add a mapping call scenario
    /// </summary>
    public TestScenarioBuilder AddMappingCall(string sourceVar, string sourceType, string destType,
        string mapperVar = "mapper")
    {
        _mappingCalls.Add($"var result = {mapperVar}.Map<{destType}>({sourceVar});");
        return this;
    }

    /// <summary>
    ///     Add a method that performs mapping
    /// </summary>
    public TestScenarioBuilder AddMappingMethod(string methodName, string sourceType, string destType,
        params string[] methodBody)
    {
        var methodBuilder = new StringBuilder();
        methodBuilder.AppendLine("public class MappingService");
        methodBuilder.AppendLine("{");
        methodBuilder.AppendLine("    private readonly IMapper _mapper;");
        methodBuilder.AppendLine("    public MappingService(IMapper mapper) => _mapper = mapper;");
        methodBuilder.AppendLine();
        methodBuilder.AppendLine($"    public {destType} {methodName}({sourceType} source)");
        methodBuilder.AppendLine("    {");

        if (methodBody.Length > 0)
        {
            foreach (string line in methodBody)
            {
                methodBuilder.AppendLine($"        {line}");
            }
        }
        else
        {
            methodBuilder.AppendLine($"        return _mapper.Map<{destType}>(source);");
        }

        methodBuilder.AppendLine("    }");
        methodBuilder.AppendLine("}");

        _classes.Add(methodBuilder.ToString());
        return this;
    }

    /// <summary>
    ///     Build the complete source code
    /// </summary>
    public string Build()
    {
        var result = new StringBuilder();

        // Add usings
        foreach (string usingStatement in _usings)
        {
            result.AppendLine($"using {usingStatement};");
        }

        result.AppendLine();

        // Add classes
        foreach (string classCode in _classes)
        {
            result.AppendLine(classCode);
            result.AppendLine();
        }

        // Add profiles
        foreach (string profileCode in _profiles)
        {
            result.AppendLine(profileCode);
            result.AppendLine();
        }

        // Add a test method if there are mapping calls
        if (_mappingCalls.Count > 0)
        {
            result.AppendLine("public class TestClass");
            result.AppendLine("{");
            result.AppendLine("    public void TestMethod()");
            result.AppendLine("    {");
            result.AppendLine("        var mapper = new Mock<IMapper>().Object;");

            foreach (string call in _mappingCalls)
            {
                result.AppendLine($"        {call}");
            }

            result.AppendLine("    }");
            result.AppendLine("}");
        }

        return result.ToString();
    }

    /// <summary>
    ///     Create a simple type mismatch scenario
    /// </summary>
    public static TestScenarioBuilder CreateTypeMismatchScenario()
    {
        return new TestScenarioBuilder()
            .AddClass("Source", ("string", "Age"))
            .AddClass("Destination", ("int", "Age"))
            .AddMapping("Source", "Destination");
    }

    /// <summary>
    ///     Create a nullable to non-nullable scenario
    /// </summary>
    public static TestScenarioBuilder CreateNullableScenario()
    {
        return new TestScenarioBuilder()
            .AddClass("Source", ("string?", "Name"))
            .AddClass("Destination", ("string", "Name"))
            .AddMapping("Source", "Destination");
    }

    /// <summary>
    ///     Create a missing property scenario
    /// </summary>
    public static TestScenarioBuilder CreateMissingPropertyScenario()
    {
        return new TestScenarioBuilder()
            .AddClass("Source", ("string", "Name"), ("string", "ImportantData"))
            .AddClass("Destination", ("string", "Name"))
            .AddMapping("Source", "Destination");
    }
}
