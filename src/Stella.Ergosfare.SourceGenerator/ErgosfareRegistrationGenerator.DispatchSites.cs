using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.Symbols;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     Dispatch-site side of the generator: discovers mediator dispatch invocations in the
///     current compilation, aggregates the dispatch manifests of referenced assemblies, and
///     judges whole-closure dispatch reachability in composition-root compilations —
///     provably dead dispatches (ERGO005/006), unreachable handlers (ERGO007), the
///     opt-in handler trim (ERGO008) and the strict-mode opacity aid (ERGO009).
/// </summary>
public sealed partial class ErgosfareRegistrationGenerator
{
    private const string CommandMediatorInterfaceName = "ICommandMediator";
    private const string QueryMediatorInterfaceName = "IQueryMediator";
    private const string EventMediatorInterfaceName = "IEventMediator";
    private const string MessageMediatorInterfaceName = "IMessageMediator";


    private const string CompositionRootBuildProperty = "build_property.ErgosfareCompositionRoot";
    private const string TrimUnusedHandlersBuildProperty = "build_property.ErgosfareTrimUnusedHandlers";

    /// <summary>
    ///     Cheap syntax pre-filter for dispatch invocations: an invocation with arguments
    ///     whose invoked simple name is one of the mediator dispatch method names.
    /// </summary>
    private static bool IsDispatchInvocationCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } invocation)
        {
            return false;
        }

        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            SimpleNameSyntax simpleName => simpleName.Identifier.ValueText,
            _ => null,
        };

        return name is "SendAsync" or "QueryAsync" or "StreamAsync" or "PublishAsync" or "DispatchAsync" or "Mediate";
    }

    /// <summary>
    ///     Cheap syntax pre-filter for registration invocations: an invocation whose
    ///     invoked simple name is one of the registration method names. RegisterGenerated
    ///     and RegisterAll are deliberately absent — they are the bulk collection of what
    ///     discovery already counts as evidence.
    /// </summary>
    private static bool IsRegistrationInvocationCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            SimpleNameSyntax simpleName => simpleName.Identifier.ValueText,
            _ => null,
        };

        return name is "Register" or "RegisterParticipants";
    }

    /// <summary>
    ///     Projects a candidate invocation to its registration-site model, or <c>null</c>
    ///     when the invocation does not bind to an Ergosfare registration surface (or is
    ///     the module-container <c>Register(IModule)</c>, whose real registrations are the
    ///     builder calls inside it). Manual registration is the same collection path as
    ///     <c>RegisterGenerated()</c>, so a statically known type argument contributes its
    ///     main-handler contracts as coverage evidence; anything unknowable is opaque and
    ///     suspends the dead-dispatch judgment.
    /// </summary>
    private static RegistrationSiteModel? TransformRegistrationSite(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method
            || method.ContainingType is not { } containing
            || !IsErgosfareRegistrationSurface(containing))
        {
            return null;
        }

        switch (method.Name)
        {
            case "RegisterParticipants":
                // A batch of types assembled at run time is a value, not a set of type
                // arguments; nothing here can say which types it carries.
                return OpaqueRegistration();

            case "Register" when method is { IsGenericMethod: true, TypeArguments.Length: 1 }:
                // A type argument that is itself a type parameter — Register<T>() inside a
                // generic method — names a different type per instantiation, none of which
                // this compilation can enumerate.
                return method.TypeArguments[0] is INamedTypeSymbol genericArgument
                    ? EvidenceRegistration(genericArgument)
                    : UnknownTypeRegistration(invocation);

            case "Register" when method.Parameters.Length >= 1:
            {
                var parameterType = method.Parameters[0].Type;

                if (parameterType is INamedTypeSymbol { Name: "Type" } typeParameter
                    && SymbolNaming.IsInNamespace(typeParameter, "System"))
                {
                    // Register(typeof(X)) is provable; any other Type-valued argument
                    // is a runtime decision.
                    if (FindMessageArgument(invocation, method) is { } argument
                        && UnwrapConversions(argument.Expression) is TypeOfExpressionSyntax typeOf
                        && ctx.SemanticModel.GetTypeInfo(typeOf.Type, ct).Type is INamedTypeSymbol literal
                        && literal is not IErrorTypeSymbol)
                    {
                        return EvidenceRegistration(literal);
                    }

                    return UnknownTypeRegistration(invocation);
                }

                // Register(IModule) and friends: a container, not a type registration —
                // the builder calls inside the module are the real, visible sites.
                return null;
            }

            default:
                return null;
        }

        static RegistrationSiteModel OpaqueRegistration() => new()
        {
            TypeMetadataName = null,
            MainHandlerMessageKeys = ImmutableArray<string>.Empty,
            IsOpaque = true,
            UnknownTypeLocation = null,
        };

        // Opaque, and a defect: ERGO018 reports it at the call. Still opaque so the
        // dead-dispatch judgment stays quiet — the dispatches downstream of an unknown
        // registration are not the finding, the registration is.
        static RegistrationSiteModel UnknownTypeRegistration(SyntaxNode call) => new()
        {
            TypeMetadataName = null,
            MainHandlerMessageKeys = ImmutableArray<string>.Empty,
            IsOpaque = true,
            UnknownTypeLocation = LocationInfo.From(call),
        };
    }

    /// <summary>
    ///     Builds the evidence model of a provably registered type: its main-handler
    ///     message expressions. Generic registrations stay opaque — the runtime builds
    ///     their descriptors reflectively, which the compile-time model cannot mirror
    ///     (<see cref="BuildDescriptors"/> deliberately returns nothing for them).
    /// </summary>
    private static RegistrationSiteModel EvidenceRegistration(INamedTypeSymbol registered)
    {
        for (var current = registered; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return new RegistrationSiteModel
                {
                    TypeMetadataName = null,
                    MainHandlerMessageKeys = ImmutableArray<string>.Empty,
                    IsOpaque = true,

                    // Named, but generic: the runtime builds a generic registration's
                    // descriptors reflectively per closed form, so the type is known and its
                    // evidence is not. Not the ERGO018 defect.
                    UnknownTypeLocation = null,
                };
            }
        }

        ImmutableArray<string>.Builder? keys = null;

        foreach (var descriptor in ContractReader.BuildDescriptors(registered))
        {
            if (descriptor.Kind == DescriptorKind.MainHandler)
            {
                (keys ??= ImmutableArray.CreateBuilder<string>()).Add(descriptor.MessageTypeExpression);
            }
        }

        return new RegistrationSiteModel
        {
            TypeMetadataName = SymbolNaming.BuildMetadataName(registered.OriginalDefinition),
            MainHandlerMessageKeys = keys?.ToImmutable() ?? ImmutableArray<string>.Empty,
            IsOpaque = false,
            UnknownTypeLocation = null,
        };
    }

    /// <summary>
    ///     Whether the type is one of Ergosfare's registration surfaces: the module
    ///     builders (and their DI-extension siblings) or the registry abstraction itself.
    /// </summary>
    private static bool IsErgosfareRegistrationSurface(INamedTypeSymbol type)
        => SymbolNaming.IsInNamespace(type, "Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection")
           || SymbolNaming.IsInNamespace(type, "Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection")
           || SymbolNaming.IsInNamespace(type, "Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection")
           || SymbolNaming.IsInNamespace(type, "Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection");

    /// <summary>
    ///     Projects a candidate invocation to its dispatch-site model, or <c>null</c> when
    ///     the invocation does not bind to a mediator dispatch method. The message
    ///     argument's casts and conversions are looked through so the recorded static type
    ///     is the expression's natural one — <c>SendAsync((ICommand)x)</c> records
    ///     <c>x</c>'s declared type, not the cast target.
    /// </summary>
    private static DispatchSiteModel? TransformDispatchSite(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method)
        {
            return null;
        }

        if (ClassifyDispatchMethod(method) is not { } kind)
        {
            return null;
        }

        if (FindMessageArgument(invocation, method) is not { } argument)
        {
            return null;
        }

        var expression = UnwrapConversions(argument.Expression);
        var typeInfo = ctx.SemanticModel.GetTypeInfo(expression, ct);

        var site = BuildSiteModel(typeInfo.Type ?? typeInfo.ConvertedType, kind,
            LocationInfo.From(invocation), referencedAssemblyName: null);

        if (site is null)
        {
            return null;
        }

        ReadGroupFilter(ctx, invocation, method, ct, out var groups, out var unprovable);

        return site.Value with { Groups = groups, HasUnprovableGroups = unprovable };
    }

    /// <summary>
    ///     Reads the site's group filter: the names when every element is a literal the
    ///     compiler can fold, otherwise nothing plus the unprovable flag. A site that names
    ///     no filter at all reports the empty set — the default group, which is a group set
    ///     like any other.
    /// </summary>
    /// <remarks>
    ///     The one indirection worth following is a reference to a field or local whose
    ///     initializer is itself readable — the documented idiom is a
    ///     <c>static readonly GroupSet</c> defined once and reused, so refusing to look
    ///     through it would leave the recommended spelling unprovable.
    /// </remarks>
    private static void ReadGroupFilter(
        GeneratorSyntaxContext ctx,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        CancellationToken ct,
        out ImmutableArray<string> groups,
        out bool unprovable)
    {
        groups = ImmutableArray<string>.Empty;
        unprovable = false;

        if (FindGroupArgument(invocation, method) is not { } argument)
        {
            return;
        }

        var names = new List<string>();

        if (TryReadGroupNames(ctx, UnwrapConversions(argument.Expression), ct, names, depth: 0))
        {
            groups = GroupNames.Normalize(names);
            return;
        }

        unprovable = true;
    }

    /// <summary>
    ///     The argument bound to the dispatch method's group parameter — the
    ///     <c>IEnumerable&lt;string&gt;</c>, <c>GroupSet</c> or <c>string[]</c> slot — honoring
    ///     named arguments and skipping the parameter when the call omits it.
    /// </summary>
    private static ArgumentSyntax? FindGroupArgument(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var groupParameterIndex = -1;

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (IsGroupParameter(method.Parameters[i]))
            {
                groupParameterIndex = i;
                break;
            }
        }

        if (groupParameterIndex < 0)
        {
            return null;
        }

        var groupParameterName = method.Parameters[groupParameterIndex].Name;
        var arguments = invocation.ArgumentList.Arguments;

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            if (argument.NameColon is { } nameColon)
            {
                if (string.Equals(nameColon.Name.Identifier.ValueText, groupParameterName, StringComparison.Ordinal))
                {
                    return argument;
                }
            }
            else if (i == groupParameterIndex)
            {
                return argument;
            }
        }

        return null;
    }

    private static bool IsGroupParameter(IParameterSymbol parameter)
    {
        if (parameter.Name is "groups")
        {
            return true;
        }

        return parameter.Type is INamedTypeSymbol { Name: "GroupSet" } groupSet
               && SymbolNaming.IsInNamespace(groupSet, ContractNames.CoreAbstractionsNamespace);
    }

    /// <summary>
    ///     Collects the literal names behind a group expression, returning <c>false</c> the
    ///     moment anything is not statically readable.
    /// </summary>
    private static bool TryReadGroupNames(
        GeneratorSyntaxContext ctx,
        ExpressionSyntax expression,
        CancellationToken ct,
        List<string> names,
        int depth)
    {
        // One hop through a named reference, no more: a chain of aliases is not an idiom
        // worth chasing, and the bound is what keeps this analysis finite.
        if (depth > 1)
        {
            return false;
        }

        switch (expression)
        {
            // null / default: no filter, the default group.
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NullLiteralExpression):
            case LiteralExpressionSyntax when expression.IsKind(SyntaxKind.DefaultLiteralExpression):
                return true;

            // GroupSet.Of("a", "b") — the canonical spelling.
            case InvocationExpressionSyntax call:
            {
                if (ctx.SemanticModel.GetSymbolInfo(call, ct).Symbol is not IMethodSymbol
                    {
                        Name: "Of", ContainingType: { Name: "GroupSet" } owner,
                    }
                    || !SymbolNaming.IsInNamespace(owner, ContractNames.CoreAbstractionsNamespace))
                {
                    return false;
                }

                foreach (var argument in call.ArgumentList.Arguments)
                {
                    if (!TryReadStringLiteral(ctx, argument.Expression, ct, names))
                    {
                        return false;
                    }
                }

                return true;
            }

            // new[] { "a" } / new string[] { "a" }
            case ArrayCreationExpressionSyntax { Initializer: { } arrayInitializer }:
                return TryReadStringLiterals(ctx, arrayInitializer.Expressions, ct, names);

            case ImplicitArrayCreationExpressionSyntax implicitArray:
                return TryReadStringLiterals(ctx, implicitArray.Initializer.Expressions, ct, names);

            // ["a", "b"] — the collection expression.
            case CollectionExpressionSyntax collection:
            {
                foreach (var element in collection.Elements)
                {
                    if (element is not ExpressionElementSyntax { Expression: { } elementExpression }
                        || !TryReadStringLiteral(ctx, elementExpression, ct, names))
                    {
                        return false;
                    }
                }

                return true;
            }

            // GroupSet.Empty, or a reference to a field/local holding a readable set.
            case IdentifierNameSyntax:
            case MemberAccessExpressionSyntax:
            {
                var symbol = ctx.SemanticModel.GetSymbolInfo(expression, ct).Symbol;

                if (symbol is IFieldSymbol { Name: "Empty", ContainingType: { Name: "GroupSet" } emptyOwner }
                    && SymbolNaming.IsInNamespace(emptyOwner, ContractNames.CoreAbstractionsNamespace))
                {
                    return true;
                }

                return symbol is IFieldSymbol or ILocalSymbol
                       && TryReadInitializer(ctx, symbol, ct, names, depth);
            }

            default:
                return false;
        }
    }

    /// <summary>
    ///     Follows a field or local back to its initializer and reads that instead. Only a
    ///     single, unambiguous declaration counts, and only one that this compilation can
    ///     see — a set assembled elsewhere stays unprovable.
    /// </summary>
    private static bool TryReadInitializer(
        GeneratorSyntaxContext ctx,
        ISymbol symbol,
        CancellationToken ct,
        List<string> names,
        int depth)
    {
        if (symbol is IFieldSymbol { IsReadOnly: false, IsConst: false })
        {
            // A writable field can hold anything by the time the dispatch runs.
            return false;
        }

        var references = symbol.DeclaringSyntaxReferences;

        if (references.Length != 1)
        {
            return false;
        }

        var initializer = references[0].GetSyntax(ct) switch
        {
            VariableDeclaratorSyntax { Initializer.Value: { } value } => value,
            PropertyDeclarationSyntax { Initializer.Value: { } value } => value,
            _ => null,
        };

        if (initializer is null)
        {
            return false;
        }

        // The initializer's own semantic model: a field declared in another file belongs to
        // a different syntax tree, and the site's model cannot answer questions about it.
        var initializerContext = initializer.SyntaxTree == ctx.SemanticModel.SyntaxTree
            ? ctx
            : default;

        if (initializerContext.SemanticModel is null)
        {
            return false;
        }

        return TryReadGroupNames(initializerContext, UnwrapConversions(initializer), ct, names, depth + 1);
    }

    private static bool TryReadStringLiterals(
        GeneratorSyntaxContext ctx,
        SeparatedSyntaxList<ExpressionSyntax> expressions,
        CancellationToken ct,
        List<string> names)
    {
        foreach (var expression in expressions)
        {
            if (!TryReadStringLiteral(ctx, expression, ct, names))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Reads one group name: any expression the compiler folds to a string constant,
    ///     which covers literals, <c>const</c> fields and constant concatenation alike.
    /// </summary>
    private static bool TryReadStringLiteral(
        GeneratorSyntaxContext ctx,
        ExpressionSyntax expression,
        CancellationToken ct,
        List<string> names)
    {
        var constant = ctx.SemanticModel.GetConstantValue(expression, ct);

        if (constant is { HasValue: true, Value: string name })
        {
            names.Add(name);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Maps a bound method to its dispatch surface: the mediator interfaces' dispatch
    ///     methods, matched on the interface itself or — for calls through a concrete
    ///     mediator implementation — on any mediator interface the containing type
    ///     implements.
    /// </summary>
    private static DispatchSiteKind? ClassifyDispatchMethod(IMethodSymbol method)
    {
        if (method.ContainingType is not { } containing)
        {
            return null;
        }

        if (ClassifyByMediatorInterface(containing, method.Name) is { } direct)
        {
            return direct;
        }

        foreach (var iface in containing.AllInterfaces)
        {
            if (ClassifyByMediatorInterface(iface, method.Name) is { } inherited)
            {
                return inherited;
            }
        }

        return null;
    }

    private static DispatchSiteKind? ClassifyByMediatorInterface(INamedTypeSymbol type, string methodName)
        => methodName switch
        {
            "SendAsync" when type.Name == CommandMediatorInterfaceName && SymbolNaming.IsInNamespace(type, ContractNames.CommandMarkerNamespace)
                => DispatchSiteKind.Command,
            "QueryAsync" when type.Name == QueryMediatorInterfaceName && SymbolNaming.IsInNamespace(type, ContractNames.QueryMarkerNamespace)
                => DispatchSiteKind.Query,
            "StreamAsync" when type.Name == QueryMediatorInterfaceName && SymbolNaming.IsInNamespace(type, ContractNames.QueryMarkerNamespace)
                => DispatchSiteKind.Stream,
            "PublishAsync" when type.Name == EventMediatorInterfaceName && SymbolNaming.IsInNamespace(type, ContractNames.EventMarkerNamespace)
                => DispatchSiteKind.Event,
            "DispatchAsync" or "Mediate" when type.Name == MessageMediatorInterfaceName && SymbolNaming.IsInNamespace(type, ContractNames.CoreAbstractionsNamespace)
                => DispatchSiteKind.Message,
            _ => null,
        };

    /// <summary>
    ///     The argument bound to the method's first parameter — the message on every
    ///     dispatch surface — honoring named arguments.
    /// </summary>
    private static ArgumentSyntax? FindMessageArgument(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (method.Parameters.IsEmpty)
        {
            return null;
        }

        var messageParameterName = method.Parameters[0].Name;
        var arguments = invocation.ArgumentList.Arguments;

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            if (argument.NameColon is { } nameColon)
            {
                if (string.Equals(nameColon.Name.Identifier.ValueText, messageParameterName, StringComparison.Ordinal))
                {
                    return argument;
                }
            }
            else if (i == 0)
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>
    ///     Strips casts, <c>as</c> conversions, parentheses and null-forgiveness so the
    ///     recorded static type is the message expression's natural one.
    /// </summary>
    private static ExpressionSyntax UnwrapConversions(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    continue;
                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression):
                    expression = binary.Left;
                    continue;
                default:
                    return expression;
            }
        }
    }

    /// <summary>
    ///     Builds the site model for a static message type. Type parameters resolve to
    ///     their first named constraint; expressions with no usable static type (untyped
    ///     nulls, unconstrained type parameters) fall back to the surface's marker
    ///     interface — the site then conservatively reaches every message of that kind.
    ///     Core-mediator sites whose static type carries no Ergosfare marker produce no
    ///     model at all: plain-message dispatch lives outside the generator's closed world.
    /// </summary>
    private static DispatchSiteModel? BuildSiteModel(
        ITypeSymbol? type,
        DispatchSiteKind kind,
        LocationInfo? location,
        string? referencedAssemblyName)
    {
        if (type is ITypeParameterSymbol parameter)
        {
            type = FirstNamedConstraint(parameter);
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments.Length == 1)
        {
            type = nullable.TypeArguments[0];
        }

        if (type is not INamedTypeSymbol named || named is IErrorTypeSymbol)
        {
            return BuildMarkerFallbackSite(kind, location, referencedAssemblyName);
        }

        var isOpaque = IsOpaqueStaticType(named);

        if (kind == DispatchSiteKind.Message && !isOpaque)
        {
            ParticipantAttributes.GetMarkers(named, out var isCommand, out var isQuery, out var isEvent);

            if (!isCommand && !isQuery && !isEvent)
            {
                // A marker-less type through the core mediator is a plain message; the
                // closed-world judgment has nothing sound to say about it.
                return null;
            }
        }

        return new DispatchSiteModel
        {
            MessageTypeExpression = SymbolNaming.NormalizedTypeExpression(named),
            MessageTypeMetadataName = SymbolNaming.BuildMetadataName(named.OriginalDefinition),
            DisplayName = named.ToDisplayString(),
            Kind = kind,
            IsOpaque = isOpaque,
            IsValueType = named.IsValueType,
            IsGenericMessage = named.IsGenericType,
            AssignableKeys = ParticipantAttributes.GetAssignableKeys(named),
            Groups = ImmutableArray<string>.Empty,
            HasUnprovableGroups = false,
            Location = location,
            ReferencedAssemblyName = referencedAssemblyName,
        };
    }

    /// <summary>
    ///     The first named constraint of a type parameter (class constraints first), or
    ///     <c>null</c> when the parameter is unconstrained — the closest static evidence a
    ///     generic dispatch wrapper leaves behind.
    /// </summary>
    private static INamedTypeSymbol? FirstNamedConstraint(ITypeParameterSymbol parameter)
    {
        INamedTypeSymbol? firstInterface = null;

        foreach (var constraint in parameter.ConstraintTypes)
        {
            if (constraint is not INamedTypeSymbol namedConstraint)
            {
                continue;
            }

            if (namedConstraint.TypeKind == TypeKind.Class)
            {
                return namedConstraint;
            }

            firstInterface ??= namedConstraint;
        }

        return firstInterface;
    }

    /// <summary>
    ///     Whether the static type proves nothing about the concrete message: <c>object</c>,
    ///     <c>IMessage</c>, or one of the module marker interfaces (any arity — the
    ///     result-generic markers name a result, never a concrete message).
    /// </summary>
    private static bool IsOpaqueStaticType(INamedTypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Object)
        {
            return true;
        }

        if (type.TypeKind != TypeKind.Interface)
        {
            return false;
        }

        return (type.Name == "IMessage" && SymbolNaming.IsInNamespace(type, ContractNames.CoreAbstractionsNamespace))
               || (type.Name == ContractNames.CommandMarker && SymbolNaming.IsInNamespace(type, ContractNames.CommandMarkerNamespace))
               || (type.Name is ContractNames.QueryMarker or "IStreamQuery" && SymbolNaming.IsInNamespace(type, ContractNames.QueryMarkerNamespace))
               || (type.Name == ContractNames.EventMarker && SymbolNaming.IsInNamespace(type, ContractNames.EventMarkerNamespace));
    }

    /// <summary>
    ///     The opaque marker-interface site of a dispatch surface — recorded when no more
    ///     specific static type is recoverable, so reachability still sees the site.
    /// </summary>
    private static DispatchSiteModel BuildMarkerFallbackSite(
        DispatchSiteKind kind,
        LocationInfo? location,
        string? referencedAssemblyName)
    {
        var (expression, metadataName, displayName) = kind switch
        {
            DispatchSiteKind.Command => (
                "global::" + ContractNames.CommandMarkerNamespace + "." + ContractNames.CommandMarker,
                ContractNames.CommandMarkerNamespace + "." + ContractNames.CommandMarker,
                ContractNames.CommandMarkerNamespace + "." + ContractNames.CommandMarker),
            DispatchSiteKind.Query or DispatchSiteKind.Stream => (
                "global::" + ContractNames.QueryMarkerNamespace + "." + ContractNames.QueryMarker,
                ContractNames.QueryMarkerNamespace + "." + ContractNames.QueryMarker,
                ContractNames.QueryMarkerNamespace + "." + ContractNames.QueryMarker),
            DispatchSiteKind.Event => (
                "global::" + ContractNames.EventMarkerNamespace + "." + ContractNames.EventMarker,
                ContractNames.EventMarkerNamespace + "." + ContractNames.EventMarker,
                ContractNames.EventMarkerNamespace + "." + ContractNames.EventMarker),
            _ => (
                "global::" + ContractNames.CoreAbstractionsNamespace + ".IMessage",
                ContractNames.CoreAbstractionsNamespace + ".IMessage",
                ContractNames.CoreAbstractionsNamespace + ".IMessage"),
        };

        return new DispatchSiteModel
        {
            MessageTypeExpression = expression,
            MessageTypeMetadataName = metadataName,
            DisplayName = displayName,
            Kind = kind,
            IsOpaque = true,
            IsValueType = false,
            IsGenericMessage = false,
            AssignableKeys = ImmutableArray<string>.Empty,
            Groups = ImmutableArray<string>.Empty,
            HasUnprovableGroups = false,
            Location = location,
            ReferencedAssemblyName = referencedAssemblyName,
        };
    }


    /// <summary>
    ///     Aggregates the dispatch manifests of the referenced assemblies, rehydrating each
    ///     recorded site's static type back to a symbol so assignability is re-derived here
    ///     rather than trusted from strings. Assemblies skipped by the reserved-prefix rule
    ///     are Ergosfare's own; every other Ergosfare-referencing assembly without a
    ///     manifest marker flips <see cref="DispatchManifestScanResult.HasUnknownSiteAssemblies"/>.
    /// </summary>
    private static DispatchManifestScanResult ScanReferencedManifests(Compilation compilation, CancellationToken ct)
    {
        ImmutableArray<DispatchSiteModel>.Builder? sites = null;
        ImmutableArray<RegistrationSiteModel>.Builder? registrations = null;
        var hasUnknownSiteAssemblies = false;
        var hasOpaqueRegistrations = false;

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ct.ThrowIfCancellationRequested();

            if (!ReferenceScanner.ReferencesErgosfare(assembly))
            {
                continue;
            }

            if (ReferenceScanner.IsErgosfareAssemblyName(assembly.Name) && !ReferenceScanner.HasForceScanReferencesOptIn(assembly))
            {
                continue;
            }

            var hasManifestMarker = false;
            List<(string MetadataName, DispatchSiteKind Kind, bool Opaque, ImmutableArray<string> Groups)>? siteEntries = null;
            List<string>? registrationEntries = null;

            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass is not { } attributeClass
                    || !SymbolNaming.IsInNamespace(attributeClass, ContractMetadataNames.DispatchSitesNamespace))
                {
                    continue;
                }

                switch (attributeClass.Name)
                {
                    case "DispatchManifestAttribute":
                        hasManifestMarker = true;

                        foreach (var named in attribute.NamedArguments)
                        {
                            if (named.Key == "HasOpaqueRegistrations" && named.Value.Value is true)
                            {
                                hasOpaqueRegistrations = true;
                            }
                        }

                        continue;

                    case "ManualRegistrationAttribute"
                        when attribute.ConstructorArguments.Length == 1
                             && attribute.ConstructorArguments[0].Value is string registeredName:
                        (registrationEntries ??= []).Add(registeredName);
                        continue;

                    case "DispatchSiteAttribute"
                        when attribute.ConstructorArguments.Length == 3
                             && attribute.ConstructorArguments[0].Value is string metadataName
                             && attribute.ConstructorArguments[2].Value is bool opaque:
                    {
                        var kind = attribute.ConstructorArguments[1].Value switch
                        {
                            byte b => (DispatchSiteKind)b,
                            int i => (DispatchSiteKind)i,
                            _ => (DispatchSiteKind?)null,
                        };

                        if (kind is not null && kind.Value <= DispatchSiteKind.Message)
                        {
                            (siteEntries ??= []).Add(
                                (metadataName, kind.Value, opaque, ReadManifestGroups(attribute)));
                        }

                        continue;
                    }
                }
            }

            if (!hasManifestMarker)
            {
                hasUnknownSiteAssemblies = true;
                continue;
            }

            if (siteEntries is not null)
            {
                foreach (var (metadataName, kind, opaque, groups) in siteEntries)
                {
                    var symbol = assembly.GetTypeByMetadataName(metadataName)
                                 ?? compilation.GetTypeByMetadataName(metadataName);

                    if (symbol is null)
                    {
                        // A site whose static type this compilation cannot resolve is a
                        // site the judgment cannot see — same soundness posture as a
                        // missing manifest.
                        hasUnknownSiteAssemblies = true;
                        continue;
                    }

                    if (BuildSiteModel(symbol, kind, location: null, assembly.Name) is { } value)
                    {
                        // Opacity and the group filter travel with the manifest: the recorder
                        // saw the original expression, the rehydrated symbol alone cannot
                        // reconstruct either.
                        (sites ??= ImmutableArray.CreateBuilder<DispatchSiteModel>())
                            .Add(value with { IsOpaque = opaque, Groups = groups });
                    }
                }
            }

            if (registrationEntries is not null)
            {
                foreach (var metadataName in registrationEntries)
                {
                    var symbol = assembly.GetTypeByMetadataName(metadataName)
                                 ?? compilation.GetTypeByMetadataName(metadataName);

                    if (symbol is null)
                    {
                        // Unresolvable evidence is missing evidence: the safe reading is
                        // an opaque registration, which suspends the dead-dispatch
                        // judgment rather than mis-firing it.
                        hasOpaqueRegistrations = true;
                        continue;
                    }

                    var model = EvidenceRegistration(symbol);
                    hasOpaqueRegistrations |= model.IsOpaque;
                    (registrations ??= ImmutableArray.CreateBuilder<RegistrationSiteModel>()).Add(model);
                }
            }
        }

        return new DispatchManifestScanResult(
            sites?.ToImmutable() ?? ImmutableArray<DispatchSiteModel>.Empty,
            registrations?.ToImmutable() ?? ImmutableArray<RegistrationSiteModel>.Empty,
            hasUnknownSiteAssemblies,
            hasOpaqueRegistrations);
    }

    /// <summary>
    ///     Reads a manifest site's recorded group filter. Absent or empty means the site
    ///     named no filter — or named one the recording generator could not read, which it
    ///     records the same way: neither keys a plan here.
    /// </summary>
    private static ImmutableArray<string> ReadManifestGroups(AttributeData attribute)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key != "Groups" || named.Value.Kind != TypedConstantKind.Array)
            {
                continue;
            }

            var values = named.Value.Values;

            if (values.IsDefaultOrEmpty)
            {
                return ImmutableArray<string>.Empty;
            }

            var names = new List<string>(values.Length);

            foreach (var value in values)
            {
                if (value.Value is not string name)
                {
                    return ImmutableArray<string>.Empty;
                }

                names.Add(name);
            }

            return GroupNames.Normalize(names);
        }

        return ImmutableArray<string>.Empty;
    }

    private static bool IsExecutableOutputKind(OutputKind outputKind)
        => outputKind is OutputKind.ConsoleApplication
            or OutputKind.WindowsApplication
            or OutputKind.WindowsRuntimeApplication;

    private static bool? ReadCompositionRootOverride(AnalyzerConfigOptionsProvider provider)
    {
        if (!provider.GlobalOptions.TryGetValue(CompositionRootBuildProperty, out var value))
        {
            return null;
        }

        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ? false : null;
    }

    private static bool ReadTrimUnusedHandlers(AnalyzerConfigOptionsProvider provider)
        => provider.GlobalOptions.TryGetValue(TrimUnusedHandlersBuildProperty, out var value)
           && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Reports the dispatch-reachability diagnostics and applies the opt-in handler
    ///     trim, returning the (possibly reduced) model list emission proceeds with.
    ///     ERGO009 is local and always evaluated; every closure-wide verdict requires a
    ///     composition root with reference scanning on, and the unreachable-handler side
    ///     additionally requires every closure assembly's manifest to be present.
    /// </summary>
    internal static List<RegistrableTypeModel> ApplyDispatchJudgment(
        SourceProductionContext context,
        List<RegistrableTypeModel> types,
        List<RegistrableTypeModel> excludedShadows,
        ImmutableArray<DispatchSiteModel> sourceSites,
        ImmutableArray<RegistrationSiteModel> registrationSites,
        DispatchManifestScanResult manifestScan,
        JudgmentInputs inputs)
    {
        // Strict-mode opacity aid: purely local, no composition knowledge involved.
        foreach (var site in sourceSites)
        {
            if (site.IsOpaque && site.Location is { } location)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.OpaqueDispatchSite, location.ToLocation(), site.DisplayName));
            }
        }

        var isCompositionRoot = inputs.CompositionRootOverride ?? inputs.IsExecutableOutput;

        if (!isCompositionRoot || !inputs.ScanReferences)
        {
            return types;
        }

        // Coverage evidence: every collection path that feeds the registry. Discovery is
        // the bulk path (RegisterGenerated); provable manual Register calls are the
        // per-type path — same registry, same trust. An opaque registration anywhere in
        // the closure means the evidence is incomplete by construction.
        var hasOpaqueRegistrations = manifestScan.HasOpaqueRegistrations;
        var handlerMessageKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var registration in registrationSites)
        {
            hasOpaqueRegistrations |= registration.IsOpaque;

            foreach (var key in registration.MainHandlerMessageKeys)
            {
                handlerMessageKeys.Add(TypeExpressions.DefinitionKey(key));
            }
        }

        foreach (var registration in manifestScan.RegistrationSites)
        {
            foreach (var key in registration.MainHandlerMessageKeys)
            {
                handlerMessageKeys.Add(TypeExpressions.DefinitionKey(key));
            }
        }

        // Indexes over the composition — excluded shadows included: they are possible
        // runtime instances (their coverage simply has to come from manual-registration
        // evidence). All comparisons run on definition-normalized keys, so
        // constructed-generic descriptors and sites match their definitions
        // conservatively.
        var modelsByKey = new Dictionary<string, RegistrableTypeModel>(StringComparer.Ordinal);
        var subtypesByKey = new Dictionary<string, List<RegistrableTypeModel>>(StringComparer.Ordinal);

        IndexCompositionTypes(types, handlerMessageKeys, modelsByKey, subtypesByKey, collectHandlerEvidence: true);
        IndexCompositionTypes(excludedShadows, handlerMessageKeys, modelsByKey, subtypesByKey, collectHandlerEvidence: false);

        // Per-site verdicts: dead dispatch (error) and uncovered-static dispatch
        // (warning) — but only while every registration in the closure is provable.
        if (!hasOpaqueRegistrations)
        {
            JudgeSites(context, sourceSites, handlerMessageKeys, modelsByKey, subtypesByKey);
            JudgeSites(context, manifestScan.Sites, handlerMessageKeys, modelsByKey, subtypesByKey);
        }

        // Per-message verdicts: a same-level main-handler contest is broken for every
        // container in the process — provable ones fail the build with the same
        // evidence model. Registration opacity matters only to the covariant arm (an
        // unseen registration could add the direct winner); extra handlers can never
        // resolve a direct-level contest, so that arm needs no suspension.
        JudgeContestedMainHandlers(context, types, handlerMessageKeys, hasOpaqueRegistrations);

        // The unreachable-handler side needs the whole closure's sites; any manifest-less
        // Ergosfare-referencing assembly makes that set incomplete. (Opaque registrations
        // do not matter here: registrations create no dispatch sites.)
        if (manifestScan.HasUnknownSiteAssemblies)
        {
            return types;
        }

        var reachedKeys = CollectReachedKeys(sourceSites, manifestScan.Sites, subtypesByKey);
        var trimmedExpressions = ReportUnreachableHandlers(
            context, types, reachedKeys, inputs.TrimUnusedHandlers);

        if (trimmedExpressions is null)
        {
            return types;
        }

        var kept = new List<RegistrableTypeModel>(types.Count - trimmedExpressions.Count);

        foreach (var type in types)
        {
            if (!trimmedExpressions.Contains(type.TypeofExpression))
            {
                kept.Add(type);
            }
        }

        return kept;
    }

    /// <summary>
    ///     Feeds one model list into the judgment's composition indexes; handler-message
    ///     evidence is collected only for discovered types — an excluded shadow's handler
    ///     role counts as evidence solely through a provable manual registration.
    /// </summary>
    private static void IndexCompositionTypes(
        List<RegistrableTypeModel> models,
        HashSet<string> handlerMessageKeys,
        Dictionary<string, RegistrableTypeModel> modelsByKey,
        Dictionary<string, List<RegistrableTypeModel>> subtypesByKey,
        bool collectHandlerEvidence)
    {
        foreach (var type in models)
        {
            var typeKey = TypeExpressions.DefinitionKey(type.TypeofExpression);

            if (!modelsByKey.ContainsKey(typeKey))
            {
                modelsByKey.Add(typeKey, type);
            }

            if (collectHandlerEvidence)
            {
                foreach (var descriptor in type.Descriptors)
                {
                    if (descriptor.Kind == DescriptorKind.MainHandler)
                    {
                        handlerMessageKeys.Add(TypeExpressions.DefinitionKey(descriptor.MessageTypeExpression));
                    }
                }
            }

            if (!type.IsDispatchableMessage)
            {
                continue;
            }

            foreach (var assignableKey in type.AssignableKeys)
            {
                var key = TypeExpressions.DefinitionKey(assignableKey);

                if (!subtypesByKey.TryGetValue(key, out var list))
                {
                    subtypesByKey.Add(key, list = []);
                }

                list.Add(type);
            }
        }
    }

    private static void JudgeSites(
        SourceProductionContext context,
        ImmutableArray<DispatchSiteModel> sites,
        HashSet<string> handlerMessageKeys,
        Dictionary<string, RegistrableTypeModel> modelsByKey,
        Dictionary<string, List<RegistrableTypeModel>> subtypesByKey)
    {
        foreach (var site in sites)
        {
            // Publishing to zero subscribers is a legal no-op, and generic descriptor
            // matching is definition-fuzzy — neither shape can carry a sound verdict.
            if (site.Kind == DispatchSiteKind.Event || site.IsGenericMessage)
            {
                continue;
            }

            var siteKey = TypeExpressions.DefinitionKey(site.MessageTypeExpression);

            if (IsCovered(siteKey, site.AssignableKeys, site.IsValueType, handlerMessageKeys))
            {
                continue;
            }

            RegistrableTypeModel? coveredSubtype = null;

            if (subtypesByKey.TryGetValue(siteKey, out var subtypes))
            {
                foreach (var subtype in subtypes)
                {
                    if (IsCovered(TypeExpressions.DefinitionKey(subtype.TypeofExpression), subtype.AssignableKeys,
                            subtype.IsValueType, handlerMessageKeys))
                    {
                        coveredSubtype = subtype;
                        break;
                    }
                }
            }

            var originSuffix = site.ReferencedAssemblyName is { } assemblyName
                ? $" (dispatch site recorded in referenced assembly '{assemblyName}')"
                : string.Empty;

            if (coveredSubtype is null)
            {
                // Neither the static type nor any closure subtype is covered: whatever
                // instance the expression carries, the dispatch dies.
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.DeadDispatch,
                    site.Location?.ToLocation(),
                    site.DisplayName,
                    originSuffix));
            }
            else if (modelsByKey.TryGetValue(siteKey, out var selfModel) && selfModel.IsDispatchableMessage)
            {
                // The static type itself is a concrete dispatchable message with no
                // coverage while a subtype is covered — an instance of exactly the static
                // type still dies.
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.UncoveredStaticDispatch,
                    site.Location?.ToLocation(),
                    site.DisplayName,
                    coveredSubtype.Value.DisplayName,
                    originSuffix));
            }
        }
    }

    /// <summary>
    ///     The ERGO010 judgment: for every dispatchable command/query/stream message in
    ///     the composition, counts the provable main-handler claims per priority level
    ///     and fails the build on a same-level contest — several direct claimants, or
    ///     several covariant ones with provably no direct winner. Only default-discovery,
    ///     ungrouped, non-excluded handlers count (anything keyed, grouped or manual is a
    ///     container choice the compiler cannot prove co-registered — those abstain, so
    ///     deliberately contested keyed suites keep compiling). The covariant arm demands
    ///     absence of a direct handler, which an opaque registration could silently
    ///     provide — that arm suspends with the closure's registration opacity, exactly
    ///     like the dead-dispatch judgment.
    /// </summary>
    private static void JudgeContestedMainHandlers(
        SourceProductionContext context,
        List<RegistrableTypeModel> types,
        HashSet<string> handlerMessageKeys,
        bool hasOpaqueRegistrations)
    {
        // Provable claims per declared message key: handler types the bulk path
        // registers unconditionally. One entry per handler type per key — a handler
        // claiming a message through several contracts is still one claimant.
        Dictionary<string, List<RegistrableTypeModel>>? claimsByKey = null;

        foreach (var type in types)
        {
            if (!type.DiscoveryKeys.IsEmpty || type.GroupsExpression is not null)
            {
                continue;
            }

            HashSet<string>? claimed = null;

            foreach (var descriptor in type.Descriptors)
            {
                if (descriptor.Kind != DescriptorKind.MainHandler)
                {
                    continue;
                }

                var key = TypeExpressions.DefinitionKey(descriptor.MessageTypeExpression);

                if (!(claimed ??= new HashSet<string>(StringComparer.Ordinal)).Add(key))
                {
                    continue;
                }

                claimsByKey ??= new Dictionary<string, List<RegistrableTypeModel>>(StringComparer.Ordinal);

                if (!claimsByKey.TryGetValue(key, out var claimants))
                {
                    claimsByKey.Add(key, claimants = []);
                }

                claimants.Add(type);
            }
        }

        if (claimsByKey is null)
        {
            return;
        }

        foreach (var message in types)
        {
            // Events broadcast — every handler runs, there is no contest to judge.
            if (!message.IsDispatchableMessage || (message.IsEvent && !message.IsCommand && !message.IsQuery))
            {
                continue;
            }

            var messageKey = TypeExpressions.DefinitionKey(message.TypeofExpression);

            if (claimsByKey.TryGetValue(messageKey, out var directClaimants) && directClaimants.Count > 1)
            {
                ReportContest(context, message, directClaimants, "direct");
                continue;
            }

            // The covariant arm: only when the direct level is provably empty — no
            // provable direct claim of any kind (bulk or literal Register), and no
            // opaque registration that could hide one. Contract variance never applies
            // to value types, so their assignable keys stay out, mirroring IsCovered.
            if (directClaimants is { Count: 1 }
                || hasOpaqueRegistrations
                || message.IsValueType
                || handlerMessageKeys.Contains(messageKey))
            {
                continue;
            }

            List<RegistrableTypeModel>? covariantClaimants = null;

            foreach (var assignableKey in message.AssignableKeys)
            {
                if (!claimsByKey.TryGetValue(TypeExpressions.DefinitionKey(assignableKey), out var claimants))
                {
                    continue;
                }

                foreach (var claimant in claimants)
                {
                    if ((covariantClaimants ??= []).Count == 0 || !covariantClaimants.Contains(claimant))
                    {
                        covariantClaimants.Add(claimant);
                    }
                }
            }

            if (covariantClaimants is { Count: > 1 })
            {
                ReportContest(context, message, covariantClaimants, "covariant");
            }
        }
    }

    private static void ReportContest(
        SourceProductionContext context,
        RegistrableTypeModel message,
        List<RegistrableTypeModel> claimants,
        string level)
    {
        var names = new StringBuilder();

        foreach (var claimant in claimants)
        {
            names.Append(names.Length == 0 ? string.Empty : ", ").Append(claimant.DisplayName);
        }

        context.ReportDiagnostic(Diagnostic.Create(
            GeneratorDiagnostics.ContestedMainHandlers,
            message.InfoLocation?.ToLocation(),
            message.DisplayName,
            claimants.Count,
            level,
            names.ToString()));
    }

    /// <summary>
    ///     Whether a message type is covered by at least one main-handler registration —
    ///     directly, or through an assignable base/interface registration (the runtime's
    ///     covariant resolution). Contract variance never applies to value types, so their
    ///     assignable keys stay out of the check.
    /// </summary>
    private static bool IsCovered(
        string typeKey,
        ImmutableArray<string> assignableKeys,
        bool isValueType,
        HashSet<string> handlerMessageKeys)
    {
        if (handlerMessageKeys.Contains(typeKey))
        {
            return true;
        }

        if (isValueType)
        {
            return false;
        }

        foreach (var key in assignableKeys)
        {
            if (handlerMessageKeys.Contains(TypeExpressions.DefinitionKey(key)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Every message key a dispatch site can deliver to: each site's static type, its
    ///     supertypes (a covariant handler registration there is reachable), and the same
    ///     closure for every dispatchable subtype a runtime instance could actually be.
    /// </summary>
    private static HashSet<string> CollectReachedKeys(
        ImmutableArray<DispatchSiteModel> sourceSites,
        ImmutableArray<DispatchSiteModel> manifestSites,
        Dictionary<string, List<RegistrableTypeModel>> subtypesByKey)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);

        AddSites(sourceSites);
        AddSites(manifestSites);

        return reached;

        void AddSites(ImmutableArray<DispatchSiteModel> sites)
        {
            foreach (var site in sites)
            {
                var siteKey = TypeExpressions.DefinitionKey(site.MessageTypeExpression);
                reached.Add(siteKey);

                foreach (var key in site.AssignableKeys)
                {
                    reached.Add(TypeExpressions.DefinitionKey(key));
                }

                if (!subtypesByKey.TryGetValue(siteKey, out var subtypes))
                {
                    continue;
                }

                foreach (var subtype in subtypes)
                {
                    reached.Add(TypeExpressions.DefinitionKey(subtype.TypeofExpression));

                    foreach (var key in subtype.AssignableKeys)
                    {
                        reached.Add(TypeExpressions.DefinitionKey(key));
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Reports ERGO007 for handlers no dispatch site can reach — or, when the trim is
    ///     on, reports ERGO008 instead for the handlers it excludes and returns their
    ///     type expressions. Keyed-discovery types are exempt (their participation is
    ///     deliberately conditional), and only pure main-handler types are trimmable: a
    ///     type that also carries interceptor contracts stays registered for those.
    /// </summary>
    private static HashSet<string>? ReportUnreachableHandlers(
        SourceProductionContext context,
        List<RegistrableTypeModel> types,
        HashSet<string> reachedKeys,
        bool trimUnusedHandlers)
    {
        HashSet<string>? trimmedExpressions = null;

        foreach (var type in types)
        {
            if (!type.IsAccessible || !type.DiscoveryKeys.IsEmpty)
            {
                continue;
            }

            var hasMainHandler = false;
            var isPureMainHandler = true;
            string? unreachedMessage = null;

            foreach (var descriptor in type.Descriptors)
            {
                if (descriptor.Kind != DescriptorKind.MainHandler)
                {
                    isPureMainHandler = false;
                    continue;
                }

                hasMainHandler = true;

                if (reachedKeys.Contains(TypeExpressions.DefinitionKey(descriptor.MessageTypeExpression)))
                {
                    unreachedMessage = null;
                    break;
                }

                unreachedMessage ??= descriptor.MessageTypeExpression;
            }

            if (!hasMainHandler || unreachedMessage is null)
            {
                continue;
            }

            var originSuffix = type.ReferencedAssemblyName is { } assemblyName
                ? $" (handler declared in referenced assembly '{assemblyName}')"
                : string.Empty;

            if (trimUnusedHandlers && isPureMainHandler)
            {
                (trimmedExpressions ??= new HashSet<string>(StringComparer.Ordinal)).Add(type.TypeofExpression);

                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.TrimmedUnreachableHandler,
                    type.ReferencedAssemblyName is null ? type.InfoLocation?.ToLocation() : null,
                    type.DisplayName));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.UnreachableHandler,
                    type.ReferencedAssemblyName is null ? type.InfoLocation?.ToLocation() : null,
                    type.DisplayName,
                    TypeExpressions.StripGlobalPrefix(unreachedMessage),
                    originSuffix));
            }
        }

        return trimmedExpressions;
    }
}
