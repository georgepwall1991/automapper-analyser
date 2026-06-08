# Diagnostic Rules Reference

Complete reference guide for all AutoMapper Analyzer diagnostic rules, including examples, code fixes, and configuration options.

## Table of Contents

- [Overview](#overview)
- [Type Safety Rules (AM001-AM003)](#type-safety-rules)
- [Data Integrity Rules (AM004-AM006, AM011)](#data-integrity-rules)
- [Complex Mapping Rules (AM020-AM022)](#complex-mapping-rules)
- [Custom Conversion Rules (AM030, AM032-AM033)](#custom-conversion-rules)
- [Performance Rules (AM031)](#performance-rules)
- [Configuration](#configuration)
- [Suppression](#suppression)

---

## Overview

### Rule Categories

| Category | Rules | Purpose |
|----------|-------|---------|
| **Type Safety** | AM001-AM003 | Prevent type mismatches and conversion errors |
| **Data Integrity** | AM004-AM006, AM011 | Ensure complete data mapping |
| **Complex Mappings** | AM020-AM022 | Handle nested objects and collections |
| **Custom Conversions** | AM030, AM032-AM033 | Validate custom type converters |
| **Performance** | AM031 | Detect expensive operations in mappings |

### Severity Levels

- **Error** (🔴): Prevents successful runtime mapping
- **Warning** (🟡): May cause runtime issues
- **Info** (🔵): Suggestions for improvement

### Rule Ownership Contract

To avoid duplicate/conflicting diagnostics, each issue pattern has a single primary owner:

| Issue Pattern | Primary Rule | Suppressed Rules |
|----------|-------|---------|
| Nullable source to non-nullable destination (compatible underlying type) | `AM002` | `AM001`, `AM030-AM033` |
| Scalar incompatible type conversion | `AM001` | `AM030-AM033` |
| Collection container mismatch (`HashSet<T>` vs `List<T>`, etc.) | `AM003` | `AM021`, `AM030-AM033` |
| Collection element mismatch (`List<A>` to `List<B>`) with no `CreateMap<A,B>` | `AM021` | `AM003` (element branch), `AM030-AM033` |
| Nested complex property requires map (`Address` -> `AddressDto`) | `AM020` | `AM030-AM033` |
| Invalid converter implementation/signature | `AM030` | none |
| Nullable-source converter missing null handling | `AM032` | none |
| Declared converter not used by any `ConvertUsing` configuration | `AM033` | none |

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
| **No fix** | Diagnostic is intentionally analyzer-only because there is no safe automatic edit. | Informational nullability widening, invalid converter implementations, and unused converter cleanup. |

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
For numeric properties, AM001 follows the C# predefined implicit numeric conversion table: legal widenings
such as `char` to `int` stay quiet, while conversions that require explicit casts, such as `double` to
`decimal`, report and receive a cast-based `MapFrom` fix.

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
**Category**: AutoMapper.NullSafety

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

AM002 code fixes are offered only for the nullable-source to non-nullable-destination `Error` descriptor, where a default
mapping or manual-review ignore can make the mapping explicit. The non-nullable-source to nullable-destination `Info`
descriptor is analyzer-only and does not offer a code action.

#### Safe Cases

AM002 does not report when the destination member is explicitly configured to handle nulls, such as `MapFrom(src => src.Name ?? "fallback")`, a safe AutoMapper `NullSubstitute("fallback")`, an assignable `NullSubstitute` fallback such as a `string` value for an `object` destination member, a typed value-type default such as `NullSubstitute(default(int))`, guarded nullable dereferences such as `src.Name == null ? string.Empty : src.Name.Trim()`, nullable value defaults such as `src.Count.GetValueOrDefault()`, AutoMapper `Ignore()`, a proven non-null-producing resolver expression that does not dereference a nullable receiver unsafely, a custom resolver form such as `MapFrom<TResolver>()` or `MapFrom<TResolver, TSourceMember>(...)`, or a member-level value converter such as `ConvertUsing<TConverter, TSourceMember>(...)`. Pass-through, unguarded nullable-receiver dereferences, different-member, and generic expression mappings like `MapFrom(src => src.Name)`, `MapFrom(src => src.Name.Trim())`, `MapFrom(src => src.Name.Length)`, `MapFrom(src => src.OtherNullableName)`, and `MapFrom<TSourceMember>(src => src.Name)` still report when the mapped value can come from a nullable source and the destination member is non-nullable, and diagnostics name the actual nullable source member used by the explicit mapping. Unsafe substitutes such as `NullSubstitute(null)`, `NullSubstitute(default)`, and nullable/reference typed defaults still report. Helper methods named `Ignore`, `NullSubstitute`, or `MapFrom` are not treated as AutoMapper null-handling or mapping options unless they are invoked on the AutoMapper options parameter. AM002 also stays quiet when the map uses custom construction/conversion, when nullable reference annotations are disabled or oblivious, or when the nullable source and destination member have incompatible underlying types owned by `AM001`.

When the same destination member is configured more than once, AM002 evaluates and fixes the later effective configuration. The default-value fixer preserves existing top-level `ForMember` and top-level `ForPath` options when it adds null handling, emits fully qualified framework defaults such as `global::System.DateTime.MinValue`, and uses `default!` for generic/reference fallback defaults where plain `default` would remain nullable, but it does not reuse child `ForPath` mappings as top-level nullable-property fixes and it withholds the default-value action when existing `Condition` or `PreCondition` guards can veto assignment or when an existing `MapFrom` dereferences a nullable receiver before any fallback could run.
Child-only `ForPath` configuration also does not suppress AM002 for a nullable top-level source member, because it does not construct or default the parent destination object.

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

For interface destinations such as `IList<T>`, `ICollection<T>`, or `IReadOnlyCollection<T>`, the fixer maps through a
concrete collection expression like `ToList()` rather than trying to construct the interface. For set interfaces such as
`ISet<T>`, it uses a concrete `HashSet<T>` constructor. The manual-review ignore action remains available alongside safe
automatic conversions. For unsupported custom collection destination types, AM003 keeps the diagnostic but withholds
speculative constructor rewrites and offers only the manual-review ignore action. Known BCL collection destinations with
safe collection constructors, such as `SortedSet<T>` and `LinkedList<T>`, still receive constructor-based mapping actions.
Immutable/frozen destination containers use fully qualified `ImmutableList.CreateRange(...)`,
`ImmutableArray.CreateRange(...)`, `ImmutableHashSet.CreateRange(...)`, or `FrozenSet.ToFrozenSet(...)` factory calls so
generated mappings remain executable without depending on user imports.

#### Detected Incompatibilities

- ✅ `HashSet` ↔ `List`/`Array`
- ✅ `Queue` ↔ other collections
- ✅ `Stack` ↔ other collections
- ✅ Mutable collections → `ImmutableList<T>`, `ImmutableArray<T>`, `ImmutableHashSet<T>`, or `FrozenSet<T>`
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

**Option 1: Add Property to Destination**

```csharp
public class Destination
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }  // ✅ Added intentionally
}
```

**Option 2: Map to a Similar Destination Property (Code Fix)**

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.ContactEmail, opt => opt.MapFrom(src => src.Email));
```

AM004 offers this when it finds one compatible destination property with a strong fuzzy-name match.

**Option 3: Suppress Source Validation (Code Fix, Manual Review)**

```csharp
CreateMap<Source, Destination>()
    .ForSourceMember(src => src.Email, opt => opt.DoNotValidate());
```

Use `DoNotValidate()` only when dropping the source member is intentional.

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
- Ignore destination property (`ForMember` + `Ignore`, manual review)

#### Safe Cases

AM006 does not report when the destination member is matched by convention, configured with `ForMember` or `ForPath`, covered by flattening, explicitly initialized in every returned `ConstructUsing` object initializer, or when `ConvertUsing` owns destination object creation. String-based `ForMember("Name", ...)` configuration must name the exact top-level destination member; dotted destination paths should use `ForPath`.

#### Configuration

```ini
dotnet_diagnostic.AM006.severity = suggestion
```

---

### AM011: Unmapped Required Property

**Severity**: Error 🔴
**Category**: AutoMapper.RequiredProperties

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

`AM011` treats `ForMember`, `ForPath`, and `ForCtorParam` as explicit required-member configuration. String-based `ForMember("Name", ...)` configuration must name the exact top-level required member; dotted destination paths should use `ForPath`. It also stays quiet when custom construction or conversion is present because those paths can initialize required members outside ordinary member mapping.

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
**Category**: AutoMapper.NestedObjects

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

AM020 also respects compiler-known implicit nested conversions. A nested property mapping such as `SourceAddress` to
`DestinationAddress` stays quiet when `SourceAddress` defines an implicit conversion to `DestinationAddress`, while an
explicit-only conversion still reports and requires a nested `CreateMap` or explicit member mapping.

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
**Category**: AutoMapper.Collections

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

`AM021` also respects compiler-known implicit element conversions. For example, a collection mapping from
`List<Money>` to `List<decimal>` stays quiet when `Money` defines an implicit conversion to `decimal`, while an
explicit-only conversion still reports and requires mapping configuration.

When a parent map uses `ReverseMap()`, AM021 also checks the reverse element direction. A forward
`CreateMap<SourceItem, DestinationItem>()` does not automatically prove that
`CreateMap<DestinationItem, SourceItem>()` exists, so reverse collection maps still need their own element map or explicit
reverse configuration. To keep the signal focused, AM021 reports the forward missing element map first and does not add a
second reverse diagnostic for the same collection until the forward direction is configured.

If collection containers are incompatible (`HashSet<T>` vs `List<T>`, `Queue<T>` vs `Stack<T>`, etc.), `AM003` owns the diagnostic. AM003 stays quiet when the source collection is already assignable to the destination collection contract.

Dictionary value/key mismatches are treated as `KeyValuePair<TKey, TValue>` element mismatches. For those diagnostics the fixer intentionally offers only the manual ignore action, because adding a `CreateMap<KeyValuePair<...>, KeyValuePair<...>>()` registration is not a reliable executable rewrite.

For simple element conversions, AM021 generates `global::System.Convert`, `global::System.DateTime`, and `global::System.Guid` calls so the fix remains stable even when the project contains types with the same short names.

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

For simple element conversions, AM021 can add a `Select(...)` mapping. List-like interface destinations use `ToList()`,
`HashSet<T>`/`ISet<T>` destinations are wrapped in a concrete `HashSet<T>` constructor, and known immutable/frozen
destinations use fully qualified `ImmutableList.CreateRange(...)`, `ImmutableArray.CreateRange(...)`,
`ImmutableHashSet.CreateRange(...)`, or `FrozenSet.ToFrozenSet(...)` calls so the generated mapping stays executable.
Custom collection lookalikes remain on the manual-review path instead of receiving name-based rewrites.

#### Configuration

```ini
dotnet_diagnostic.AM021.severity = warning
```

---

### AM022: Infinite Recursion Risk

**Severity**: Warning 🟡
**Category**: AutoMapper.Recursion

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

### AM030: Invalid Type Converter Implementation

**Severity**: Error
**Category**: AutoMapper.Converters

#### Description

Reports custom converter classes that claim to implement `ITypeConverter<TSource, TDestination>` but do not provide the
required `Convert(TSource source, TDestination destination, ResolutionContext context)` implementation.

`AM030` no longer reports missing property-level conversion setup. Those mismatches are owned by `AM001`, `AM020`, and `AM021`.

#### Problem

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

#### Solution

Implement the exact AutoMapper converter method signature:

```csharp
public class StringToIntConverter : ITypeConverter<string, int>
{
    public int Convert(string source, int destination, ResolutionContext context)
    {
        return int.TryParse(source, out var result) ? result : 0;
    }
}
```

AM030 is analyzer-only; it does not offer a speculative rewrite for invalid converter implementations.

#### Configuration

```ini
dotnet_diagnostic.AM030.severity = error
```

### AM032: Type Converter Null Handling

**Severity**: Warning
**Category**: AutoMapper.Converters

#### Description

Reports nullable-source converters whose `Convert` implementation does not visibly guard or handle the source
parameter before using it. Detection recognizes `== null`/`!= null`, null patterns,
`string.IsNullOrEmpty`/`IsNullOrWhiteSpace`, null-coalescing, conditional access, and modern guard clauses such as
`ArgumentNullException.ThrowIfNull(source)`, `ArgumentException.ThrowIfNullOrEmpty(source)`, and
`ArgumentException.ThrowIfNullOrWhiteSpace(source)`. Guard calls whose first argument is unrelated to the source
parameter still report.

#### Problem

```csharp
public class StringToIntConverter : ITypeConverter<string?, int>
{
    public int Convert(string? source, int destination, ResolutionContext context)
    {
        // ❌ No null check - will throw if source is null
        return int.Parse(source);
    }
}

CreateMap<string?, int>()
    .ConvertUsing<StringToIntConverter>();
// ⚠️ AM032: Converter doesn't handle null values
```

#### Solution

Add explicit null handling:

```csharp
public class StringToIntConverter : ITypeConverter<string?, int>
{
    public int Convert(string? source, int destination, ResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(source))
            return 0;

        return int.TryParse(source, out var result) ? result : 0;
    }
}
```

#### Code Fix

For nullable-source converters, AM032 offers an executable fix that inserts:

```csharp
if (source == null) throw new global::System.ArgumentNullException(nameof(source));
```

The generated guard is fully qualified so the fixer does not add, reorder, or duplicate `using System` directives.

#### Configuration

```ini
dotnet_diagnostic.AM032.severity = warning
```

### AM033: Unused Type Converter

**Severity**: Info
**Category**: AutoMapper.Converters

#### Description

Reports concrete `ITypeConverter<TSource, TDestination>` implementations that are declared but not referenced by any
supported AutoMapper converter configuration. Usage analysis treats generic, instance, and type-based converter
configuration as usage, including `ConvertUsing<MyConverter>()`, `ConvertUsing(new MyConverter())`, and
`ConvertUsing(typeof(MyConverter))`. It also recognizes simple local, field, or property initializers where an
`ITypeConverter<TSource, TDestination>` variable is initialized with a concrete converter and then passed to
`ConvertUsing(converter)`.

When any `ConvertUsing(...)` argument resolves to the interface `ITypeConverter<TSource, TDestination>` itself, for
example through constructor injection (`public TestProfile(ITypeConverter<string, DateTime> converter)`), a
service-locator resolution call, or another DI shape whose concrete implementation cannot be statically traced, every
declared concrete implementation of that interface pair is treated as in use.

#### Problem

```csharp
public class LegacyConverter : ITypeConverter<string, DateTime>
{
    public DateTime Convert(string source, DateTime destination, ResolutionContext context)
        => DateTime.Parse(source);
}

// Converter declared but never used in ConvertUsing
// ℹ️ AM033: Type converter 'LegacyConverter' is defined but not used
```

No diagnostic is reported when the converter is referenced through a supported AutoMapper converter overload:

```csharp
CreateMap<string, DateTime>()
    .ConvertUsing(typeof(LegacyConverter)); // ✅ counted as usage

ITypeConverter<string, DateTime> converter = new LegacyConverter();
CreateMap<string, DateTime>()
    .ConvertUsing(converter); // ✅ counted as usage
```

#### Solution

Remove the unused converter, or register it through the appropriate `ConvertUsing(...)` overload. AM033 is
analyzer-only; it does not offer a speculative rewrite.

#### Configuration

```ini
dotnet_diagnostic.AM033.severity = info
```

---

## Performance Rules

### AM031: Performance Warning

**Severity**: Warning 🟡 / Info 🔵
**Category**: AutoMapper.Performance

#### Description

Detects expensive operations inside mapping expressions that should be performed before mapping.
AM031 analyzes both `ForMember(... MapFrom(...))` and `ForPath(... MapFrom(...))`; nested destination paths are reported as paths such as `Stats.Total`. Multiple-enumeration tracking covers the commonly used terminal LINQ operators — `ToList`, `ToArray`, `ToHashSet`, `ToDictionary`, `ToLookup`, `Sum`, `Average`, `Min`, `Max`, `Aggregate`, `Count`, `LongCount`, `First`/`FirstOrDefault`, `Last`/`LastOrDefault`, `Single`/`SingleOrDefault`, `Any`, `All`, `Contains`, `ElementAt`/`ElementAtOrDefault`, and `SequenceEqual`. Static `Enumerable`/`Queryable` terminal calls are keyed to their source sequence arguments instead of the `Enumerable`/`Queryable` type name. Lazy/intermediate operators such as `Where`, `Select`, `OrderBy`, `GroupBy`, and `Distinct` intentionally do not count toward the enumeration count.

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

#### Problem 4: Sync-over-async Deadlock Risk

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Data, opt => opt.MapFrom(src =>
        _service.GetDataAsync(src.Id).Result));  // ❌ Synchronous access
// ⚠️ AM031: Synchronously waiting on async work can cause deadlocks
```

Task-valued source members are also detected:

```csharp
CreateMap<Source, Destination>()
    .ForMember(dest => dest.Data, opt => opt.MapFrom(src =>
        src.DataTask.Result));  // ❌ Synchronous access
```

`Task.Wait()` and `GetAwaiter().GetResult()` are reported through the same descriptor.

#### Solutions

**Code Fix 1: Cache Collection Enumeration**

AM031 offers this executable rewrite only for `ForMember` mappings when the repeated enumeration is rooted in the source mapping parameter, including nested source paths such as `src.Customer.Orders`. Captured fields, injected services, other closure values, and `ForPath` diagnostics stay analyzer-only because the analyzer cannot safely decide where those values should be cached or because the generated statement lambda would not compile for expression-tree `ForPath.MapFrom`.

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

**Code Fix 3: Remove Redundant ForMember (when convention mapping is equivalent)**

If source/destination have compatible same-name members and the existing mapping is already the direct convention shape
(`MapFrom(src => src.Member)`), AM031 can remove the redundant `ForMember(...)` and let AutoMapper convention mapping
apply. Transforms such as `src.Score + 1`, captured values, service calls, and other non-equivalent expressions keep the
manual-review action only so the fixer does not change runtime mapping policy.

#### Detected Patterns

- ✅ Database queries (EF Core, Dapper, SQL)
- ✅ File I/O operations
- ✅ HTTP/API calls
- ✅ Reflection operations
- ✅ Multiple collection enumerations
- ✅ `DateTime.Now`, `Random`, `Guid.NewGuid()`
- ✅ `Task.Result`, `Task.Wait()`, `GetAwaiter().GetResult()`, including Task-valued source properties
- ✅ Complex LINQ (SelectMany chains)

#### Configuration

```ini
dotnet_diagnostic.AM031.severity = warning

# Specific sub-rules
dotnet_diagnostic.AM031.001.severity = error  # Expensive operations
dotnet_diagnostic.AM031.002.severity = warning  # Multiple enumerations
dotnet_diagnostic.AM031.003.severity = error  # Sync-over-async deadlock
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

The AM041 code fix is intentionally withheld when a duplicate `ReverseMap()` has reverse-side configuration chained after it, such as `.ReverseMap().ForMember(...)` or `(CreateMap<...>().ReverseMap()).ForMember(...)`. Removing that chain automatically could move or drop mapping policy, so those cases require manual review.

The same safety boundary applies to duplicate `CreateMap<TSource, TDestination>()` registrations that carry chained mapping configuration, such as `CreateMap<S, D>().ForMember(...)`, `(CreateMap<S, D>()).ForPath(...)`, or `CreateMap<S, D>().ReverseMap().ForMember(...)`. The fix is withheld because removing the duplicate statement would silently drop the chained policy (for example a `.ForMember(d => d.X, opt => opt.Ignore())` override). The bare `CreateMap<S, D>().ReverseMap()` reversal stays on the safe automatic swap.

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

AM050 only reports when it can prove the source and destination members have the same type, including string-based destination member names such as `ForMember("Name", ...)` and top-level `ForPath(dest => dest.Name, ...)` mappings. Nested `ForPath` destination paths stay outside this cleanup rule because convention mapping equivalence is not guaranteed. It stays quiet when the destination member cannot be resolved or the same-name members have different types. Both source and destination lambda arguments accept simple, parenthesized, and typed shapes — `s => s.Name`, `(s) => s.Name`, and `(Source s) => s.Name` are all recognised. Multi-parameter parenthesized lambdas (such as AutoMapper's `(src, ctx) => ...` `IMemberConfigurationExpression` overload) intentionally stay outside the analyzer's scope.

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

Top-level redundant `ForPath(dest => dest.Name, opt => opt.MapFrom(src => src.Name))` configurations receive the same removal action. Nested paths keep no automatic cleanup action.

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

# Custom Conversions
dotnet_diagnostic.AM030.severity = error
dotnet_diagnostic.AM032.severity = warning
dotnet_diagnostic.AM033.severity = info

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
   <PackageReference Include="AutoMapperAnalyzer.Analyzers" Version="2.30.52">
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

**Last Updated**: 2026-05-15
**Version**: 2.30.52
**Maintainer**: George Wall
