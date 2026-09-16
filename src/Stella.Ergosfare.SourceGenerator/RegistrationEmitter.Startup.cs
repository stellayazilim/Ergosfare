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

    private static void EmitParticipantRegistrations(StringBuilder sb, IReadOnlyList<RegistrableTypeModel> types)
    {
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void RegisterParticipantTypes()");
        sb.AppendLine("        {");
        foreach (var type in types)
        {
            if (type.Descriptors.IsEmpty || !type.CanRegisterParticipant) continue;
            sb.Append("            ").Append(PlanRegistryFullName).Append(".AddParticipant<")
                .Append(type.TypeofExpression).AppendLine(">();");
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
