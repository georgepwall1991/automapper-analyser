## Release 2.30.8

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
AM001 | AutoMapper.TypeSafety | Error | Property type mismatch in AutoMapper configuration
AM002 | AutoMapper.NullSafety | Error | Nullable to non-nullable mapping issue in AutoMapper configuration
AM003 | AutoMapper.Collections | Error | Collection type incompatibility in AutoMapper configuration
AM004 | AutoMapper.MissingProperty | Warning | Source property has no corresponding destination property
AM005 | AutoMapper.PropertyMapping | Warning | Property names differ only in casing
AM006 | AutoMapper.DataIntegrity | Info | Destination property is not mapped
AM011 | AutoMapper.RequiredProperties | Error | Required destination property is not mapped from source
AM020 | AutoMapper.NestedObjects | Warning | Nested object mapping configuration missing
AM021 | AutoMapper.Collections | Warning | Collection element type incompatibility
AM022 | AutoMapper.Recursion | Warning | Infinite recursion risk in AutoMapper configuration
AM030 | AutoMapper.Converters | Error | Invalid type converter implementation
AM031 | AutoMapper.Performance | Warning | Expensive operation in mapping expression
AM041 | AutoMapper.Configuration | Warning | Duplicate mapping registration
AM050 | AutoMapper.Configuration | Info | Redundant MapFrom configuration

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
