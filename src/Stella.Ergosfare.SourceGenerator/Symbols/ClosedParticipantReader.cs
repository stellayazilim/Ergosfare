using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>Collects concrete generic participants named in the compiled application.</summary>
internal static class ClosedParticipantReader
{
    internal static ImmutableArray<RegistrableTypeModel> Read(Compilation compilation, bool scanReferences, CancellationToken ct)
    {
        var known = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semantic = compilation.GetSemanticModel(tree);
            foreach (var syntax in tree.GetRoot(ct).DescendantNodes().OfType<GenericNameSyntax>())
            {
                ct.ThrowIfCancellationRequested();
                if (semantic.GetTypeInfo(syntax, ct).Type is INamedTypeSymbol type) Add(type);
            }
        }

        // Metadata preserves constructed base/interface signatures even when the consumer
        // does not spell the closed participant in its own source.
        if (scanReferences)
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                if (ReferenceScanner.ReferencesErgosfare(assembly)
                    && (!ReferenceScanner.IsErgosfareAssemblyName(assembly.Name)
                        || ReferenceScanner.HasForceScanReferencesOptIn(assembly)))
                    Visit(assembly.GlobalNamespace);

        var results = ImmutableArray.CreateBuilder<RegistrableTypeModel>();
        foreach (var type in known)
        {
            if (type.IsAbstract || type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
                || !type.IsGenericType || !SymbolNaming.IsFullyClosed(type)
                || !ConstructionAnalyzer.IsNameableClosedType(type, compilation.Assembly)) continue;
            if (Monomorphizer.CreateMonomorphizedModel(type, type.OriginalDefinition, compilation.Assembly) is { } model)
                results.Add(model);
        }
        return results.ToImmutable();

        void Add(INamedTypeSymbol type)
        {
            if (!known.Add(type)) return;
            foreach (var argument in type.TypeArguments)
                if (argument is INamedTypeSymbol named) Add(named);
            if (type.BaseType is { } parent) Add(parent);
            foreach (var contract in type.Interfaces) Add(contract);
        }

        void Visit(INamespaceOrTypeSymbol container)
        {
            foreach (var member in container.GetMembers())
            {
                ct.ThrowIfCancellationRequested();
                if (member is INamespaceSymbol ns) Visit(ns);
                else if (member is INamedTypeSymbol type)
                {
                    Add(type);
                    Visit(type);
                }
            }
        }
    }
}
