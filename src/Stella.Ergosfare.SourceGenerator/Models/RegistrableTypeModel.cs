namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     Value-equatable projection of a registrable construct discovered in the compilation:
///     any user-declared type assignable to one of the module marker interfaces
///     (<c>ICommand</c>, <c>IQuery</c>, <c>IEvent</c>). Handlers and interceptors inherit
///     the marker through their contract interfaces, so a single marker check covers
///     messages, handlers and interceptors alike — mirroring what the reflection-based
///     <c>RegisterFromAssembly</c> discovers at runtime.
/// </summary>
/// <param name="TypeofExpression">
///     The <c>typeof</c> argument for the type, fully qualified with <c>global::</c>;
///     generic definitions use the unbound form (e.g. <c>global::App.Handler&lt;&gt;</c>).
/// </param>
/// <param name="DisplayName">Human-readable type name used in diagnostics.</param>
/// <param name="IsCommand">Whether the type is assignable to the command module marker.</param>
/// <param name="IsQuery">Whether the type is assignable to the query module marker.</param>
/// <param name="IsEvent">Whether the type is assignable to the event module marker.</param>
/// <param name="IsAccessible">
///     Whether generated code (a sibling type in the same assembly) can reference the type;
///     private/protected nested and file-local types cannot be registered and are reported
///     via <c>ERGOSG001</c> instead.
/// </param>
/// <param name="Location">Declaration location, captured only for inaccessible types.</param>
internal readonly record struct RegistrableTypeModel(
    string TypeofExpression,
    string DisplayName,
    bool IsCommand,
    bool IsQuery,
    bool IsEvent,
    bool IsAccessible,
    LocationInfo? Location);
