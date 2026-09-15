using System.Text;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator;

internal static partial class RegistrationEmitter
{
    private static void StartMember(StringBuilder sb, ref bool wroteMember)
    {
        if (wroteMember) sb.AppendLine();
        wroteMember = true;
    }

    private static void EmitParticipantRegistrations(StringBuilder sb, IReadOnlyList<RegistrableTypeModel> types,
        IReadOnlyList<StagedPlanModel> plans)
    {
        var used = new HashSet<string>(plans.SelectMany(plan => plan.Handlers.Select(h => h.TypeExpression)
            .Concat(plan.IndirectHandlers.Select(h => h.TypeExpression))
            .Concat(plan.PreCalls.Concat(plan.PostCalls).Concat(plan.ExceptionCalls).Concat(plan.FinalCalls)
                .Select(call => call.TypeExpression))), StringComparer.Ordinal);
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void RegisterParticipantFactories()");
        sb.AppendLine("        {");
        foreach (var type in types)
        {
            if (type.Descriptors.IsEmpty || type.ServiceConstructionExpression is null && !used.Contains(type.TypeofExpression)) continue;
            sb.Append("            ").Append(PlanRegistryFullName).Append(".AddParticipantFactory(typeof(")
                .Append(type.TypeofExpression).Append("), ")
                .Append(type.ServiceConstructionExpression is { } construction ? "static provider => " + construction : "null")
                .AppendLine(");");
        }
        foreach (var group in types.Where(t => t.MonomorphizedFrom is not null).GroupBy(t => t.MonomorphizedFrom))
        {
            sb.Append("            ").Append(PlanRegistryFullName).Append(".AddSelectionExpansion(typeof(")
                .Append(group.Key).Append("), new global::System.Type[] { ");
            foreach (var type in group) sb.Append("typeof(").Append(type.TypeofExpression).Append("), ");
            sb.AppendLine("});");
        }
        sb.AppendLine("        }");
    }
}
