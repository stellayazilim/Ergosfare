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
/// The dispatch-site half of the generator.
/// </summary>
/// <remarks>
/// It finds the mediator dispatches in this compilation, gathers the manifests referenced
/// assemblies recorded, and — in a composition root, where the closure is complete — judges
/// what can reach what: dead dispatches as ERGO005 and ERGO006, unreachable handlers as
/// ERGO007, the opt-in trim as ERGO008, and opaque sites as ERGO009.
/// </remarks>
public sealed partial class ErgosfareRegistrationGenerator
{
    private const string CommandMediatorInterfaceName = "ICommandMediator";
    private const string QueryMediatorInterfaceName = "IQueryMediator";
    private const string EventMediatorInterfaceName = "IEventMediator";
    private const string MessageMediatorInterfaceName = "IMessageMediator";


    private const string CompositionRootBuildProperty = "build_property.ErgosfareCompositionRoot";
    private const string TrimUnusedHandlersBuildProperty = "build_property.ErgosfareTrimUnusedHandlers";

    /// <summary>
    /// Reports whether a node could be a dispatch, on its syntax alone.
    /// </summary>
    /// <param name="node">The node to test.</param>
    /// <returns>
    /// <c>true</c> for an invocation with arguments whose name is one of the mediator's
    /// dispatch methods.
    /// </returns>
    /// <remarks>
    /// The cheap first pass; whether it really binds to a mediator is settled later, against
    /// the semantic model.
    /// </remarks>
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
    /// Reports whether a node could be a registration, on its syntax alone.
    /// </summary>
    /// <param name="node">The node to test.</param>
    /// <returns>
    /// <c>true</c> for an invocation whose name is one of the registration methods.
    /// </returns>
    /// <remarks>
    /// <c>RegisterGenerated</c> and <c>RegisterAll</c> are left out on purpose: they collect
    /// in bulk what discovery already counts as evidence.
    /// </remarks>
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
    /// Reads one candidate invocation as a registration.
    /// </summary>
    /// <param name="ctx">The invocation to read.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>
    /// The site's model, or <c>null</c> when the invocation binds to no Ergosfare
    /// registration surface, or is the module-container <c>Register(IModule)</c> whose real
    /// registrations are the builder calls inside it.
    /// </returns>
    /// <remarks>
    /// A hand-written registration reaches the container the same way <c>RegisterGenerated</c>
    /// does, so a type it names outright contributes its main-handler contracts as coverage
    /// evidence. A type it cannot name comes back opaque, which suspends the dead-dispatch
    /// judgment.
    /// </remarks>
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
                // A batch assembled at run time is a value, not a list of type arguments, and
                // nothing here can say which types it carries.
                return OpaqueRegistration();

            case "Register" when method is { IsGenericMethod: true, TypeArguments.Length: 1 }:
                // A type argument that is itself a type parameter, as in Register<T>() inside
                // a generic method, names a different type per instantiation — and this
                // compilation cannot list them.
                return method.TypeArguments[0] is INamedTypeSymbol genericArgument
                    ? EvidenceRegistration(genericArgument)
                    : UnknownTypeRegistration(invocation);

            case "Register" when method.Parameters.Length >= 1:
            {
                var parameterType = method.Parameters[0].Type;

                if (parameterType is INamedTypeSymbol { Name: "Type" } typeParameter
                    && SymbolNaming.IsInNamespace(typeParameter, "System"))
                {
                    // Register(typeof(X)) names its type outright; any other Type-valued
                    // argument is decided at run time.
                    if (FindMessageArgument(invocation, method) is { } argument
                        && UnwrapConversions(argument.Expression) is TypeOfExpressionSyntax typeOf
                        && ctx.SemanticModel.GetTypeInfo(typeOf.Type, ct).Type is INamedTypeSymbol literal
                        && literal is not IErrorTypeSymbol)
                    {
                        return EvidenceRegistration(literal);
                    }

                    return UnknownTypeRegistration(invocation);
                }

                // Register(IModule) and its siblings take a container rather than a type; the
                // builder calls inside the module are the registrations, and they are visible
                // on their own.
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

        // Opaque, and a defect: ERGO018 reports it at the call. Opaque all the same, so the
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
    /// Builds the evidence a named registration contributes: the messages its main handlers
    /// claim.
    /// </summary>
    /// <param name="registered">The registered type.</param>
    /// <returns>The site's model, opaque when the type is generic.</returns>
    /// <remarks>
    /// A generic registration stays opaque: the runtime builds its descriptors reflectively
    /// per closed form, and <see cref="ContractReader.BuildDescriptors"/> answers nothing for
    /// it. The type is known and its evidence is not, which is a different thing from the
    /// unknown type ERGO018 reports.
    /// </remarks>
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

                    // Nothing to report: the type is known, only its evidence is missing.
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
    /// Reports whether a type is one of Ergosfare's registration surfaces.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns>
    /// <c>true</c> for the module builders, their dependency-injection siblings, and the
    /// registry itself.
    /// </returns>
    private static bool IsErgosfareRegistrationSurface(INamedTypeSymbol type)
        => SymbolNaming.IsInNamespace(type, "Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection")
           || SymbolNaming.IsInNamespace(type, "Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection")
           || SymbolNaming.IsInNamespace(type, "Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection")
           || SymbolNaming.IsInNamespace(type, "Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection");

    /// <summary>
    /// Reads one candidate invocation as a dispatch.
    /// </summary>
    /// <param name="ctx">The invocation to read.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>
    /// The site's model, or <c>null</c> when the invocation binds to no mediator dispatch.
    /// </returns>
    /// <remarks>
    /// Casts and conversions around the message argument are looked through, so the recorded
    /// type is the expression's own: <c>SendAsync((ICommand)x)</c> records what <c>x</c> is
    /// declared as, not what it was cast to.
    /// </remarks>
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
    /// Reads the group set a dispatch names.
    /// </summary>
    /// <param name="ctx">The syntax context the invocation belongs to.</param>
    /// <param name="invocation">The dispatch to read.</param>
    /// <param name="method">The method it binds to.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <param name="groups">The named groups, normalized; empty when the call names none.</param>
    /// <param name="unprovable">Set when the set could not be read.</param>
    /// <remarks>
    /// Every element has to be a literal the compiler can fold. Naming no set at all is the
    /// empty set, which means the default group — a group set like any other. One indirection
    /// is followed: a reference to a field or local whose initializer is itself readable, the
    /// documented idiom being a <c>static readonly GroupSet</c> defined once and reused.
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
    /// Finds the argument bound to a dispatch method's group parameter.
    /// </summary>
    /// <param name="invocation">The dispatch to read.</param>
    /// <param name="method">The method it binds to.</param>
    /// <returns>
    /// The argument, or <c>null</c> when the method takes no group parameter or the call
    /// omits it.
    /// </returns>
    /// <remarks>
    /// Named arguments are honored, so the parameter is found wherever the call put it.
    /// </remarks>
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

    /// <summary>
    /// Reports whether a parameter is a dispatch's group parameter.
    /// </summary>
    /// <param name="parameter">The parameter to test.</param>
    /// <returns><c>true</c> when it carries the group set.</returns>
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
    /// Collects the literal names behind a group expression.
    /// </summary>
    /// <param name="ctx">The syntax context the expression belongs to.</param>
    /// <param name="expression">The expression to read.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <param name="names">The list names are added to.</param>
    /// <param name="depth">How many named references have been followed so far.</param>
    /// <returns><c>false</c> the moment something cannot be read.</returns>
    private static bool TryReadGroupNames(
        GeneratorSyntaxContext ctx,
        ExpressionSyntax expression,
        CancellationToken ct,
        List<string> names,
        int depth)
    {
        // One hop through a named reference and no further: a chain of aliases is not an
        // idiom worth chasing, and the limit is what keeps this finite.
        if (depth > 1)
        {
            return false;
        }

        switch (expression)
        {
            // null or default: no set named, so the default group.
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NullLiteralExpression):
            case LiteralExpressionSyntax when expression.IsKind(SyntaxKind.DefaultLiteralExpression):
                return true;

            // GroupSet.Of("a", "b"), the spelling the documentation recommends.
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

            // GroupSet.Empty, or a reference to a field or local holding a readable set.
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
    /// Follows a field or local back to its initializer and reads that instead.
    /// </summary>
    /// <param name="ctx">The syntax context the reference belongs to.</param>
    /// <param name="symbol">The field or local to follow.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <param name="names">The list names are added to.</param>
    /// <param name="depth">How many named references have been followed so far.</param>
    /// <returns><c>true</c> when the initializer could be read.</returns>
    /// <remarks>
    /// Only a single unambiguous declaration counts, and only one this compilation can see: a
    /// set assembled elsewhere stays unreadable.
    /// </remarks>
    private static bool TryReadInitializer(
        GeneratorSyntaxContext ctx,
        ISymbol symbol,
        CancellationToken ct,
        List<string> names,
        int depth)
    {
        if (symbol is IFieldSymbol { IsReadOnly: false, IsConst: false })
        {
            // A writable field can be holding anything by the time the dispatch runs.
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

        // The initializer needs its own semantic model: a field declared in another file
        // belongs to a different syntax tree, which the dispatch's model cannot answer for.
        var initializerContext = initializer.SyntaxTree == ctx.SemanticModel.SyntaxTree
            ? ctx
            : default;

        // GeneratorSyntaxContext is a struct, so the default above really does carry a null
        // SemanticModel, whatever its annotation claims. Without this check, the next line
        // dereferences that null.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (initializerContext.SemanticModel is null)
        {
            return false;
        }

        return TryReadGroupNames(initializerContext, UnwrapConversions(initializer), ct, names, depth + 1);
    }

    /// <summary>
    /// Reads a list of expressions as group names.
    /// </summary>
    /// <param name="ctx">The syntax context the expressions belong to.</param>
    /// <param name="expressions">The expressions to read.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <param name="names">The list names are added to.</param>
    /// <returns><c>false</c> the moment one of them cannot be read.</returns>
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
    /// Reads one expression as a group name.
    /// </summary>
    /// <param name="ctx">The syntax context the expression belongs to.</param>
    /// <param name="expression">The expression to read.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <param name="names">The list the name is added to.</param>
    /// <returns><c>true</c> when the compiler folds it to a string constant.</returns>
    /// <remarks>
    /// Which covers a literal, a <c>const</c> field and constant concatenation alike.
    /// </remarks>
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
    /// Decides which dispatch surface a bound method belongs to.
    /// </summary>
    /// <param name="method">The method the call binds to.</param>
    /// <returns>
    /// The kind of dispatch, or <c>null</c> when the method is not one.
    /// </returns>
    /// <remarks>
    /// Matched on the mediator interface itself, or — for a call through a concrete mediator
    /// — on any mediator interface the containing type implements.
    /// </remarks>
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

    /// <summary>
    /// Matches one interface and method name against the dispatch surfaces.
    /// </summary>
    /// <param name="type">The interface to match.</param>
    /// <param name="methodName">The called method's name.</param>
    /// <returns>The kind of dispatch, or <c>null</c> when the pair is not one.</returns>
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
    /// Finds the argument bound to a method's first parameter, which every dispatch surface
    /// takes the message in.
    /// </summary>
    /// <param name="invocation">The call to read.</param>
    /// <param name="method">The method it binds to.</param>
    /// <returns>The argument, or <c>null</c> when the call passes none.</returns>
    /// <remarks>
    /// Named arguments are honored, so the parameter is found wherever the call put it.
    /// </remarks>
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
    /// Strips casts, <c>as</c> conversions, parentheses and null-forgiveness from an
    /// expression.
    /// </summary>
    /// <param name="expression">The expression to strip.</param>
    /// <returns>The expression underneath, so its own type is what gets recorded.</returns>
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
    /// Builds the site model for one static message type.
    /// </summary>
    /// <param name="type">The message expression's static type.</param>
    /// <param name="kind">The dispatch surface the call went through.</param>
    /// <param name="location">Where to report findings about this site.</param>
    /// <param name="referencedAssemblyName">
    /// The assembly whose manifest recorded the site, or <c>null</c> for one in this
    /// compilation.
    /// </param>
    /// <returns>
    /// The model, or <c>null</c> when the site is outside the closed world.
    /// </returns>
    /// <remarks>
    /// A type parameter resolves to its first named constraint. An expression with no usable
    /// static type — an untyped null, an unconstrained type parameter — falls back to the
    /// surface's marker interface, and the site then reaches every message of that kind. A
    /// core-mediator site whose static type carries no Ergosfare marker gets no model at all:
    /// dispatching a plain message is outside what any of this can speak to.
    /// </remarks>
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
                // A markerless type through the core mediator is a plain message, and the
                // closed-world judgment has nothing to say about one.
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
    /// Finds a type parameter's first named constraint, preferring a class over an interface.
    /// </summary>
    /// <param name="parameter">The type parameter to read.</param>
    /// <returns>The constraint, or <c>null</c> when the parameter has none.</returns>
    /// <remarks>
    /// The closest thing to a static type a generic dispatch wrapper leaves behind.
    /// </remarks>
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
    /// Reports whether a static type says nothing about which concrete message is dispatched.
    /// </summary>
    /// <param name="type">The static type to test.</param>
    /// <returns>
    /// <c>true</c> for <c>object</c>, <c>IMessage</c> and the module markers, at any arity —
    /// a result-generic marker names a result, never a concrete message.
    /// </returns>
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
    /// Builds the opaque marker-interface site of a dispatch surface.
    /// </summary>
    /// <param name="kind">The dispatch surface the call went through.</param>
    /// <param name="location">Where to report findings about this site.</param>
    /// <param name="referencedAssemblyName">
    /// The assembly whose manifest recorded the site, or <c>null</c> for one in this
    /// compilation.
    /// </param>
    /// <returns>A site reaching every message of that surface's kind.</returns>
    /// <remarks>
    /// Recorded when no more specific static type can be recovered, so reachability still
    /// sees that the dispatch is there.
    /// </remarks>
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
    /// Gathers the dispatch manifests the referenced assemblies recorded.
    /// </summary>
    /// <param name="compilation">The compilation whose references are read.</param>
    /// <param name="ct">Cancels the scan.</param>
    /// <returns>
    /// The recorded sites and registrations, and whether the picture they give is complete.
    /// </returns>
    /// <remarks>
    /// Each recorded static type is resolved back to a symbol, so assignability is worked out
    /// here rather than taken on trust from a string. An Ergosfare-referencing assembly
    /// carrying no manifest marker sets
    /// <see cref="DispatchManifestScanResult.HasUnknownSiteAssemblies"/>; the ones skipped by
    /// the reserved-prefix rule are Ergosfare's own and do not.
    /// </remarks>
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
                        // A site whose static type this compilation cannot resolve is a site
                        // the judgment cannot see, which is the same position a missing
                        // manifest leaves it in.
                        hasUnknownSiteAssemblies = true;
                        continue;
                    }

                    if (BuildSiteModel(symbol, kind, location: null, assembly.Name) is { } value)
                    {
                        // Opacity and the group set travel in the manifest: the generator that
                        // recorded them saw the original expression, and the resolved symbol
                        // alone cannot reconstruct either.
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
                        // Evidence that cannot be resolved is missing evidence, and reading it
                        // as an opaque registration suspends the dead-dispatch judgment
                        // rather than letting it fire on a gap.
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
    /// Reads the group set a manifest site recorded.
    /// </summary>
    /// <param name="attribute">The recorded site.</param>
    /// <returns>The named groups, normalized; empty when it recorded none.</returns>
    /// <remarks>
    /// Absent or empty means the site named no set — or named one the recording generator
    /// could not read, which it writes down the same way. Neither keys a plan here.
    /// </remarks>
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

    /// <summary>
    /// Reports whether a compilation produces an executable.
    /// </summary>
    /// <param name="outputKind">The compilation's output kind.</param>
    /// <returns><c>true</c> for an application of any kind.</returns>
    /// <remarks>
    /// The default answer to whether this compilation is a composition root, and so whether
    /// its closure is complete enough to judge.
    /// </remarks>
    private static bool IsExecutableOutputKind(OutputKind outputKind)
        => outputKind is OutputKind.ConsoleApplication
            or OutputKind.WindowsApplication
            or OutputKind.WindowsRuntimeApplication;

    /// <summary>
    /// Reads the <c>ErgosfareCompositionRoot</c> property.
    /// </summary>
    /// <param name="provider">The build properties to read.</param>
    /// <returns>
    /// What the project says, or <c>null</c> when it says nothing — the output kind then
    /// decides.
    /// </returns>
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

    /// <summary>
    /// Reads the <c>ErgosfareTrimUnusedHandlers</c> property.
    /// </summary>
    /// <param name="provider">The build properties to read.</param>
    /// <returns><c>true</c> when the project opted into the trim.</returns>
    private static bool ReadTrimUnusedHandlers(AnalyzerConfigOptionsProvider provider)
        => provider.GlobalOptions.TryGetValue(TrimUnusedHandlersBuildProperty, out var value)
           && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reports the reachability diagnostics and applies the opt-in handler trim.
    /// </summary>
    /// <param name="context">The context diagnostics are reported to.</param>
    /// <param name="types">The discovered types.</param>
    /// <param name="excludedShadows">The types hidden from discovery.</param>
    /// <param name="sourceSites">The dispatches in this compilation.</param>
    /// <param name="registrationSites">The registration calls in this compilation.</param>
    /// <param name="manifestScan">What the referenced assemblies' manifests recorded.</param>
    /// <param name="inputs">The settings the judgment reads.</param>
    /// <returns>The type list emission proceeds with, reduced if anything was trimmed.</returns>
    /// <remarks>
    /// ERGO009 is local and always reported. Every verdict about the whole closure needs a
    /// composition root with reference scanning on, and the unreachable-handler side needs
    /// every assembly in that closure to carry a manifest as well.
    /// </remarks>
    internal static List<RegistrableTypeModel> ApplyDispatchJudgment(
        SourceProductionContext context,
        List<RegistrableTypeModel> types,
        List<RegistrableTypeModel> excludedShadows,
        ImmutableArray<DispatchSiteModel> sourceSites,
        ImmutableArray<RegistrationSiteModel> registrationSites,
        DispatchManifestScanResult manifestScan,
        JudgmentInputs inputs)
    {
        // Purely local: nothing about the composition is needed to see that a site's static
        // type says nothing.
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

        // The coverage evidence, from every path that feeds the registry: discovery in bulk
        // through RegisterGenerated, and hand-written Register calls one type at a time —
        // the same registry, trusted the same way. One opaque registration anywhere in the
        // closure makes the evidence incomplete.
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

        // Indexes over the composition, hidden types included: they can still be dispatched,
        // their coverage simply has to come from a hand-written registration. Every
        // comparison runs on definition-normalized keys, so a constructed generic matches its
        // definition rather than being missed.
        var modelsByKey = new Dictionary<string, RegistrableTypeModel>(StringComparer.Ordinal);
        var subtypesByKey = new Dictionary<string, List<RegistrableTypeModel>>(StringComparer.Ordinal);

        IndexCompositionTypes(types, handlerMessageKeys, modelsByKey, subtypesByKey, collectHandlerEvidence: true);
        IndexCompositionTypes(excludedShadows, handlerMessageKeys, modelsByKey, subtypesByKey, collectHandlerEvidence: false);

        // The per-site verdicts, ERGO005 and ERGO006 — reached only while every registration
        // in the closure is one this compilation could read.
        if (!hasOpaqueRegistrations)
        {
            JudgeSites(context, sourceSites, handlerMessageKeys, modelsByKey, subtypesByKey);
            JudgeSites(context, manifestScan.Sites, handlerMessageKeys, modelsByKey, subtypesByKey);
        }

        // The per-message verdict: two main handlers on one level break every container in
        // the process, so a provable contest fails the build. An unreadable registration
        // matters only to the covariant arm, where it could be adding the direct handler that
        // would settle things; nothing extra can settle a contest between two direct
        // handlers, so that arm is never suspended.
        JudgeContestedMainHandlers(context, types, handlerMessageKeys, hasOpaqueRegistrations);

        // The unreachable-handler side needs every dispatch in the closure, and one
        // Ergosfare-referencing assembly without a manifest is enough to make that set
        // incomplete. An unreadable registration does not matter here: registering something
        // creates no dispatch.
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
    /// Feeds one model list into the judgment's indexes.
    /// </summary>
    /// <param name="models">The models to index.</param>
    /// <param name="handlerMessageKeys">The set covered messages are added to.</param>
    /// <param name="modelsByKey">The map from message key to model.</param>
    /// <param name="subtypesByKey">The map from a type's key to the messages assignable to it.</param>
    /// <param name="collectHandlerEvidence">
    /// Whether these models' handler contracts count as coverage evidence.
    /// </param>
    /// <remarks>
    /// Only a discovered type's handler role counts on its own; a hidden type's counts solely
    /// through a hand-written registration naming it.
    /// </remarks>
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

    /// <summary>
    /// Judges each dispatch against the closure's coverage.
    /// </summary>
    /// <param name="context">The context diagnostics are reported to.</param>
    /// <param name="sites">The dispatches to judge.</param>
    /// <param name="handlerMessageKeys">The messages some handler covers.</param>
    /// <param name="modelsByKey">The map from message key to model.</param>
    /// <param name="subtypesByKey">The map from a type's key to the messages assignable to it.</param>
    /// <remarks>
    /// A dispatch nothing can serve is ERGO005; one where only a subtype is served is
    /// ERGO006.
    /// </remarks>
    private static void JudgeSites(
        SourceProductionContext context,
        ImmutableArray<DispatchSiteModel> sites,
        HashSet<string> handlerMessageKeys,
        Dictionary<string, RegistrableTypeModel> modelsByKey,
        Dictionary<string, List<RegistrableTypeModel>> subtypesByKey)
    {
        foreach (var site in sites)
        {
            // A generic message matches descriptors by definition only, which is too coarse to
            // carry a verdict either way.
            //
            // A publish is judged like any other dispatch. What a publish reaching nobody does
            // at run time is a separate question from whether any subscriber can ever match
            // it, and in a closed world a publish nothing subscribes to is dead — writing the
            // event and forgetting the subscriber being the ordinary way to get there.
            if (site.IsGenericMessage)
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
                    // A publish reaching nobody returns; every other lane throws. The verdict
                    // is the same either way, the consequence is not, and the message says
                    // which one the caller gets.
                    site.Kind == DispatchSiteKind.Event
                        ? "the publish is guaranteed to reach nobody"
                        : "the call is guaranteed to throw NoHandlerFoundException at runtime",
                    originSuffix));
            }
            else if (modelsByKey.TryGetValue(siteKey, out var selfModel) && selfModel.IsDispatchableMessage)
            {
                // The static type is itself a concrete message with no coverage while a
                // subtype has some, so an instance of exactly that type still fails.
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
    /// Reports ERGO010 for a message two main handlers claim on the same level.
    /// </summary>
    /// <param name="context">The context diagnostics are reported to.</param>
    /// <param name="types">The discovered types.</param>
    /// <param name="handlerMessageKeys">The messages some handler covers.</param>
    /// <param name="hasOpaqueRegistrations">
    /// Whether any registration in the closure could not be read.
    /// </param>
    /// <remarks>
    /// A contest is several direct claimants, or several covariant ones with no direct
    /// handler to beat them. Only handlers the bulk path registers unconditionally count —
    /// ungrouped, unkeyed and not hidden — because anything else is a container's choice that
    /// nothing here can prove two of are ever live together, which is what lets a deliberately
    /// contested keyed suite keep compiling. The covariant arm needs the direct level to be
    /// empty, which an unreadable registration could quietly fill, so that arm is suspended
    /// with the rest of the closure-wide judgment.
    /// </remarks>
    private static void JudgeContestedMainHandlers(
        SourceProductionContext context,
        List<RegistrableTypeModel> types,
        HashSet<string> handlerMessageKeys,
        bool hasOpaqueRegistrations)
    {
        // The claims per message, from handlers the bulk path registers unconditionally. One
        // entry per handler type per message: claiming it through several contracts still
        // makes one claimant.
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
            // An event broadcasts: every handler runs, so there is no contest to judge.
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

            // The covariant arm, reached only when the direct level is empty for certain: no
            // direct claim from either registration path, and no unreadable registration that
            // could be hiding one. Contract variance never applies to a value type, so its
            // assignable keys stay out here as they do in IsCovered.
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

    /// <summary>
    /// Reports one contest.
    /// </summary>
    /// <param name="context">The context diagnostics are reported to.</param>
    /// <param name="message">The contested message.</param>
    /// <param name="claimants">The handlers claiming it.</param>
    /// <param name="level">The priority level they claim it on.</param>
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
    /// Reports whether some main handler covers a message.
    /// </summary>
    /// <param name="typeKey">The message's normalized key.</param>
    /// <param name="assignableKeys">The keys of the types it is assignable to.</param>
    /// <param name="isValueType">Whether the message is a value type.</param>
    /// <param name="handlerMessageKeys">The messages some handler covers.</param>
    /// <returns><c>true</c> when a handler can serve it.</returns>
    /// <remarks>
    /// Directly, or through a registration against a base type or interface, the way the
    /// runtime resolves covariantly. Contract variance never applies to a value type, so its
    /// assignable keys stay out of the test.
    /// </remarks>
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
    /// Collects every message a dispatch in the closure can deliver to.
    /// </summary>
    /// <param name="sourceSites">The dispatches in this compilation.</param>
    /// <param name="manifestSites">The dispatches referenced assemblies recorded.</param>
    /// <param name="subtypesByKey">The map from a type's key to the messages assignable to it.</param>
    /// <returns>The reachable message keys.</returns>
    /// <remarks>
    /// Each site's own static type, the types it is assignable to — a handler registered
    /// there is reached covariantly — and the same again for every dispatchable subtype a
    /// runtime instance could turn out to be.
    /// </remarks>
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
    /// Reports the handlers no dispatch in the closure can reach.
    /// </summary>
    /// <param name="context">The context diagnostics are reported to.</param>
    /// <param name="types">The discovered types.</param>
    /// <param name="reachedKeys">The messages some dispatch can deliver.</param>
    /// <param name="trimUnusedHandlers">Whether the project opted into the trim.</param>
    /// <returns>
    /// The type expressions to leave out of the registration, or <c>null</c> when nothing is
    /// trimmed.
    /// </returns>
    /// <remarks>
    /// ERGO007 by default; with the trim on, ERGO008 for each handler it excludes instead. A
    /// keyed type is exempt, its taking part being conditional by design, and only a pure
    /// main-handler type can be trimmed — one that also carries interceptor contracts stays
    /// registered for those.
    /// </remarks>
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
