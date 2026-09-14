namespace Stella.Ergosfare.Core.Abstractions.Planning;

/// <summary>Compiler-only selection exported by a configuration method. Generated automatically.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedSelectionAttribute(string method, string? typeExpression, byte module, string? pattern) : Attribute
{
    public string Method { get; } = method;
    public string? TypeExpression { get; } = typeExpression;
    public byte Module { get; } = module;
    public string? Pattern { get; } = pattern;
}

/// <summary>Compiler-only edge between configuration methods. Generated automatically.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedSelectionCallAttribute(string caller, string targetAssembly, string targetMethod) : Attribute
{
    public string Caller { get; } = caller;
    public string TargetAssembly { get; } = targetAssembly;
    public string TargetMethod { get; } = targetMethod;
}
