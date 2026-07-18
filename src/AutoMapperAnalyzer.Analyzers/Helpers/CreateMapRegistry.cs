using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoMapperAnalyzer.Analyzers.Helpers;

/// <summary>
///     Caches resolved AutoMapper CreateMap registrations for a compilation.
/// </summary>
internal sealed class CreateMapRegistry
{
    private static readonly ConditionalWeakTable<Compilation, CreateMapRegistry> Cache = new();
    private readonly Dictionary<InvocationExpressionSyntax, (string Source, string Dest, Location Location)>
        _duplicates;

    private readonly ImmutableArray<MappingInfo> _mappings;

    private CreateMapRegistry(
        ImmutableArray<MappingInfo> mappings,
        Dictionary<InvocationExpressionSyntax, (string Source, string Dest, Location Location)> duplicates)
    {
        _mappings = mappings;
        _duplicates = duplicates;
    }

    public bool Contains(ITypeSymbol? source, ITypeSymbol? destination)
    {
        if (source == null || destination == null)
        {
            return false;
        }

        foreach (MappingInfo mapping in _mappings)
        {
            if (SymbolEqualityComparer.Default.Equals(mapping.Source, source) &&
                SymbolEqualityComparer.Default.Equals(mapping.Destination, destination))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets the sole explicit forward registration for a mapping direction.
    ///     Reverse-generated and duplicate registrations are deliberately treated as ambiguous.
    /// </summary>
    public bool TryGetUniqueForwardMapping(
        ITypeSymbol? source,
        ITypeSymbol? destination,
        out InvocationExpressionSyntax invocation,
        out SemanticModel semanticModel)
    {
        invocation = null!;
        semanticModel = null!;
        MappingInfo? match = null;

        foreach (MappingInfo mapping in _mappings)
        {
            if (!SymbolEqualityComparer.Default.Equals(mapping.Source, source) ||
                !SymbolEqualityComparer.Default.Equals(mapping.Destination, destination))
            {
                continue;
            }

            if (match != null)
            {
                return false;
            }

            match = mapping;
        }

        if (match is not { IsReverseMap: false } uniqueForwardMapping)
        {
            return false;
        }

        invocation = uniqueForwardMapping.Node;
        semanticModel = uniqueForwardMapping.SemanticModel;
        return true;
    }

    /// <summary>
    ///     Checks whether every registration for a mapping direction explicitly constrains recursive traversal.
    /// </summary>
    public bool IsCycleConstrained(ITypeSymbol? source, ITypeSymbol? destination)
    {
        return IsConstrainedBy(source, destination, mapping => mapping.IsCycleConstrained);
    }

    public bool IsMaxDepthConstrained(ITypeSymbol? source, ITypeSymbol? destination)
    {
        return IsConstrainedBy(source, destination, mapping => mapping.HasMaxDepth);
    }

    public bool IsPreserveReferencesConstrained(ITypeSymbol? source, ITypeSymbol? destination)
    {
        return IsConstrainedBy(source, destination, mapping => mapping.HasPreserveReferences);
    }

    public bool IsConvertUsingConstrained(ITypeSymbol? source, ITypeSymbol? destination)
    {
        return IsConstrainedBy(source, destination, mapping => mapping.HasConvertUsing);
    }

    private bool IsConstrainedBy(
        ITypeSymbol? source,
        ITypeSymbol? destination,
        Func<MappingInfo, bool> predicate)
    {
        if (source == null || destination == null)
        {
            return false;
        }

        bool found = false;
        foreach (MappingInfo mapping in _mappings)
        {
            if (!SymbolEqualityComparer.Default.Equals(mapping.Source, source) ||
                !SymbolEqualityComparer.Default.Equals(mapping.Destination, destination))
            {
                continue;
            }

            found = true;
            if (!predicate(mapping))
            {
                return false;
            }
        }

        return found;
    }

    /// <summary>
    ///     Checks if a CreateMap exists for the element types of collections.
    ///     Unwraps collection types (IEnumerable&lt;T&gt;, List&lt;T&gt;, etc.) to check element mappings.
    /// </summary>
    public bool ContainsElementMapping(ITypeSymbol? sourceCollection, ITypeSymbol? destinationCollection)
    {
        if (sourceCollection == null || destinationCollection == null)
        {
            return false;
        }

        ITypeSymbol? sourceElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(sourceCollection);
        ITypeSymbol? destElementType = AutoMapperAnalysisHelpers.GetCollectionElementType(destinationCollection);

        if (sourceElementType == null || destElementType == null)
        {
            return false;
        }

        // Check if a mapping exists for the element types
        return Contains(sourceElementType, destElementType);
    }

    /// <summary>
    ///     Gets the duplicate mappings identified during registry build.
    /// </summary>
    public IReadOnlyDictionary<InvocationExpressionSyntax, (string Source, string Dest, Location Location)>
        GetDuplicateMappings()
    {
        return _duplicates;
    }

    public static CreateMapRegistry Build(Compilation compilation)
    {
        var mappings = new List<MappingInfo>();

        foreach (SyntaxTree? syntaxTree in compilation.SyntaxTrees)
        {
            // Fast path: Check if the file content contains "CreateMap" before parsing
            if (!syntaxTree.TryGetText(out SourceText? text) || !text.ToString().Contains("CreateMap"))
            {
                continue;
            }

            SyntaxNode root = syntaxTree.GetRoot();
            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

            foreach (InvocationExpressionSyntax? invocation in root.DescendantNodes()
                         .OfType<InvocationExpressionSyntax>())
            {
                if (!AutoMapperAnalysisHelpers.IsCreateMapInvocation(invocation, semanticModel))
                {
                    continue;
                }

                (ITypeSymbol? sourceType, ITypeSymbol? destType) =
                    AutoMapperAnalysisHelpers.GetCreateMapTypeArguments(invocation, semanticModel);
                if (sourceType != null && destType != null)
                {
                    InvocationExpressionSyntax? reverseMapInvocation =
                        AutoMapperAnalysisHelpers.GetReverseMapInvocation(invocation);
                    bool hasMaxDepth = HasCycleBreaker(
                        invocation,
                        reverseMapInvocation,
                        semanticModel,
                        reverseDirection: false,
                        methodName: "MaxDepth");
                    bool hasPreserveReferences = HasCycleBreaker(
                        invocation,
                        reverseMapInvocation,
                        semanticModel,
                        reverseDirection: false,
                        methodName: "PreserveReferences");
                    bool hasConvertUsing = HasCycleBreaker(
                        invocation,
                        reverseMapInvocation,
                        semanticModel,
                        reverseDirection: false,
                        methodName: "ConvertUsing");
                    mappings.Add(new MappingInfo
                    {
                        Source = sourceType,
                        Destination = destType,
                        Location = invocation.GetLocation(),
                        Node = invocation,
                        SemanticModel = semanticModel,
                        IsReverseMap = false,
                        IsCycleConstrained = hasMaxDepth || hasPreserveReferences || hasConvertUsing,
                        HasMaxDepth = hasMaxDepth,
                        HasPreserveReferences = hasPreserveReferences,
                        HasConvertUsing = hasConvertUsing
                    });

                    // Check for ReverseMap()
                    if (reverseMapInvocation != null)
                    {
                        Location loc;
                        if (reverseMapInvocation.Expression is MemberAccessExpressionSyntax ma)
                        {
                            loc = ma.Name.GetLocation();
                        }
                        else
                        {
                            loc = reverseMapInvocation.GetLocation();
                        }

                        bool hasReverseMaxDepth = HasCycleBreaker(
                            invocation,
                            reverseMapInvocation,
                            semanticModel,
                            reverseDirection: true,
                            methodName: "MaxDepth");
                        bool hasReversePreserveReferences = HasCycleBreaker(
                            invocation,
                            reverseMapInvocation,
                            semanticModel,
                            reverseDirection: true,
                            methodName: "PreserveReferences");
                        bool hasReverseConvertUsing = HasCycleBreaker(
                            invocation,
                            reverseMapInvocation,
                            semanticModel,
                            reverseDirection: true,
                            methodName: "ConvertUsing");
                        mappings.Add(new MappingInfo
                        {
                            Source = destType,
                            Destination = sourceType,
                            Location = loc,
                            Node = reverseMapInvocation,
                            SemanticModel = semanticModel,
                            IsReverseMap = true,
                            IsCycleConstrained = hasReverseMaxDepth ||
                                                 hasReversePreserveReferences ||
                                                 hasReverseConvertUsing,
                            HasMaxDepth = hasReverseMaxDepth,
                            HasPreserveReferences = hasReversePreserveReferences,
                            HasConvertUsing = hasReverseConvertUsing
                        });
                    }
                    else
                    {
                        foreach (InvocationExpressionSyntax deferredReverseMap in
                                 GetDeferredReverseMapInvocations(invocation, semanticModel))
                        {
                            var memberAccess = (MemberAccessExpressionSyntax)deferredReverseMap.Expression;
                            Location location = memberAccess.Name.GetLocation();

                            mappings.Add(new MappingInfo
                            {
                                Source = destType,
                                Destination = sourceType,
                                Location = location,
                                Node = deferredReverseMap,
                                SemanticModel = semanticModel,
                                IsReverseMap = true,
                                IsCycleConstrained = false
                            });
                        }
                    }
                }
            }
        }

        // Identify duplicates
        var duplicates = new Dictionary<InvocationExpressionSyntax, (string Source, string Dest, Location Location)>();
        IEnumerable<IGrouping<(ITypeSymbol Source, ITypeSymbol Destination), MappingInfo>> groups =
            mappings.GroupBy(m => (m.Source, m.Destination), new MappingComparer());

        foreach (IGrouping<(ITypeSymbol Source, ITypeSymbol Destination), MappingInfo>? group in groups)
        {
            if (group.Count() > 1)
            {
                // Sort by location to have deterministic reporting
                var sorted = group.OrderBy(x => x.Location.SourceTree?.FilePath)
                    .ThenBy(x => x.Location.SourceSpan.Start)
                    .ToList();

                // Report on all except the first one
                for (int i = 1; i < sorted.Count; i++)
                {
                    MappingInfo duplicate = sorted[i];
                    if (sorted.Take(i).All(previous => AreMutuallyExclusiveByIfElse(previous, duplicate)))
                    {
                        continue;
                    }

                    duplicates[duplicate.Node] = (
                        Source: FormatMappingTypeName(duplicate.Source),
                        Dest: FormatMappingTypeName(duplicate.Destination),
                        duplicate.Location
                    );
                }
            }
        }

        return new CreateMapRegistry(mappings.ToImmutableArray(), duplicates);
    }

    private static bool AreMutuallyExclusiveByIfElse(MappingInfo first, MappingInfo second)
    {
        SyntaxNode? firstBoundary = GetContainingExecutableBoundary(first.Node);
        SyntaxNode? secondBoundary = GetContainingExecutableBoundary(second.Node);
        if (firstBoundary == null || !ReferenceEquals(firstBoundary, secondBoundary))
        {
            return false;
        }

        foreach (IfStatementSyntax ifStatement in first.Node.Ancestors().OfType<IfStatementSyntax>())
        {
            if (ifStatement.Else is not { Statement: StatementSyntax elseStatement } ||
                !ifStatement.Span.Contains(second.Node.Span))
            {
                continue;
            }

            bool firstInThen = ifStatement.Statement.Span.Contains(first.Node.Span);
            bool firstInElse = elseStatement.Span.Contains(first.Node.Span);
            bool secondInThen = ifStatement.Statement.Span.Contains(second.Node.Span);
            bool secondInElse = elseStatement.Span.Contains(second.Node.Span);
            if ((firstInThen && secondInElse) || (firstInElse && secondInThen))
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxNode? GetContainingExecutableBoundary(SyntaxNode node)
    {
        return node.Ancestors().FirstOrDefault(ancestor =>
            ancestor is BaseMethodDeclarationSyntax or
                AccessorDeclarationSyntax or
                LocalFunctionStatementSyntax or
                AnonymousFunctionExpressionSyntax or
                GlobalStatementSyntax);
    }

    public static CreateMapRegistry FromCompilation(Compilation compilation)
    {
        return Cache.GetValue(compilation, Build);
    }

    private static string FormatMappingTypeName(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            string rankSuffix = arrayType.Rank == 1 ? "[]" : $"[{new string(',', arrayType.Rank - 1)}]";
            return $"{FormatMappingTypeName(arrayType.ElementType)}{rankSuffix}";
        }

        if (type is INamedTypeSymbol { TypeArguments.Length: > 0 } namedType)
        {
            string typeArguments = string.Join(", ", namedType.TypeArguments.Select(FormatMappingTypeName));
            return $"{namedType.Name}<{typeArguments}>";
        }

        return type.Name;
    }

    private static bool HasCycleBreaker(
        InvocationExpressionSyntax createMapInvocation,
        InvocationExpressionSyntax? reverseMapInvocation,
        SemanticModel semanticModel,
        bool reverseDirection,
        string methodName)
    {
        IAssemblySymbol? autoMapperAssembly = GetInvocationAssembly(createMapInvocation, semanticModel);
        if (autoMapperAssembly == null)
        {
            return false;
        }

        for (SyntaxNode? parent = createMapInvocation.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is not InvocationExpressionSyntax chainedCall)
            {
                continue;
            }

            bool appliesToReverseDirection = reverseMapInvocation != null &&
                                             reverseMapInvocation.Ancestors().Contains(chainedCall);
            if (appliesToReverseDirection != reverseDirection)
            {
                continue;
            }

            if (IsAutoMapperMethodInvocationFromAssembly(
                    chainedCall,
                    semanticModel,
                    methodName,
                    autoMapperAssembly) &&
                IsMappingInitializerRootedAtCreateMap(
                    chainedCall,
                    createMapInvocation,
                    semanticModel,
                    autoMapperAssembly))
            {
                return true;
            }
        }

        return HasDeferredCycleBreaker(
            createMapInvocation,
            reverseMapInvocation,
            semanticModel,
            reverseDirection,
            methodName,
            autoMapperAssembly);
    }

    private static bool HasDeferredCycleBreaker(
        InvocationExpressionSyntax createMapInvocation,
        InvocationExpressionSyntax? reverseMapInvocation,
        SemanticModel semanticModel,
        bool reverseDirection,
        string methodName,
        IAssemblySymbol autoMapperAssembly)
    {
        VariableDeclaratorSyntax? declarator = createMapInvocation.Ancestors()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();
        if (declarator?.Initializer is not { Value: ExpressionSyntax initializer } ||
            !IsMappingInitializerRootedAtCreateMap(initializer, createMapInvocation, semanticModel, autoMapperAssembly) ||
            declarator.Parent?.Parent is not LocalDeclarationStatementSyntax declaration ||
            declaration.Declaration.Variables.Count != 1 ||
            declaration.Parent is not BlockSyntax block ||
            semanticModel.GetDeclaredSymbol(declarator) is not ILocalSymbol mappingLocal)
        {
            return false;
        }

        bool localRepresentsReverseDirection = reverseMapInvocation != null;
        foreach (ExpressionStatementSyntax statement in block.Statements
                     .OfType<ExpressionStatementSyntax>()
                     .Where(candidate => candidate.SpanStart > declaration.SpanStart))
        {
            if (!HasStraightLinePath(
                    declaration,
                    statement,
                    block,
                    mappingLocal,
                    semanticModel,
                    autoMapperAssembly))
            {
                continue;
            }

            foreach (InvocationExpressionSyntax candidate in statement.Expression.DescendantNodesAndSelf()
                         .OfType<InvocationExpressionSyntax>())
            {
                if (!IsDirectFluentInvocation(statement.Expression, candidate) ||
                    !IsAutoMapperMethodInvocationFromAssembly(
                        candidate,
                        semanticModel,
                        methodName,
                        autoMapperAssembly) ||
                    candidate.Expression is not MemberAccessExpressionSyntax memberAccess ||
                    !IsFluentReceiverRootedAtLocal(
                        memberAccess.Expression,
                        mappingLocal,
                        semanticModel,
                        autoMapperAssembly))
                {
                    continue;
                }

                bool followsReverseMap = memberAccess.Expression.DescendantNodesAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(invocation => IsAutoMapperMethodInvocationFromAssembly(
                        invocation,
                        semanticModel,
                        "ReverseMap",
                        autoMapperAssembly));
                bool appliesToReverseDirection = localRepresentsReverseDirection || followsReverseMap;
                if (appliesToReverseDirection == reverseDirection)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsDirectFluentInvocation(
        ExpressionSyntax statementExpression,
        InvocationExpressionSyntax target)
    {
        ExpressionSyntax current = statementExpression;
        while (true)
        {
            while (current is ParenthesizedExpressionSyntax parenthesized)
            {
                current = parenthesized.Expression;
            }

            if (ReferenceEquals(current, target))
            {
                return true;
            }

            if (current is not InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax memberAccess
                })
            {
                return false;
            }

            current = memberAccess.Expression;
        }
    }

    private static bool HasStraightLinePath(
        LocalDeclarationStatementSyntax declaration,
        ExpressionStatementSyntax statement,
        BlockSyntax block,
        ILocalSymbol mappingLocal,
        SemanticModel semanticModel,
        IAssemblySymbol autoMapperAssembly)
    {
        int declarationIndex = block.Statements.IndexOf(declaration);
        int statementIndex = block.Statements.IndexOf(statement);
        if (declarationIndex < 0 || statementIndex <= declarationIndex)
        {
            return false;
        }

        for (int index = declarationIndex + 1; index < statementIndex; index++)
        {
            StatementSyntax interveningStatement = block.Statements[index];
            if (interveningStatement is LocalFunctionStatementSyntax or EmptyStatementSyntax)
            {
                continue;
            }

            if (interveningStatement is ExpressionStatementSyntax expressionStatement &&
                IsSafeSameLocalAutoMapperStatement(
                    expressionStatement,
                    mappingLocal,
                    semanticModel,
                    autoMapperAssembly))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsSafeSameLocalAutoMapperStatement(
        ExpressionStatementSyntax statement,
        ILocalSymbol mappingLocal,
        SemanticModel semanticModel,
        IAssemblySymbol autoMapperAssembly)
    {
        if (statement.Expression is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax memberAccess
            } invocation ||
            !IsAutoMapperMethodInvocationFromAssembly(
                invocation,
                semanticModel,
                memberAccess.Name.Identifier.ValueText,
                autoMapperAssembly) ||
            !IsFluentReceiverRootedAtLocal(
                memberAccess.Expression,
                mappingLocal,
                semanticModel,
                autoMapperAssembly) ||
            HasPotentiallyMutatingCallbackOperation(
                invocation,
                semanticModel,
                autoMapperAssembly))
        {
            return false;
        }

        DataFlowAnalysis? dataFlow = semanticModel.AnalyzeDataFlow(statement);
        return dataFlow?.Succeeded == true &&
               !dataFlow.WrittenInside.Any(symbol =>
                   SymbolEqualityComparer.Default.Equals(symbol, mappingLocal));
    }

    private static bool HasPotentiallyMutatingCallbackOperation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IAssemblySymbol autoMapperAssembly)
    {
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            TypeInfo argumentType = semanticModel.GetTypeInfo(argument.Expression);
            if (argument.Expression is not AnonymousFunctionExpressionSyntax &&
                argumentType.ConvertedType?.TypeKind == TypeKind.Delegate)
            {
                return true;
            }

            if (argument.DescendantNodesAndSelf().Any(node =>
                    node is ObjectCreationExpressionSyntax or
                        ImplicitObjectCreationExpressionSyntax or
                        AssignmentExpressionSyntax or
                        AwaitExpressionSyntax))
            {
                return true;
            }

            foreach (InvocationExpressionSyntax nestedInvocation in argument.DescendantNodesAndSelf()
                         .OfType<InvocationExpressionSyntax>())
            {
                if (nestedInvocation.Expression is not MemberAccessExpressionSyntax nestedMemberAccess ||
                    !IsAutoMapperMethodInvocationFromAssembly(
                        nestedInvocation,
                        semanticModel,
                        nestedMemberAccess.Name.Identifier.ValueText,
                        autoMapperAssembly))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<InvocationExpressionSyntax> GetDeferredReverseMapInvocations(
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel)
    {
        IAssemblySymbol? autoMapperAssembly = GetInvocationAssembly(createMapInvocation, semanticModel);
        if (autoMapperAssembly == null)
        {
            yield break;
        }

        VariableDeclaratorSyntax? declarator = createMapInvocation.Ancestors()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();
        if (declarator?.Initializer is not { Value: ExpressionSyntax initializer } ||
            !IsMappingInitializerRootedAtCreateMap(
                initializer,
                createMapInvocation,
                semanticModel,
                autoMapperAssembly) ||
            declarator.Parent?.Parent is not LocalDeclarationStatementSyntax declaration ||
            declaration.Declaration.Variables.Count != 1 ||
            declaration.Parent is not BlockSyntax block ||
            semanticModel.GetDeclaredSymbol(declarator) is not ILocalSymbol mappingLocal)
        {
            yield break;
        }

        foreach (ExpressionStatementSyntax statement in block.Statements
                     .OfType<ExpressionStatementSyntax>()
                     .Where(candidate => candidate.SpanStart > declaration.SpanStart))
        {
            if (statement.Expression is not InvocationExpressionSyntax candidate ||
                candidate.ArgumentList.Arguments.Count != 0 ||
                !IsAutoMapperMethodInvocationFromAssembly(
                    candidate,
                    semanticModel,
                    "ReverseMap",
                    autoMapperAssembly) ||
                candidate.Expression is not MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax receiver
                } ||
                !SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(receiver).Symbol,
                    mappingLocal))
            {
                continue;
            }

            yield return candidate;
        }
    }

    private static IAssemblySymbol? GetInvocationAssembly(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        return semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
            ? (method.ReducedFrom ?? method).ContainingAssembly
            : null;
    }

    private static bool IsAutoMapperMethodInvocationFromAssembly(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string methodName,
        IAssemblySymbol autoMapperAssembly)
    {
        return MappingChainAnalysisHelper.IsAutoMapperMethodInvocation(
                   invocation,
                   semanticModel,
                   methodName) &&
               GetInvocationAssembly(invocation, semanticModel) is { } containingAssembly &&
               SymbolEqualityComparer.Default.Equals(containingAssembly, autoMapperAssembly);
    }

    private static bool TryUnwrapTransparentExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        out ExpressionSyntax unwrapped)
    {
        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            unwrapped = parenthesized.Expression;
            return true;
        }

        if (expression is PostfixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SuppressNullableWarningExpression
            } suppressNullableWarning)
        {
            unwrapped = suppressNullableWarning.Operand;
            return true;
        }

        if (expression is CastExpressionSyntax cast)
        {
            Conversion conversion = semanticModel.GetConversion(cast.Expression);
            if (conversion.IsIdentity || conversion.IsReference)
            {
                unwrapped = cast.Expression;
                return true;
            }
        }

        unwrapped = expression;
        return false;
    }

    internal static bool IsMappingInitializerRootedAtCreateMap(
        ExpressionSyntax initializer,
        InvocationExpressionSyntax createMapInvocation,
        SemanticModel semanticModel,
        IAssemblySymbol autoMapperAssembly)
    {
        ExpressionSyntax current = initializer;
        while (true)
        {
            if (TryUnwrapTransparentExpression(current, semanticModel, out ExpressionSyntax unwrapped))
            {
                current = unwrapped;
                continue;
            }

            if (current == createMapInvocation)
            {
                return true;
            }

            if (current is not InvocationExpressionSyntax invocation ||
                invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                !IsAutoMapperMethodInvocationFromAssembly(
                    invocation,
                    semanticModel,
                    memberAccess.Name.Identifier.ValueText,
                    autoMapperAssembly))
            {
                return false;
            }

            current = memberAccess.Expression;
        }
    }

    private static bool IsFluentReceiverRootedAtLocal(
        ExpressionSyntax expression,
        ILocalSymbol mappingLocal,
        SemanticModel semanticModel,
        IAssemblySymbol autoMapperAssembly)
    {
        ExpressionSyntax current = expression;
        while (true)
        {
            if (TryUnwrapTransparentExpression(current, semanticModel, out ExpressionSyntax unwrapped))
            {
                current = unwrapped;
                continue;
            }

            if (current is IdentifierNameSyntax identifier)
            {
                return SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier).Symbol,
                    mappingLocal);
            }

            if (current is not InvocationExpressionSyntax invocation ||
                invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                return false;
            }

            if (!IsAutoMapperMethodInvocationFromAssembly(
                    invocation,
                    semanticModel,
                    memberAccess.Name.Identifier.ValueText,
                    autoMapperAssembly))
            {
                return false;
            }

            current = memberAccess.Expression;
        }
    }

    internal struct MappingInfo
    {
        public ITypeSymbol Source;
        public ITypeSymbol Destination;
        public Location Location;
        public InvocationExpressionSyntax Node;
        public SemanticModel SemanticModel;
        public bool IsReverseMap;
        public bool IsCycleConstrained;
        public bool HasMaxDepth;
        public bool HasPreserveReferences;
        public bool HasConvertUsing;
    }

    private class MappingComparer : IEqualityComparer<(ITypeSymbol Source, ITypeSymbol Destination)>
    {
        public bool Equals((ITypeSymbol Source, ITypeSymbol Destination) x,
            (ITypeSymbol Source, ITypeSymbol Destination) y)
        {
            return SymbolEqualityComparer.Default.Equals(x.Source, y.Source) &&
                   SymbolEqualityComparer.Default.Equals(x.Destination, y.Destination);
        }

        public int GetHashCode((ITypeSymbol Source, ITypeSymbol Destination) obj)
        {
            int h1 = SymbolEqualityComparer.Default.GetHashCode(obj.Source);
            int h2 = SymbolEqualityComparer.Default.GetHashCode(obj.Destination);
            return h1 ^ h2;
        }
    }
}
