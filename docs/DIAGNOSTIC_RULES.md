# Diagnostic Rules Reference

Complete reference guide for all AutoMapper Analyzer diagnostic rules, including examples, code fixes, and configuration options.

## Table of Contents

- [Overview](#overview)
- [Type Safety Rules (AM001-AM003)](#type-safety-rules)
- [Data Integrity Rules (AM004-AM005, AM011)](#data-integrity-rules)
- [Complex Mapping Rules (AM020-AM022)](#complex-mapping-rules)
- [Custom Conversion Rules (AM030)](#custom-conversion-rules)
- [Performance Rules (AM031)](#performance-rules)
- [Configuration](#configuration)
- [Suppression](#suppression)

---

## Overview

### Rule Categories

| Category | Rules | Purpose |
|----------|-------|---------|
| **Type Safety** | AM001-AM003 | Prevent type mismatches and conversion errors |
| **Data Integrity** | AM004-AM005, AM011 | Ensure complete data mapping |
| **Complex Mappings** | AM020-AM022 | Handle nested objects and collections |
| **Custom Conversions** | AM030 | Validate custom type converters |
| **Performance** | AM031 | Detect expensive operations in mappings |

### Severity Levels

- **Error** (🔴): Prevents successful runtime mapping
- **Warning** (🟡): May cause runtime issues
- **Info** (🔵): Suggestions for improvement

### Rule Ownership Contract

To avoid duplicate/conflicting diagnostics, each issue pattern has a single primary owner:

| Issue Pattern | Primary Rule | Suppressed Rules |
|----------|-------|---------|
| Nullable source to non-nullable destination (compatible underlying type) | `AM002` | `AM001`, `AM030` |
| Scalar incompatible type conversion | `AM001` | `AM030` |
| Collection container mismatch (`HashSet<T>` vs `List<T>`, etc.) | `AM003` | `AM021`, `AM030` |
| Collection element mismatch (`List<A>` to `List<B>`) with no `CreateMap<A,B>` | `AM021` | `AM003` (element branch), `AM030` |
| Nested complex property requires map (`Address` -> `AddressDto`) | `AM020` | `AM030` |
| Converter implementation quality problems | `AM030` | none |

### Code Fix Trust Levels

The checked-in `RuleCatalog` class classifies fixers so users can tell whether an action is a direct rewrite or starter
configuration:

The generated [rule catalog](RULE_CATALOG.md) is the compact source-of-truth view for descriptor metadata, sample links,
fixer providers, and trust levels.

| Trust Level | Meaning | Examples |
|----------|-------|---------|
| **Safe rewrite** | Behavior-preserving or convention-equivalent cleanup. | Removing redundant `MapFrom`, adding a missing nested `CreateMap`. |
| **Likely rewrite** | Reasonable generated mapping that should still be reviewed for domain policy. | Numeric/string conversions, collection conversion helpers, `MaxDepth`. |
| **Scaffold** | Compile-safe starter code or explicit suppression that requires manual review. | Default-value mappings and `Ignore()` actions for required/unmapped/performance diagnostics. |

Code action titles mark manual-review suppressions explicitly.

---

## Type Safety Rules

### AM001: Property Type Mismatch

**Severity**: Error 🔴
**Category**: AutoMapper.TypeSafety

#### Description

Detects incompatible property types between source and destination that cannot be automatically mapped without explicit conversion.

#### Problem

```csharp
public class Source
{
    public string Age { get; set; }  // string
}

public class Destination
{
    public int Age { get; set; }     // int
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ❌ AM001: Property 'Age' has incompatible types
    }
}
```

**Runtime Behavior**: AutoMapper will attempt `Convert.ChangeType()`, which throws `FormatException` if `Age = "abc"`.

#### Solution

**Code Fix: Add ForMember with Conversion**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Age, opt => opt.MapFrom(src =>
        int.TryParse(src.Age, out var age) ? age : 0));
```

For enum/string mismatches, AM001 offers direct conversion fixes such as `src.Status.ToString()` and
`src.Status != null ? global::System.Enum.Parse<global::MyApp.OrderStatus>(src.Status) : default`.

#### When to Use

- ✅ String → numeric conversions
- ✅ Enum → string conversions
- ✅ String → enum conversions
- ✅ Custom type conversions
- ❌ Compatible types (no warning needed)

#### Configuration

```ini
# .editorconfig
dotnet_diagnostic.AM001.severity = error
```

---

### AM002: Nullable Compatibility Issue

**Severity**: Error 🔴 for nullable source to non-nullable destination; Info 🔵 for non-nullable source to nullable destination
**Category**: AutoMapper.TypeSafety

#### Description

Detects nullable source properties mapped to non-nullable destination properties, which can violate the destination nullable contract at runtime. It also reports an informational diagnostic when a non-nullable source maps to a nullable destination so teams can simplify or document intentionally widened nullability.

#### Problem

```csharp
public class Source
{
    public string? FirstName { get; set; }  // Nullable
}

public class Destination
{
    public string FirstName { get; set; }   // Non-nullable
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ❌ AM002: FirstName nullable compatibility issue
    }
}
```

**Runtime Behavior**: If `source.FirstName == null`, the destination property receives null, violating its nullable contract.

#### Solutions

**Option 1: Provide Default Value**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src =>
        src.FirstName ?? "Unknown"));
```

**Option 2: Use Null Conditional**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src =>
        src.FirstName ?? string.Empty));
```

**Option 3: Make Destination Nullable**

```csharp
public class Destination
{
    public string? FirstName { get; set; }  // Now nullable
}
```

#### Safe Cases

AM002 does not report when the destination member is explicitly configured with `ForMember` or `ForPath`, when the map uses custom construction/conversion, when nullable reference annotations are disabled or oblivious, or when the nullable source and destination member have incompatible underlying types owned by `AM001`.

#### Configuration

```ini
dotnet_diagnostic.AM002.severity = error
```

---

### AM003: Collection Type Incompatibility

**Severity**: Error 🔴
**Category**: AutoMapper.Collections

#### Description

Detects incompatible **collection container** types that require explicit conversion configuration.

#### Problem

```csharp
public class Source
{
    public HashSet<string> Tags { get; set; }
}

public class Destination
{
    public List<string> Tags { get; set; }
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ❌ AM003: Collection 'Tags' has incompatible types
    }
}
```

**Issue**:
1. Collection container mismatch: `HashSet<T>` → `List<T>`

#### Solution

**Code Fix: Add ForMember with Container Conversion**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.ToList()));
```

#### Detected Incompatibilities

- ✅ `HashSet` ↔ `List`/`Array`
- ✅ `Queue` ↔ other collections
- ✅ `Stack` ↔ other collections
- ❌ Element type mismatches (owned by `AM021`)
- ❌ `List` → `Array` (AutoMapper handles this)
- ❌ Source collections already assignable to the destination contract, such as `T[]` → `IEnumerable<T>` or `HashSet<T>` → `IReadOnlyCollection<T>`

#### Configuration

```ini
dotnet_diagnostic.AM003.severity = error
```

---

## Data Integrity Rules

### AM004: Missing Destination Property

**Severity**: Warning 🟡
**Category**: AutoMapper.MissingProperty

#### Description

Detects unmapped source properties, preventing silent data loss.

#### Problem

```csharp
public class Source
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class Destination
{
    public int Id { get; set; }
    public string Name { get; set; }
    // Email property missing
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ⚠️ AM004: Property 'Email' will not be mapped
    }
}
```

**Runtime Behavior**: `source.Email` is silently ignored during mapping.

#### Solutions

**Option 1: Add Property to Destination (Smart Fix)**

```csharp
public class Destination
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }  // ✅ Added automatically by code fix
}
```

**Option 2: Explicitly Ignore**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Email, opt => opt.Ignore());
```

**Option 3: Map to Different Property**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.ContactEmail, opt => opt.MapFrom(src => src.Email));
```

#### Configuration

```ini
# Treat as error (recommended for data-critical applications)
dotnet_diagnostic.AM004.severity = error

# Treat as warning (default)
dotnet_diagnostic.AM004.severity = warning

# Information only
dotnet_diagnostic.AM004.severity = suggestion
```

---

### AM005: Case Sensitivity Mismatch

**Severity**: Warning 🟡
**Category**: AutoMapper.PropertyMapping

#### Description

Detects property name mismatches due to case sensitivity, which can cause cross-platform issues.

#### Problem

```csharp
public class Source
{
    public string UserName { get; set; }  // PascalCase
}

public class Destination
{
    public string Username { get; set; }   // Different casing
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ⚠️ AM005: Property 'UserName' case mismatch
    }
}
```

**Why This Matters**:
- AutoMapper uses case-insensitive matching by default
- Some serializers (JSON.NET) are case-sensitive
- Can cause issues in cross-platform scenarios

#### Solution

**Option 1: Standardize Naming**

```csharp
public class Destination
{
    public string UserName { get; set; }  // ✅ Matches source
}
```

**Option 2: Explicit Mapping**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.UserName));
```

#### Configuration

```ini
dotnet_diagnostic.AM005.severity = warning
```

---

### AM006: Unmapped Destination Property

**Severity**: Info 🔵
**Category**: AutoMapper.DataIntegrity

#### Description

Detects destination properties that have no corresponding source property and are not explicitly mapped.

#### Problem

```csharp
public class Source
{
    public string Name { get; set; }
}

public class Destination
{
    public string Name { get; set; }
    public string ExtraInfo { get; set; } // No matching source
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ℹ️ AM006: Destination property 'ExtraInfo' is not mapped
    }
}
```

**Runtime Behavior**: `destination.ExtraInfo` will remain at its default value (null/zero).

#### Solutions

**Option 1: Add Source Property**

```csharp
public class Source
{
    public string Name { get; set; }
    public string ExtraInfo { get; set; }  // ✅ Added
}
```

**Option 2: Explicitly Ignore**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.ExtraInfo, opt => opt.Ignore());
```

**Option 3: Explicit Mapping**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.ExtraInfo, opt => opt.MapFrom(src => "Default Value"));
```

#### Code Fixes

**Per-property fixes:**
- Map from similar source property (fuzzy match suggestion)
- Ignore destination property (`ForMember` + `Ignore`)
- Create property in source type

**Bulk fixes:**
- Ignore all unmapped destination properties
- Create all missing properties in source type

#### Configuration

```ini
dotnet_diagnostic.AM006.severity = suggestion
```

---

### AM011: Unmapped Required Property

**Severity**: Error 🔴
**Category**: AutoMapper.DataIntegrity

#### Description

Detects destination properties marked as `required` (C# 11+) that have no corresponding source property or explicit destination configuration.

#### Problem

```csharp
public class Source
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class Destination
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }  // No source property
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ❌ AM011: Required property 'Email' is not mapped
    }
}
```

**Runtime Behavior**: Throws `InvalidOperationException` if `Email` is not initialized.

#### Solutions

**Option 1: Fuzzy Match Suggestion**

If Source has `UserEmail`, the analyzer suggests:

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.UserEmail));
```

**Option 2: Provide Default Value**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Email, opt => opt.MapFrom(src => "noreply@example.com"));
```

Use this only when the default value is valid domain data. The code fix may generate a compilable starter value such as `string.Empty`, `0`, or `false`, but required members usually deserve an intentional source mapping.

**Option 3: Configure With ForPath**

```csharp
CreateMap<Source, Destination>()
    .ForPath(dest => dest.Email, opt => opt.MapFrom(src => src.ContactEmail));
```

`AM011` treats `ForMember`, `ForPath`, and `ForCtorParam` as explicit required-member configuration. It also stays quiet when custom construction or conversion is present because those paths can initialize required members outside ordinary member mapping.

**Option 4: Make Property Optional**

```csharp
public class Destination
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }  // ✅ Now optional
}
```

**Manual Review Boundary**: The fixer can suggest a unique fuzzy source-property match or add a default-value mapping. If you choose to ignore a required member, verify that another construction path initializes it or that leaving it unset is intentional.

#### Configuration

```ini
# Always enforce (recommended)
dotnet_diagnostic.AM011.severity = error
```

---

## Complex Mapping Rules

### AM020: Nested Object Mapping Issue

**Severity**: Warning 🟡
**Category**: AutoMapper.ComplexMappings

#### Description

Detects nested object properties that require explicit `CreateMap` configuration, including support for `internal` properties and cross-profile detection.

#### Problem

```csharp
public class SourceAddress
{
    public string Street { get; set; }
}

public class DestinationAddress
{
    public string Street { get; set; }
}

public class Source
{
    public SourceAddress Address { get; set; }
}

public class Destination
{
    public DestinationAddress Address { get; set; }
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ⚠️ AM020: Nested property 'Address' needs CreateMap configuration
    }
}
```

**Runtime Behavior**: Throws `AutoMapperMappingException` when mapping nested objects without configuration.

#### Solutions

**Code Fix: Add CreateMap for Nested Type**

```csharp
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        CreateMap<SourceAddress, DestinationAddress>();  // ✅ Added
    }
}
```

#### Cross-Profile Detection

The analyzer detects mappings defined in other profiles:

```csharp
// AddressProfile.cs
public class AddressProfile : Profile
{
    public AddressProfile()
    {
        CreateMap<SourceAddress, DestinationAddress>();
    }
}

// PersonProfile.cs
public class PersonProfile : Profile
{
    public PersonProfile()
    {
        CreateMap<Source, Destination>();
        // ✅ No warning - mapping found in AddressProfile
    }
}
```

#### Internal Property Support

Also detects issues with `internal` properties:

```csharp
public class Source
{
    internal SourceAddress InternalAddress { get; set; }
}

public class Destination
{
    internal DestinationAddress InternalAddress { get; set; }
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ⚠️ AM020: Internal property 'InternalAddress' needs mapping
    }
}
```

#### Configuration

```ini
dotnet_diagnostic.AM020.severity = warning
```

---

### AM021: Collection Element Mismatch

**Severity**: Warning 🟡
**Category**: AutoMapper.ComplexMappings

#### Description

Detects collection properties where **container types are compatible** but element types are incompatible and require custom mapping.

#### Problem

```csharp
public class SourceItem
{
    public string Name { get; set; }
}

public class DestinationItem
{
    public string Title { get; set; }  // Different property name
}

public class Source
{
    public List<SourceItem> Items { get; set; }
}

public class Destination
{
    public List<DestinationItem> Items { get; set; }
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        // ⚠️ AM021: Collection 'Items' has incompatible element types
    }
}
```

`AM021` checks for an existing `CreateMap<SourceItem, DestinationItem>()` before reporting. If element mapping exists, no diagnostic is emitted.

When a parent map uses `ReverseMap()`, AM021 also checks the reverse element direction. A forward
`CreateMap<SourceItem, DestinationItem>()` does not automatically prove that
`CreateMap<DestinationItem, SourceItem>()` exists, so reverse collection maps still need their own element map or explicit
reverse configuration. To keep the signal focused, AM021 reports the forward missing element map first and does not add a
second reverse diagnostic for the same collection until the forward direction is configured.

If collection containers are incompatible (`HashSet<T>` vs `List<T>`, `Queue<T>` vs `Stack<T>`, etc.), `AM003` owns the diagnostic. AM003 stays quiet when the source collection is already assignable to the destination collection contract.

Dictionary value/key mismatches are treated as `KeyValuePair<TKey, TValue>` element mismatches. For those diagnostics the fixer intentionally offers only the manual ignore action, because adding a `CreateMap<KeyValuePair<...>, KeyValuePair<...>>()` registration is not a reliable executable rewrite.

#### Solution

**Code Fix: Add CreateMap for Element Types**

```csharp
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Source, Destination>();
        CreateMap<SourceItem, DestinationItem>()
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Name));
    }
}
```

#### Configuration

```ini
dotnet_diagnostic.AM021.severity = warning
```

---

### AM022: Infinite Recursion Risk

**Severity**: Warning 🟡
**Category**: AutoMapper.ComplexMappings

#### Description

Detects circular reference patterns in convention-mapped object graphs that can cause stack overflow without `MaxDepth`
configuration.

#### Problem

```csharp
public class SourcePerson
{
    public string Name { get; set; }
    public SourcePerson Friend { get; set; }  // Self-reference
}

public class DestinationPerson
{
    public string Name { get; set; }
    public DestinationPerson Friend { get; set; }  // Self-reference
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<SourcePerson, DestinationPerson>();
        // ⚠️ AM022: Potential infinite recursion detected
    }
}
```

**Runtime Behavior**: Stack overflow if object graph has circular references.

#### Solutions

**Option 1: Add MaxDepth (Code Fix)**

```csharp
CreateMap<SourcePerson, DestinationPerson>()
    .MaxDepth(3);  // ✅ Limits recursion depth
```

**Option 2: Preserve References**

```csharp
CreateMap<SourcePerson, DestinationPerson>()
    .PreserveReferences();  // ✅ Reuses already-mapped references
```

**Option 3: Ignore Circular Property**

```csharp
CreateMap<SourcePerson, DestinationPerson>()
    .ForMember(dest => dest.Friend, opt => opt.Ignore());
```

**Option 4: Implement Custom Resolution**

```csharp
CreateMap<SourcePerson, DestinationPerson>()
    .ForMember(dest => dest.Friend, opt => opt.MapFrom((src, dest, destMember, context) =>
    {
        if (context.Items.ContainsKey("depth"))
        {
            var depth = (int)context.Items["depth"];
            if (depth > 3) return null;
            context.Items["depth"] = depth + 1;
        }
        else
        {
            context.Items["depth"] = 1;
        }
        return src.Friend;
    }));
```

#### Indirect Cycles

Also detects indirect circular references when the cycle is reachable through matching source and destination member
names:

```csharp
public class SourceA { public SourceB RelatedB { get; set; } }
public class SourceB { public SourceC RelatedC { get; set; } }
public class SourceC { public SourceA RelatedA { get; set; } }  // Cycle: A→B→C→A

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<SourceA, DestA>();
        CreateMap<SourceB, DestB>();
        CreateMap<SourceC, DestC>();
        // ⚠️ AM022: Circular reference detected in mapping chain
    }
}
```

AM022 is intentionally conservative: unrelated cycles on the source and destination types do not report unless the
recursive member path is actually convention-mapped and each indirect nested type pair has a configured `CreateMap`
that AutoMapper can use for recursion. Ignoring the top-level recursive destination member with
`ForMember(..., opt => opt.Ignore())` or `ForPath(..., opt => opt.Ignore())` suppresses the diagnostic. Forward
`MaxDepth`, `PreserveReferences`, and `ConvertUsing` configuration also suppress AM022 because those mapping shapes
own recursion behavior explicitly.

#### Configuration

```ini
dotnet_diagnostic.AM022.severity = warning
```

---

## Custom Conversion Rules

### AM030: Custom Type Converter Issues

**Severity**: Mixed (Error/Warning/Info)
**Category**: AutoMapper.CustomConversions

#### Description

Validates custom type converter implementations and detects converter-quality issues:
- Invalid converter implementation/signature (`Error`)
- Missing null handling for nullable source converters (`Warning`)
- Unused converter declarations (`Info`)

`AM030` no longer reports missing property-level conversion setup. Those mismatches are owned by `AM001`, `AM020`, and `AM021`.
Unused-converter analysis treats generic, instance, and type-based converter configuration as usage, including
`ConvertUsing<MyConverter>()`, `ConvertUsing(new MyConverter())`, and `ConvertUsing(typeof(MyConverter))`.
It also recognizes simple local, field, or property initializers where an `ITypeConverter<TSource, TDestination>`
variable is initialized with a concrete converter and then passed to `ConvertUsing(converter)`.

#### Problem 1: Invalid Converter Signature

```csharp
public class StringToIntConverter : ITypeConverter<string, int>
{
    // ❌ Wrong signature - should return 'int', not 'object'
    public object Convert(string source, int destination, ResolutionContext context)
    {
        return int.TryParse(source, out var result) ? result : 0;
    }
}

CreateMap<Source, Destination>()
    .ConvertUsing<StringToIntConverter>();
// ⚠️ AM030: Converter has invalid signature
```

#### Problem 2: Missing Null Handling

```csharp
public class StringToIntConverter : ITypeConverter<string, int>
{
    public int Convert(string source, int destination, ResolutionContext context)
    {
        // ❌ No null check - will throw if source is null
        return int.Parse(source);
    }
}

CreateMap<string?, int>()
    .ConvertUsing<StringToIntConverter>();
// ⚠️ AM030: Converter doesn't handle null values
```

#### Problem 3: Unused Converter

```csharp
public class LegacyConverter : ITypeConverter<string, DateTime>
{
    public DateTime Convert(string source, DateTime destination, ResolutionContext context)
        => DateTime.Parse(source);
}

// Converter declared but never used in ConvertUsing
// ℹ️ AM030: Type converter 'LegacyConverter' is defined but not used
```

No diagnostic is reported when the converter is referenced through a supported AutoMapper converter overload:

```csharp
CreateMap<string, DateTime>()
    .ConvertUsing(typeof(LegacyConverter)); // ✅ counted as usage

ITypeConverter<string, DateTime> converter = new LegacyConverter();
CreateMap<string, DateTime>()
    .ConvertUsing(converter); // ✅ counted as usage
```

#### Solutions

**Option 1: Fix Converter Implementation**

```csharp
public class StringToIntConverter : ITypeConverter<string, int>
{
    public int Convert(string source, int destination, ResolutionContext context)
    {
        // ✅ Null check added
        if (string.IsNullOrWhiteSpace(source))
            return 0;

        return int.TryParse(source, out var result) ? result : 0;
    }
}
```

**Option 2: Add Null Guard (Code Fix)**

For nullable-source converters, AM030 offers an executable fix that inserts:

```csharp
if (source == null) throw new global::System.ArgumentNullException(nameof(source));
```

The generated guard is fully qualified so the fixer does not add, reorder, or duplicate `using System` directives.

#### Configuration

```ini
dotnet_diagnostic.AM030.severity = warning
```

---

## Performance Rules

### AM031: Performance Warning

**Severity**: Warning 🟡 / Info 🔵
**Category**: AutoMapper.Performance

#### Description

Detects expensive operations inside mapping expressions that should be performed before mapping.

#### Problem 1: Database Query in Mapping

```csharp
public class MappingProfile : Profile
{
    private readonly DbContext _db;

    public MappingProfile(DbContext db)
    {
        _db = db;

        CreateMap<Source, Destination>()
            .ForMember(dest => dest.OrderCount, opt => opt.MapFrom(src =>
                _db.Orders.Count(o => o.UserId == src.Id)));  // ❌ Database query
        // ⚠️ AM031: Expensive database operation detected
    }
}
```

**Runtime Impact**: Database query executes **for every mapped object**, causing N+1 query problem.

#### Problem 2: Multiple Enumeration

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Total, opt => opt.MapFrom(src =>
        src.Numbers.Sum() + src.Numbers.Average()));  // ❌ Enumerates twice
// ⚠️ AM031: Multiple enumeration of collection
```

#### Problem 3: Non-Deterministic Operations

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.DaysOld, opt => opt.MapFrom(src =>
        (DateTime.Now - src.CreatedDate).Days));  // ❌ DateTime.Now
// ⚠️ AM031: Non-deterministic operation (DateTime.Now)
```

**Issue**: Mapping same object twice produces different results, breaks unit tests, and causes caching issues.

#### Problem 4: Task.Result Deadlock Risk

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Data, opt => opt.MapFrom(src =>
        _service.GetDataAsync(src.Id).Result));  // ❌ Synchronous access
// ⚠️ AM031: Task.Result can cause deadlocks
```

Task-valued source members are also detected:

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Data, opt => opt.MapFrom(src =>
        src.DataTask.Result));  // ❌ Synchronous access
```

#### Solutions

**Code Fix 1: Cache Collection Enumeration**

AM031 offers this executable rewrite only when the repeated enumeration is rooted in the source mapping parameter, including nested source paths such as `src.Customer.Orders`. Captured fields, injected services, and other closure values keep manual-review actions because the analyzer cannot safely decide where those values should be cached.

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Total, opt => opt.MapFrom(src =>
    {
        var numbersCache = src.Numbers.ToList();  // ✅ Cached
        return numbersCache.Sum() + numbersCache.Average();
    }));
```

**Code Fix 2: Ignore Mapping for the Expensive Property**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.OrderCount, opt => opt.Ignore());
```

**Code Fix 3: Remove Redundant ForMember (when convention mapping is valid)**

If source/destination have compatible same-name members, AM031 can remove the redundant `ForMember(...)` and let AutoMapper convention mapping apply.

#### Detected Patterns

- ✅ Database queries (EF Core, Dapper, SQL)
- ✅ File I/O operations
- ✅ HTTP/API calls
- ✅ Reflection operations
- ✅ Multiple collection enumerations
- ✅ `DateTime.Now`, `Random`, `Guid.NewGuid()`
- ✅ `Task.Result`, `Task.Wait()`, including Task-valued source properties
- ✅ Complex LINQ (SelectMany chains)

#### Configuration

```ini
dotnet_diagnostic.AM031.severity = warning

# Specific sub-rules
dotnet_diagnostic.AM031.001.severity = error  # Expensive operations
dotnet_diagnostic.AM031.002.severity = warning  # Multiple enumerations
dotnet_diagnostic.AM031.003.severity = error  # Task.Result deadlock
dotnet_diagnostic.AM031.004.severity = warning  # Non-deterministic
```

---

## Configuration Analysis Rules

### AM041: Duplicate Mapping Registration

**Severity**: Warning 🟡
**Category**: AutoMapper.Configuration

#### Description

Detects multiple `CreateMap<TSource, TDest>()` definitions for the same types within the compilation, which creates ambiguity and runtime issues.
Diagnostics include constructed generic type arguments and array element types/ranks, so duplicate collection maps are reported as `List<Source>` to `List<Destination>` instead of only `List` to `List`.

#### Problem

```csharp
public class MyProfile : Profile
{
    public MyProfile()
    {
        CreateMap<Source, Destination>();
        CreateMap<Source, Destination>(); // ❌ AM041: Duplicate mapping
    }
}
```

#### Solution

**Code Fix: Remove Duplicate Registration**

```csharp
public class MyProfile : Profile
{
    public MyProfile()
    {
        CreateMap<Source, Destination>();
        // Duplicate removed
    }
}
```

#### Configuration

```ini
dotnet_diagnostic.AM041.severity = warning
```

---

### AM050: Redundant MapFrom Configuration

**Severity**: Info 🔵
**Category**: AutoMapper.Configuration

#### Description

Detects explicit `MapFrom` calls where the source and destination properties have the same name. AutoMapper maps these automatically by default.

AM050 only reports when it can prove the source and destination members have the same type, including string-based destination member names such as `ForMember("Name", ...)`. It stays quiet when the destination member cannot be resolved or the same-name members have different types.

#### Problem

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name)); // ℹ️ AM050: Redundant
```

#### Solution

**Code Fix: Remove Redundant Configuration**

```csharp
CreateMap<Source, Destination>();
// AutoMapper automatically maps 'Name' to 'Name'
```

#### Configuration

```ini
dotnet_diagnostic.AM050.severity = suggestion
```

---

## Configuration

### Global Configuration

**`.editorconfig`** (recommended):

```ini
# AutoMapper Analyzer Rules

# Type Safety - Treat as errors
dotnet_diagnostic.AM001.severity = error
dotnet_diagnostic.AM002.severity = error
dotnet_diagnostic.AM003.severity = error

# Data Integrity - Treat as warnings
dotnet_diagnostic.AM004.severity = warning
dotnet_diagnostic.AM005.severity = warning
dotnet_diagnostic.AM011.severity = error

# Complex Mappings - Treat as warnings
dotnet_diagnostic.AM020.severity = warning
dotnet_diagnostic.AM021.severity = warning
dotnet_diagnostic.AM022.severity = warning

# Custom Conversions - Treat as warnings
dotnet_diagnostic.AM030.severity = warning

# Performance - Treat expensive operations as errors
dotnet_diagnostic.AM031.severity = warning
dotnet_diagnostic.AM031.001.severity = error
```

### Project-Level Configuration

**`.csproj`** file:

```xml
<PropertyGroup>
    <!-- Disable specific rules -->
    <NoWarn>AM004;AM005</NoWarn>

    <!-- Treat warnings as errors -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

---

## Suppression

### Pragma Directives

Suppress specific warnings in code:

```csharp
#pragma warning disable AM001  // Reason: Custom IValueConverter handles conversion
CreateMap<Source, Destination>();
#pragma warning restore AM001
```

### Attribute Suppression

Suppress at method/class level:

```csharp
[SuppressMessage("AutoMapper", "AM004:Missing destination property",
    Justification = "PII data intentionally excluded for GDPR compliance")]
public class SensitiveDataMappingProfile : Profile
{
    public SensitiveDataMappingProfile()
    {
        CreateMap<UserEntity, PublicUserDto>();
    }
}
```

### Global Suppression

**`GlobalSuppressions.cs`**:

```csharp
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("AutoMapper", "AM004:Missing destination property",
    Scope = "type",
    Target = "~T:MyApp.Profiles.LegacyMappingProfile",
    Justification = "Legacy code being phased out")]
```

---

## Troubleshooting

### Analyzer Not Running

1. **Check package reference**:
   ```xml
   <PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.30.13">
       <PrivateAssets>all</PrivateAssets>
       <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
   </PackageReference>
   ```

2. **Rebuild project**: `dotnet clean && dotnet build`

3. **Check IDE support**: Restart Visual Studio/Rider/VS Code

### False Positives

If you encounter false positives:

1. **Verify AutoMapper version**: Analyzer requires AutoMapper 10.1.1+
2. **Check code patterns**: Ensure using standard AutoMapper patterns
3. **Report issue**: https://github.com/georgepwall1991/automapper-analyser/issues

### Performance Issues

If analyzer slows down builds:

1. **Check project size**: Analyzer optimized for <10,000 files
2. **Disable specific rules**: Use `.editorconfig` to disable expensive rules
3. **Report performance**: Include build logs in issue report

---

## Additional Resources

- [Architecture Guide](./ARCHITECTURE.md)
- [AutoMapper Documentation](https://docs.automapper.org/)
- [Project README](../README.md)
- [Contributing Guide](../CONTRIBUTING.md)
- [GitHub Issues](https://github.com/georgepwall1991/automapper-analyser/issues)

---

**Last Updated**: 2025-11-19
**Version**: 2.5.0
**Maintainer**: George Wall
