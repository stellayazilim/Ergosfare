namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// The handler-descriptor kinds the registry distinguishes; mirrors the runtime
/// descriptor-builder set.
/// </summary>
internal enum DescriptorKind : byte
{
    MainHandler,
    PreInterceptor,
    PostInterceptor,
    ExceptionInterceptor,
    FinalInterceptor,
}
