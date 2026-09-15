using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.Symbols;

namespace Stella.Ergosfare.SourceGenerator;

public sealed partial class ErgosfareRegistrationGenerator
{
    private static INamedTypeSymbol? ReadBuilderReceiver(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken ct)
    {
        while (true)
        {
            if (semanticModel.GetTypeInfo(expression, ct).Type is INamedTypeSymbol { TypeKind: not TypeKind.Error } type)
                return type;
            if (expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member }
                && member.Name.Identifier.ValueText is "AddGenerated" or "Register")
                expression = member.Expression;
            else return null;
        }
    }

    private static INamedTypeSymbol? ReadUnboundExplicitSelection(SemanticModel semanticModel, InvocationExpressionSyntax call, CancellationToken ct)
    {
        if (call.Expression is not MemberAccessExpressionSyntax member || member.Name.Identifier.ValueText != "Register"
            || ReadBuilderReceiver(semanticModel, member.Expression, ct) is not { } builder || !IsErgosfareRegistrationSurface(builder))
            return null;
        if (member.Name is GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } generic)
            return semanticModel.GetTypeInfo(generic.TypeArgumentList.Arguments[0], ct).Type as INamedTypeSymbol;
        return call.ArgumentList.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax typeOf
            ? semanticModel.GetTypeInfo(typeOf.Type, ct).Type as INamedTypeSymbol : null;
    }

    // Explicit type selections remain readable when automatic reference scanning is off.
    private static ImmutableArray<RegistrableTypeModel> ReadExplicitCandidates(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var semanticModel = ctx.SemanticModel;
        var call = (InvocationExpressionSyntax)ctx.Node;
        var type = ReadUnboundExplicitSelection(semanticModel, call, ct);
        if (type is null && semanticModel.GetSymbolInfo(call, ct).Symbol is IMethodSymbol method
            && method.Name == "Register" && IsErgosfareRegistrationSurface(method.ContainingType))
            type = method is { IsGenericMethod: true, TypeArguments.Length: 1 }
                ? method.TypeArguments[0] as INamedTypeSymbol
                : FindMessageArgument(call, method)?.Expression is TypeOfExpressionSyntax expression
                    ? semanticModel.GetTypeInfo(expression.Type, ct).Type as INamedTypeSymbol : null;
        if (type is null || type.TypeKind == TypeKind.Error) return ImmutableArray<RegistrableTypeModel>.Empty;
        var models = ImmutableArray.CreateBuilder<RegistrableTypeModel>();
        Add(type);
        foreach (var contract in type.AllInterfaces)
            if (contract.TypeArguments.Length > 0 && contract.TypeArguments[0] is INamedTypeSymbol message)
                Add(message);
        return models.ToImmutable();

        void Add(INamedTypeSymbol symbol)
        {
            var assembly = symbol.ContainingAssembly;
            if (ReferenceScanner.TryCreateReferencedModel(symbol, assembly.Name,
                    assembly.GivesAccessTo(semanticModel.Compilation.Assembly),
                    ParticipantAttributes.HasExcludeFromDiscovery(assembly.GetAttributes())) is { } model)
                models.Add(model);
        }
    }

    // Read literal selections from the public module builders.
    private static RegistrationSiteModel? TryReadBulkSelection(SemanticModel semanticModel,
        InvocationExpressionSyntax call, CancellationToken ct)
    {
        if (call.Expression is not MemberAccessExpressionSyntax member
            || member.Name.Identifier.ValueText != "AddGenerated") return null;

        var receiver = ReadBuilderReceiver(semanticModel, member.Expression, ct);
        if (receiver is null || !IsErgosfareRegistrationSurface(receiver)) return null;
        byte module = receiver.Name switch { "CommandModuleBuilder" => 1, "QueryModuleBuilder" => 2, "EventModuleBuilder" => 3, _ => 255 };
        if (module == 255) return null;

        string? pattern = "";
        if (call.ArgumentList.Arguments.Count > 0)
        {
            var constant = semanticModel.GetConstantValue(call.ArgumentList.Arguments[0].Expression, ct);
            pattern = constant.HasValue ? constant.Value as string : null;
        }
        return new RegistrationSiteModel
        {
            DiscoveryPattern = pattern,
            Module = module,
            TypeMetadataName = null,
            MainHandlerMessageKeys = ImmutableArray<string>.Empty,
            IsOpaque = pattern is null,
            UnknownTypeLocation = pattern is null ? LocationInfo.From(call) : null,
        };
    }
}
