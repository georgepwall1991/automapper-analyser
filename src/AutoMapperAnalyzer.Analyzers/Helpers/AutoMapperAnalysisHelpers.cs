using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapperAnalyzer.Analyzers.Helpers
{
    /// <summary>
    /// Provides common helper methods for AutoMapper-related code analysis.
    /// </summary>
    public static class AutoMapperAnalysisHelpers
    {
        /// <summary>
        /// Determines whether the given invocation is a CreateMap method call from AutoMapper.
        /// </summary>
        /// <param name="invocation">The invocation expression to check.</param>
        /// <param name="semanticModel">The semantic model for symbol analysis.</param>
        /// <returns>True if the invocation is a CreateMap call; otherwise, false.</returns>
        public static bool IsCreateMapInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            if (invocation == null || semanticModel == null)
                return false;

            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                return false;

            // Check if it's a CreateMap method
            if (methodSymbol.Name != "CreateMap")
                return false;

            // Check if it's from AutoMapper namespace
            var containingNamespace = methodSymbol.ContainingNamespace?.ToDisplayString();
            if (containingNamespace == "AutoMapper")
                return true;

            // Check if the containing type extends AutoMapper.Profile
            var containingType = methodSymbol.ContainingType;
            while (containingType != null)
            {
                if (containingType.ToDisplayString() == "AutoMapper.Profile" ||
                    containingType.Name == "Profile" ||
                    containingType.Name.Contains("MappingProfile") ||
                    containingType.Name.Contains("MapperConfiguration"))
                {
                    return true;
                }
                containingType = containingType.BaseType;
            }

            // Fallback: Check syntax pattern for common AutoMapper usage
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var expressionText = memberAccess.Expression.ToString();
                if (expressionText.Contains("mapper") || 
                    expressionText.Contains("Mapper") ||
                    expressionText.Contains("cfg") ||
                    expressionText.Contains("config"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the source and destination type arguments from a CreateMap invocation.
        /// </summary>
        /// <param name="invocation">The CreateMap invocation expression.</param>
        /// <param name="semanticModel">The semantic model for type resolution.</param>
        /// <returns>A tuple containing the source and destination types, or null if not found.</returns>
        public static (ITypeSymbol? sourceType, ITypeSymbol? destType) GetCreateMapTypeArguments(
            InvocationExpressionSyntax invocation, 
            SemanticModel semanticModel)
        {
            if (invocation == null || semanticModel == null)
                return (null, null);

            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                return (null, null);

            // Try to get type arguments from the generic method
            if (methodSymbol.TypeArguments.Length == 2)
            {
                return (methodSymbol.TypeArguments[0], methodSymbol.TypeArguments[1]);
            }

            // Fallback: Try to parse from syntax
            if (invocation.Expression is GenericNameSyntax genericName &&
                genericName.TypeArgumentList?.Arguments.Count == 2)
            {
                var sourceTypeInfo = semanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]);
                var destTypeInfo = semanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[1]);
                return (sourceTypeInfo.Type, destTypeInfo.Type);
            }

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax genericMemberName &&
                genericMemberName.TypeArgumentList?.Arguments.Count == 2)
            {
                var sourceTypeInfo = semanticModel.GetTypeInfo(genericMemberName.TypeArgumentList.Arguments[0]);
                var destTypeInfo = semanticModel.GetTypeInfo(genericMemberName.TypeArgumentList.Arguments[1]);
                return (sourceTypeInfo.Type, destTypeInfo.Type);
            }

            return (null, null);
        }

        /// <summary>
        /// Gets all public properties from a type that can be mapped by AutoMapper.
        /// </summary>
        /// <param name="typeSymbol">The type to analyze.</param>
        /// <param name="requireGetter">When true, only include properties with an accessible getter.</param>
        /// <param name="requireSetter">When true, only include properties with an accessible setter (set or init).</param>
        /// <returns>A collection of mappable properties.</returns>
        public static IEnumerable<IPropertySymbol> GetMappableProperties(
            ITypeSymbol? typeSymbol,
            bool requireGetter = true,
            bool requireSetter = true)
        {
            if (typeSymbol == null)
                return Enumerable.Empty<IPropertySymbol>();

            var properties = new List<IPropertySymbol>();
            var currentType = typeSymbol;

            while (currentType != null)
            {
                foreach (var member in currentType.GetMembers())
                {
                    if (member is IPropertySymbol property &&
                        (property.DeclaredAccessibility == Accessibility.Public || property.DeclaredAccessibility == Accessibility.Internal) &&
                        !property.IsStatic &&
                        !property.IsIndexer &&
                        (!requireGetter || property.GetMethod != null) &&
                        (!requireSetter || property.SetMethod != null))
                    {
                        // Only include if not already in the list (handles property hiding)
                        if (!properties.Any(p => p.Name == property.Name))
                        {
                            properties.Add(property);
                        }
                    }
                }

                // Don't traverse up from object
                if (currentType.SpecialType == SpecialType.System_Object)
                    break;

                currentType = currentType.BaseType;
            }

            return properties;
        }

        /// <summary>
        /// Checks if a CreateMap configuration already exists for the given types in the compilation.
        /// </summary>
        /// <param name="compilation">The compilation to search.</param>
        /// <param name="sourceType">The source type.</param>
        /// <param name="destType">The destination type.</param>
        /// <returns>True if a CreateMap configuration exists; otherwise, false.</returns>
        public static bool HasExistingCreateMapForTypes(
            Compilation compilation,
            ITypeSymbol sourceType,
            ITypeSymbol destType)
        {
            if (compilation == null || sourceType == null || destType == null)
                return false;

            var registry = CreateMapRegistry.FromCompilation(compilation);
            return registry.Contains(sourceType, destType);
        }

        /// <summary>
        /// Determines whether two types are compatible for AutoMapper mapping.
        /// </summary>
        /// <param name="sourceType">The source property type.</param>
        /// <param name="destType">The destination property type.</param>
        /// <returns>True if the types are compatible; otherwise, false.</returns>
        public static bool AreTypesCompatible(ITypeSymbol? sourceType, ITypeSymbol? destType)
        {
            if (sourceType == null || destType == null)
                return false;

            // Same type
            if (SymbolEqualityComparer.Default.Equals(sourceType, destType))
                return true;

            // Check for basic type conversions (simplified check)
            // Note: Full conversion checking would require access to the compilation,
            // which is not available in this context

            // Check for nullable compatibility
            if (IsNullableType(destType, out var destUnderlyingType) && 
                SymbolEqualityComparer.Default.Equals(sourceType, destUnderlyingType))
            {
                return true;
            }

            // Check for collection compatibility
            if (IsCollectionType(sourceType) && IsCollectionType(destType))
            {
                var sourceElement = GetCollectionElementType(sourceType);
                var destElement = GetCollectionElementType(destType);
                return AreTypesCompatible(sourceElement, destElement);
            }

            // Check for numeric type compatibility
            if (AreNumericTypesCompatible(sourceType, destType))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a type is a nullable value type or reference type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="underlyingType">The underlying type if nullable.</param>
        /// <returns>True if the type is nullable; otherwise, false.</returns>
        public static bool IsNullableType(ITypeSymbol type, out ITypeSymbol? underlyingType)
        {
            underlyingType = null;
            
            if (type == null)
                return false;

            // Check for Nullable<T>
            if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
                {
                    underlyingType = namedType.TypeArguments[0];
                    return true;
                }
            }

            // Reference types are inherently nullable
            if (type.IsReferenceType)
            {
                underlyingType = type;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a type is a collection type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is a collection; otherwise, false.</returns>
        public static bool IsCollectionType(ITypeSymbol? type)
        {
            if (type == null)
                return false;

            // Check for array
            if (type is IArrayTypeSymbol)
                return true;

            // Check for IEnumerable<T> or derived interfaces
            if (type is INamedTypeSymbol namedType)
            {
                var enumerableInterface = namedType.AllInterfaces
                    .FirstOrDefault(i => i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
                
                if (enumerableInterface != null)
                    return true;

                // Check if the type itself is IEnumerable<T>
                if (namedType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>" ||
                    namedType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IList<T>" ||
                    namedType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.ICollection<T>")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the element type of a collection.
        /// </summary>
        /// <param name="collectionType">The collection type.</param>
        /// <returns>The element type, or null if not a collection.</returns>
        public static ITypeSymbol? GetCollectionElementType(ITypeSymbol? collectionType)
        {
            if (collectionType == null)
                return null;

            // Handle arrays
            if (collectionType is IArrayTypeSymbol arrayType)
                return arrayType.ElementType;

            // Handle generic collections
            if (collectionType is INamedTypeSymbol namedType)
            {
                // Direct generic collection
                if (namedType.TypeArguments.Length > 0)
                {
                    var genericTypeDef = namedType.OriginalDefinition.ToDisplayString();
                    if (genericTypeDef.StartsWith("System.Collections.Generic.IEnumerable") ||
                        genericTypeDef.StartsWith("System.Collections.Generic.IList") ||
                        genericTypeDef.StartsWith("System.Collections.Generic.ICollection") ||
                        genericTypeDef.StartsWith("System.Collections.Generic.List") ||
                        genericTypeDef.StartsWith("System.Collections.Generic.HashSet"))
                    {
                        return namedType.TypeArguments[0];
                    }
                }

                // Check implemented interfaces
                var enumerableInterface = namedType.AllInterfaces
                    .FirstOrDefault(i => i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
                
                if (enumerableInterface?.TypeArguments.Length > 0)
                    return enumerableInterface.TypeArguments[0];
            }

            return null;
        }

        /// <summary>
        /// Gets all ForMember calls from a CreateMap invocation chain.
        /// </summary>
        /// <param name="createMapInvocation">The CreateMap invocation.</param>
        /// <returns>A collection of ForMember invocations.</returns>
        public static IEnumerable<InvocationExpressionSyntax> GetForMemberCalls(InvocationExpressionSyntax createMapInvocation)
        {
            if (createMapInvocation == null)
                return Enumerable.Empty<InvocationExpressionSyntax>();

            var forMemberCalls = new List<InvocationExpressionSyntax>();
            var currentNode = createMapInvocation.Parent;

            while (currentNode is MemberAccessExpressionSyntax memberAccess &&
                   memberAccess.Parent is InvocationExpressionSyntax invocation)
            {
                if (memberAccess.Name.Identifier.Text == "ForMember")
                {
                    forMemberCalls.Add(invocation);
                }
                currentNode = invocation.Parent;
            }

            return forMemberCalls;
        }

        /// <summary>
        /// Checks if a property is configured with ForMember in the mapping configuration.
        /// </summary>
        /// <param name="createMapInvocation">The CreateMap invocation.</param>
        /// <param name="propertyName">The property name to check.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <returns>True if the property has a ForMember configuration; otherwise, false.</returns>
        public static bool IsPropertyConfiguredWithForMember(
            InvocationExpressionSyntax createMapInvocation,
            string propertyName,
            SemanticModel semanticModel)
        {
            var forMemberCalls = GetForMemberCalls(createMapInvocation);

            foreach (var forMember in forMemberCalls)
            {
                if (forMember.ArgumentList?.Arguments.Count > 0)
                {
                    var firstArg = forMember.ArgumentList.Arguments[0].Expression;
                    
                    // Check if it's a lambda expression selecting this property
                    if (firstArg is SimpleLambdaExpressionSyntax lambda &&
                        lambda.Body is MemberAccessExpressionSyntax memberAccess &&
                        memberAccess.Name.Identifier.Text == propertyName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the underlying type, removing nullable wrappers.
        /// </summary>
        public static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
        {
            if (type == null) return null!;

            // Handle nullable reference types (T?)
            if (type is INamedTypeSymbol namedType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return namedType.TypeArguments[0];
            }

            // Handle nullable reference types with annotations
            if (type.CanBeReferencedByName && type.NullableAnnotation == NullableAnnotation.Annotated)
            {
                return type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            }

            return type;
        }

        /// <summary>
        /// Checks if the type is a built-in value type or common reference type that doesn't need mapping.
        /// </summary>
        public static bool IsBuiltInType(ITypeSymbol type)
        {
            if (type == null) return false;

            return type.SpecialType != SpecialType.None ||
                   type.Name == "String" ||
                   type.Name == "DateTime" ||
                   type.Name == "DateTimeOffset" ||
                   type.Name == "TimeSpan" ||
                   type.Name == "Guid" ||
                   type.Name == "Decimal";
        }

        /// <summary>
        /// Gets the type name without nullability annotations.
        /// </summary>
        public static string GetTypeNameWithoutNullability(ITypeSymbol type)
        {
            ITypeSymbol underlyingType = GetUnderlyingType(type);
            return underlyingType.Name;
        }

        /// <summary>
        /// Checks if the mapping chain contains a ReverseMap call.
        /// </summary>
        public static bool HasReverseMap(InvocationExpressionSyntax createMapInvocation)
        {
            return GetReverseMapInvocation(createMapInvocation) != null;
        }

        /// <summary>
        /// Gets the ReverseMap invocation from the chain, if it exists.
        /// </summary>
        public static InvocationExpressionSyntax? GetReverseMapInvocation(InvocationExpressionSyntax createMapInvocation)
        {
            SyntaxNode? current = createMapInvocation;
            
            // Traverse up the fluent chain
            // Structure: Invocation -> MemberAccess -> Invocation -> MemberAccess -> ...
            
            while (current.Parent is MemberAccessExpressionSyntax memberAccess && 
                   memberAccess.Expression == current &&
                   memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
            {
                if (memberAccess.Name.Identifier.Text == "ReverseMap")
                {
                    return parentInvocation;
                }
                current = parentInvocation;
            }
            
            return null;
        }

        /// <summary>
        /// Determines whether two types are compatible numeric types.
        /// </summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="destType">The destination type.</param>
        /// <returns>True if both types are numeric and compatible; otherwise, false.</returns>
        private static bool AreNumericTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destType)
        {
            // Get numeric conversion levels
            int sourceLevel = GetNumericConversionLevel(sourceType.SpecialType);
            int destLevel = GetNumericConversionLevel(destType.SpecialType);

            // If either is not a numeric type, no compatibility
            if (sourceLevel == int.MaxValue || destLevel == int.MaxValue)
                return false;

            // Allow implicit conversions (smaller to larger types)
            return sourceLevel <= destLevel;
        }

        /// <summary>
        /// Gets the numeric conversion level for implicit conversions.
        /// </summary>
        /// <param name="specialType">The special type to check.</param>
        /// <returns>The conversion level, or int.MaxValue if not numeric.</returns>
        private static int GetNumericConversionLevel(SpecialType specialType)
        {
            return specialType switch
            {
                SpecialType.System_Byte => 1,
                SpecialType.System_SByte => 1,
                SpecialType.System_Int16 => 2,
                SpecialType.System_UInt16 => 2,
                SpecialType.System_Int32 => 3,
                SpecialType.System_UInt32 => 3,
                SpecialType.System_Int64 => 4,
                SpecialType.System_UInt64 => 4,
                SpecialType.System_Single => 5,
                SpecialType.System_Double => 6,
                SpecialType.System_Decimal => 7,
                _ => int.MaxValue
            };
        }
    }
}
