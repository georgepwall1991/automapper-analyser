using AutoMapperAnalyzer.Analyzers.Helpers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AutoMapperAnalyzer.Benchmarks;

/// <summary>
///     Benchmarks for analyzer performance measurement.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class AnalyzerBenchmarks
{
    private Compilation _simpleCompilation = null!;
    private Compilation _complexCompilation = null!;
    private SemanticModel _simpleSemanticModel = null!;
    private SemanticModel _complexSemanticModel = null!;

    private const string SimpleMapping = @"
using AutoMapper;

public class Source { public string Name { get; set; } }
public class Dest { public string Name { get; set; } }

public class TestProfile : Profile
{
    public TestProfile()
    {
        CreateMap<Source, Dest>();
    }
}";

    private const string ComplexMapping = @"
using AutoMapper;
using System.Collections.Generic;

public class OrderSource
{
    public int Id { get; set; }
    public string CustomerName { get; set; }
    public List<OrderItemSource> Items { get; set; }
    public AddressSource ShippingAddress { get; set; }
    public AddressSource BillingAddress { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; }
}

public class OrderItemSource
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class AddressSource
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public string Country { get; set; }
}

public class OrderDest
{
    public int Id { get; set; }
    public string CustomerName { get; set; }
    public List<OrderItemDest> Items { get; set; }
    public AddressDest ShippingAddress { get; set; }
    public AddressDest BillingAddress { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; }
}

public class OrderItemDest
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class AddressDest
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public string Country { get; set; }
}

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<OrderSource, OrderDest>();
        CreateMap<OrderItemSource, OrderItemDest>();
        CreateMap<AddressSource, AddressDest>();
    }
}";

    [GlobalSetup]
    public void Setup()
    {
        _simpleCompilation = CreateCompilation(SimpleMapping);
        _complexCompilation = CreateCompilation(ComplexMapping);

        _simpleSemanticModel = _simpleCompilation.GetSemanticModel(_simpleCompilation.SyntaxTrees.First());
        _complexSemanticModel = _complexCompilation.GetSemanticModel(_complexCompilation.SyntaxTrees.First());
    }

    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };

        return CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Benchmark(Baseline = true)]
    public void LevenshteinDistance_Short()
    {
        StringUtilities.ComputeLevenshteinDistance("Name", "Named");
    }

    [Benchmark]
    public void LevenshteinDistance_Medium()
    {
        StringUtilities.ComputeLevenshteinDistance("CustomerName", "CustomerNames");
    }

    [Benchmark]
    public void LevenshteinDistance_Long()
    {
        StringUtilities.ComputeLevenshteinDistance("ShippingAddressStreetLine1", "ShippingAddressStreetLine2");
    }

    [Benchmark]
    public bool AreNamesSimilar_Match()
    {
        return StringUtilities.AreNamesSimilar("CustomerName", "CostumerName");
    }

    [Benchmark]
    public bool AreNamesSimilar_NoMatch()
    {
        return StringUtilities.AreNamesSimilar("CustomerName", "OrderDate");
    }

    [Benchmark]
    public double ComputeSimilarityRatio()
    {
        return StringUtilities.ComputeSimilarityRatio("CustomerName", "CostumerName");
    }

    [Benchmark]
    public void CreateMapRegistry_Simple()
    {
        CreateMapRegistry.Build(_simpleCompilation);
    }

    [Benchmark]
    public void CreateMapRegistry_Complex()
    {
        CreateMapRegistry.Build(_complexCompilation);
    }
}
