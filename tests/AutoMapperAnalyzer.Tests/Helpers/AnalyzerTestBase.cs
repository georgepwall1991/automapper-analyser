using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System.Collections.Immutable;

namespace AutoMapperAnalyzer.Tests.Helpers;

/// <summary>
/// Base class for analyzer tests providing common setup and utilities
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer type being tested</typeparam>
public abstract class AnalyzerTestBase<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// Creates a test instance for the analyzer
    /// </summary>
    protected virtual CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> CreateTest()
    {
        return new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                // Add common references
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                AdditionalReferences =
                {
                    // AutoMapper reference
                    MetadataReference.CreateFromFile(typeof(AutoMapper.IMapper).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(AutoMapper.Profile).Assembly.Location),
                }
            }
        };
    }

    /// <summary>
    /// Runs analyzer test with the provided source code
    /// </summary>
    protected async Task RunAnalyzerTestAsync(string source, params DiagnosticResult[] expected)
    {
        var test = CreateTest();
        test.TestCode = source;
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    /// <summary>
    /// Runs analyzer test with multiple source files
    /// </summary>
    protected async Task RunAnalyzerTestAsync(string[] sources, params DiagnosticResult[] expected)
    {
        var test = CreateTest();
        
        for (int i = 0; i < sources.Length; i++)
        {
            if (i == 0)
            {
                test.TestCode = sources[i];
            }
            else
            {
                test.TestState.Sources.Add(($"TestFile{i}.cs", sources[i]));
            }
        }
        
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    /// <summary>
    /// Creates a diagnostic result for the specified rule
    /// </summary>
    protected DiagnosticResult CreateDiagnostic(DiagnosticDescriptor rule, int line, int column, params object[] messageArgs)
    {
        return new DiagnosticResult(rule)
            .WithLocation(line, column)
            .WithArguments(messageArgs);
    }

    /// <summary>
    /// Creates a diagnostic result with span information
    /// </summary>
    protected DiagnosticResult CreateDiagnostic(DiagnosticDescriptor rule, int startLine, int startColumn, int endLine, int endColumn, params object[] messageArgs)
    {
        return new DiagnosticResult(rule)
            .WithSpan(startLine, startColumn, endLine, endColumn)
            .WithArguments(messageArgs);
    }

    /// <summary>
    /// Common AutoMapper using statements for test code
    /// </summary>
    protected const string AutoMapperUsings = @"
using AutoMapper;
using AutoMapper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
";

    /// <summary>
    /// Creates a simple source template with AutoMapper usings
    /// </summary>
    protected string CreateSourceWithUsings(string code)
    {
        return $@"{AutoMapperUsings}

{code}";
    }
} 