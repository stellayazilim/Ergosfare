using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stella.Ergosfare.SourceGenerator.Symbols;

namespace Stella.Ergosfare.SourceGenerator;

public sealed partial class ErgosfareRegistrationGenerator
{
    private static readonly DiagnosticDescriptor WrongRegistrationModule = new(
        "ERGO025", "Registration belongs to another module",
        "Type '{0}' cannot be selected by {1}; select it through its own module",
        "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static Diagnostic? ValidateRegistrationModule(GeneratorSyntaxContext context, CancellationToken token)
    {
        var call = (InvocationExpressionSyntax)context.Node;
        if (call.Expression is not MemberAccessExpressionSyntax member
            || ReadUnboundExplicitSelection(context.SemanticModel, call, token) is not { } type
            || ReadBuilderReceiver(context.SemanticModel, member.Expression, token) is not { } builder)
            return null;

        var definition = type.IsUnboundGenericType ? type.OriginalDefinition : type;
        ParticipantAttributes.GetMarkers(definition, out var command, out var query, out var @event);
        var valid = builder.Name switch
        {
            "CommandModuleBuilder" => command,
            "QueryModuleBuilder" => query,
            "EventModuleBuilder" => @event || !definition.AllInterfaces.Any(iface =>
                SymbolNaming.IsInNamespace(iface, "Stella.Ergosfare.Core.Abstractions.Handlers")),
            _ => true,
        };
        return valid ? null : Diagnostic.Create(WrongRegistrationModule, call.GetLocation(), type.ToDisplayString(), builder.Name);
    }
}
