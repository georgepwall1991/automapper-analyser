# AutoMapper Analyzer Diagnostic Report

**Generated:** 11/19/2025 08:29:12
**Total Issues Found:** 81

## Analyzer Coverage Status
| Analyzer | Name | Issues Found |
|----------|------|--------------|
| AM001 | Property type mismatch in AutoMapper configuration | 6 |
| AM002 | Nullable to non-nullable mapping issue in AutoMapper configuration | 2 |
| AM002 | Non-nullable to nullable mapping in AutoMapper configuration | 2 |
| AM003 | Collection type incompatibility in AutoMapper configuration | 7 |
| AM003 | Collection element type incompatibility in AutoMapper configuration | 7 |
| AM004 | Source property has no corresponding destination property | 3 |
| AM005 | Property names differ only in casing | 3 |
| AM006 | Destination property is not mapped | 3 |
| AM011 | Required destination property is not mapped from source | 1 |
| AM020 | Nested object mapping configuration missing | 2 |
| AM021 | Collection element type incompatibility in AutoMapper configuration | 2 |
| AM022 | Infinite recursion risk in AutoMapper configuration | 7 |
| AM022 | Self-referencing type in AutoMapper configuration | 7 |
| AM030 | Invalid type converter implementation | 12 |
| AM030 | Missing ConvertUsing configuration for type converter | 12 |
| AM030 | Type converter may not handle null values properly | 12 |
| AM030 | Type converter is defined but not used in mapping configuration | 12 |
| AM031 | Expensive operation in mapping expression | 7 |
| AM031 | Multiple enumeration of collection in mapping | 7 |
| AM031 | Expensive computation in mapping expression | 7 |
| AM031 | Synchronous access of async operation in mapping | 7 |
| AM031 | Complex LINQ operation in mapping | 7 |
| AM031 | Non-deterministic operation in mapping | 7 |
| AM041 | Duplicate mapping registration | 24 |
| AM050 | Redundant MapFrom configuration | 2 |

## Detailed Findings

### AM001: Property type mismatch in AutoMapper configuration

- **TypeConverterExamples.cs:19**: Property 'BirthDate' has incompatible types: PersonWithStringDate.BirthDate (string) cannot be mapped to PersonWithDateTime.BirthDate (System.DateTime) without explicit conversion
- **TypeConverterExamples.cs:50**: Property 'Status' has incompatible types: OrderWithStringStatus.Status (string) cannot be mapped to OrderWithEnumStatus.Status (AutoMapperAnalyzer.Samples.Conversions.StatusEnum) without explicit conversion
- **TypeConverterExamples.cs:237**: Property 'BirthDate' has incompatible types: PersonWithStringDate.BirthDate (string) cannot be mapped to PersonWithDateTime.BirthDate (System.DateTime) without explicit conversion
- **SampleUsage.cs:115**: Property 'Price' has incompatible types: ProductSource.Price (string) cannot be mapped to ProductDestination.Price (decimal) without explicit conversion
- **SampleUsage.cs:115**: Property 'CreatedDate' has incompatible types: ProductSource.CreatedDate (string) cannot be mapped to ProductDestination.CreatedDate (System.DateTime) without explicit conversion
- **TypeSafetyExamples.cs:19**: Property 'Age' has incompatible types: PersonWithStringAge.Age (string) cannot be mapped to PersonWithIntAge.Age (int) without explicit conversion

### AM002: Nullable to non-nullable mapping issue in AutoMapper configuration

- **SampleUsage.cs:115**: Property 'Description' has nullable compatibility issue: ProductSource.Description (string?) can be null but ProductDestination.Description (string) is non-nullable
- **TypeSafetyExamples.cs:44**: Property 'Name' has nullable compatibility issue: PersonWithNullableName.Name (string?) can be null but PersonWithRequiredName.Name (string) is non-nullable

### AM003: Collection element type incompatibility in AutoMapper configuration

- **ComplexTypeMappingExamples.cs:20**: Property 'Items' has incompatible collection element types: SourceWithItems.Items (AutoMapperAnalyzer.Samples.ComplexTypes.SourceItem) elements cannot be mapped to DestWithItems.Items (AutoMapperAnalyzer.Samples.ComplexTypes.DestItem) elements without explicit conversion
- **ComplexTypeMappingExamples.cs:139**: Property 'Items' has incompatible collection element types: SourceWithItems.Items (AutoMapperAnalyzer.Samples.ComplexTypes.SourceItem) elements cannot be mapped to DestWithItems.Items (AutoMapperAnalyzer.Samples.ComplexTypes.DestItem) elements without explicit conversion
- **SampleUsage.cs:99**: Property 'PhoneNumbers' has incompatible collection element types: PersonSource.PhoneNumbers (string) elements cannot be mapped to PersonDestination.PhoneNumbers (int) elements without explicit conversion
- **SampleUsage.cs:99**: Property 'Tags' has incompatible collection types: PersonSource.Tags (string[]) cannot be mapped to PersonDestination.Tags (System.Collections.Generic.HashSet<string>) without explicit collection conversion
- **SampleUsage.cs:102**: Property 'Children' has incompatible collection element types: TreeNode.Children (AutoMapperAnalyzer.Samples.TreeNode) elements cannot be mapped to TreeNodeDto.Children (AutoMapperAnalyzer.Samples.TreeNodeDto) elements without explicit conversion
- **SampleUsage.cs:108**: Property 'Tags' has incompatible collection types: PersonSource.Tags (string[]) cannot be mapped to PersonDestination.Tags (System.Collections.Generic.HashSet<string>) without explicit collection conversion
- **TypeSafetyExamples.cs:70**: Property 'Tags' has incompatible collection types: ArticleWithStringTags.Tags (System.Collections.Generic.List<string>) cannot be mapped to ArticleWithIntTags.Tags (System.Collections.Generic.HashSet<int>) without explicit collection conversion

### AM004: Source property has no corresponding destination property

- **ConfigurationExamples.cs:22**: Source property 'FirstName' will not be mapped - potential data loss
- **ConfigurationExamples.cs:22**: Source property 'LastName' will not be mapped - potential data loss
- **MissingPropertyExamples.cs:19**: Source property 'ImportantData' will not be mapped - potential data loss

### AM005: Property names differ only in casing

- **MissingPropertyExamples.cs:72**: Property 'firstName' in source differs only in casing from destination property 'FirstName' - consider explicit mapping or case-insensitive configuration
- **MissingPropertyExamples.cs:72**: Property 'lastName' in source differs only in casing from destination property 'LastName' - consider explicit mapping or case-insensitive configuration
- **MissingPropertyExamples.cs:72**: Property 'userName' in source differs only in casing from destination property 'UserName' - consider explicit mapping or case-insensitive configuration

### AM006: Destination property is not mapped

- **ConfigurationExamples.cs:22**: Destination property 'FullName' is not mapped from source 'User'
- **MissingPropertyExamples.cs:45**: Destination property 'RequiredField' is not mapped from source 'SourceWithoutRequired'
- **UnmappedDestinationExamples.cs:22**: Destination property 'ExtraProperty' is not mapped from source 'Source'

### AM011: Required destination property is not mapped from source

- **MissingPropertyExamples.cs:45**: Required property 'RequiredField' in destination is not mapped from any source property and will cause a runtime exception

### AM020: Nested object mapping configuration missing

- **CodeFixDemo.cs:36**: Property 'WorkLocation' requires mapping configuration between 'Location' and 'LocationDto'. Consider adding CreateMap<Location, LocationDto>() or explicit ForMember configuration.
- **SampleUsage.cs:105**: Property 'Headquarters' requires mapping configuration between 'Address' and 'AddressDto'. Consider adding CreateMap<Address, AddressDto>() or explicit ForMember configuration.

### AM021: Collection element type incompatibility in AutoMapper configuration

- **SampleUsage.cs:99**: Property 'PhoneNumbers' has incompatible collection element types: PersonSource.PhoneNumbers (string) elements cannot be mapped to PersonDestination.PhoneNumbers (int) elements without explicit conversion
- **TypeSafetyExamples.cs:70**: Property 'Tags' has incompatible collection element types: ArticleWithStringTags.Tags (string) elements cannot be mapped to ArticleWithIntTags.Tags (int) elements without explicit conversion

### AM022: Infinite recursion risk in AutoMapper configuration

- **ComplexTypeMappingExamples.cs:55**: Potential infinite recursion detected: Parent to ParentDto mapping may cause stack overflow due to circular references
- **ComplexTypeMappingExamples.cs:56**: Potential infinite recursion detected: Child to ChildDto mapping may cause stack overflow due to circular references
- **ComplexTypeMappingExamples.cs:166**: Potential infinite recursion detected: Parent to ParentDto mapping may cause stack overflow due to circular references
- **ComplexTypeMappingExamples.cs:167**: Potential infinite recursion detected: Child to ChildDto mapping may cause stack overflow due to circular references
- **ComplexTypeMappingExamples.cs:186**: Potential infinite recursion detected: Parent to ParentDto mapping may cause stack overflow due to circular references
- **ComplexTypeMappingExamples.cs:188**: Potential infinite recursion detected: Child to ChildDto mapping may cause stack overflow due to circular references
- **SampleUsage.cs:102**: Self-referencing type detected: TreeNode contains properties of its own type, which may cause infinite recursion

### AM030: Missing ConvertUsing configuration for type converter

- **CodeFixDemo.cs:36**: Property 'WorkLocation' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<Location, LocationDto>> or ConvertUsing(converter => ...).
- **ComplexTypeMappingExamples.cs:20**: Property 'Items' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<List, List>> or ConvertUsing(converter => ...).
- **ComplexTypeMappingExamples.cs:139**: Property 'Items' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<List, List>> or ConvertUsing(converter => ...).
- **TypeConverterExamples.cs:19**: Property 'BirthDate' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<String, DateTime>> or ConvertUsing(converter => ...).
- **SampleUsage.cs:75**: Converter for 'UnsafeStringToDateTimeConverter' may not handle null values. Source type 'String' is nullable but converter doesn't check for null.
- **SampleUsage.cs:99**: Property 'PhoneNumbers' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<List, List>> or ConvertUsing(converter => ...).
- **SampleUsage.cs:102**: Property 'Children' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<List, List>> or ConvertUsing(converter => ...).
- **SampleUsage.cs:105**: Property 'Headquarters' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<Address, AddressDto>> or ConvertUsing(converter => ...).
- **SampleUsage.cs:115**: Property 'Price' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<String, Decimal>> or ConvertUsing(converter => ...).
- **SampleUsage.cs:115**: Property 'CreatedDate' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<String, DateTime>> or ConvertUsing(converter => ...).
- **TypeSafetyExamples.cs:19**: Property 'Age' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<String, Int32>> or ConvertUsing(converter => ...).
- **TypeSafetyExamples.cs:70**: Property 'Tags' has incompatible types but no ConvertUsing configuration. Consider using ConvertUsing<ITypeConverter<List, HashSet>> or ConvertUsing(converter => ...).

### AM031: Expensive operation in mapping expression

- **AM031_PerformanceExamples.cs:75**: Property 'Content' mapping contains file I/O operation that should be performed before mapping to avoid performance issues
- **AM031_PerformanceExamples.cs:75**: Property 'Content' mapping contains file I/O operation that should be performed before mapping to avoid performance issues
- **AM031_PerformanceExamples.cs:95**: Property 'TotalWithAverage' mapping enumerates collection 'Numbers' multiple times. Consider caching the result with ToList() or ToArray().
- **AM031_PerformanceExamples.cs:150**: Property 'DaysOld' mapping uses DateTime.Now which produces non-deterministic results. Consider computing before mapping for testability.
- **AM031_PerformanceExamples.cs:175**: Property 'TypeName' mapping contains reflection operation that should be performed before mapping to avoid performance issues
- **AM031_PerformanceExamples.cs:197**: Property 'ApiResponse' mapping contains HTTP request that should be performed before mapping to avoid performance issues
- **AM031_PerformanceExamples.cs:217**: Property 'FilteredCount' mapping contains complex LINQ operation that may impact performance. Consider simplifying or computing before mapping.

### AM041: Duplicate mapping registration

- **ComplexTypeMappingExamples.cs:139**: Mapping from 'SourceWithItems' to 'DestWithItems' is already registered
- **ComplexTypeMappingExamples.cs:166**: Mapping from 'Parent' to 'ParentDto' is already registered
- **ComplexTypeMappingExamples.cs:167**: Mapping from 'Child' to 'ChildDto' is already registered
- **ComplexTypeMappingExamples.cs:186**: Mapping from 'Parent' to 'ParentDto' is already registered
- **ComplexTypeMappingExamples.cs:188**: Mapping from 'Child' to 'ChildDto' is already registered
- **AM041_DuplicateMappingExamples.cs:21**: Mapping from 'Source' to 'Destination' is already registered
- **ConfigurationExamples.cs:145**: Mapping from 'User' to 'UserDto' is already registered
- **ConfigurationExamples.cs:174**: Mapping from 'Product' to 'ProductDto' is already registered
- **ConfigurationExamples.cs:191**: Mapping from 'Order' to 'OrderDto' is already registered
- **TypeConverterExamples.cs:162**: Mapping from 'PersonWithStringDate' to 'PersonWithDateTime' is already registered
- **TypeConverterExamples.cs:187**: Mapping from 'OrderWithStringStatus' to 'OrderWithEnumStatus' is already registered
- **TypeConverterExamples.cs:211**: Mapping from 'SourceWithNullableString' to 'DestWithGuid' is already registered
- **TypeConverterExamples.cs:237**: Mapping from 'PersonWithStringDate' to 'PersonWithDateTime' is already registered
- **MissingPropertyExamples.cs:148**: Mapping from 'SourceWithExtraData' to 'DestinationMissingData' is already registered
- **MissingPropertyExamples.cs:172**: Mapping from 'SourceWithoutRequired' to 'DestinationWithRequired' is already registered
- **MissingPropertyExamples.cs:190**: Mapping from 'SourceWithCamelCase' to 'DestinationWithPascalCase' is already registered
- **PerformanceExamples.cs:76**: Mapping from 'Customer' to 'CustomerDto' is already registered
- **PerformanceExamples.cs:167**: Mapping from 'Company' to 'CompanyDto' is already registered
- **PerformanceExamples.cs:188**: Mapping from 'Customer' to 'CustomerDto' is already registered
- **PerformanceExamples.cs:191**: Mapping from 'Employee' to 'EmployeeDto' is already registered
- **SampleUsage.cs:108**: Mapping from 'PersonSource' to 'PersonDestination' is already registered
- **SampleUsage.cs:118**: Mapping from 'ProductSource' to 'ProductDestination' is already registered
- **TypeSafetyExamples.cs:143**: Mapping from 'PersonWithStringAge' to 'PersonWithIntAge' is already registered
- **TypeSafetyExamples.cs:165**: Mapping from 'PersonWithNullableName' to 'PersonWithRequiredName' is already registered

### AM050: Redundant MapFrom configuration

- **ComplexTypeMappingExamples.cs:187**: Explicit mapping for 'Child' is redundant because the property name matches the source
- **AM050_RedundantMapFromExamples.cs:23**: Explicit mapping for 'Name' is redundant because the property name matches the source

