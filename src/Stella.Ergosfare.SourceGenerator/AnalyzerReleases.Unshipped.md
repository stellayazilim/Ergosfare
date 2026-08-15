; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ERGOSG001 | Usage | Warning | Registrable type is not accessible from generated registration code
ERGOSG002 | Usage | Warning | Registrable type in a referenced assembly is not visible to generated registration code
ERGOSG003 | Performance | Info | Multiple public constructors keep the handler on the container path
ERGOSG004 | Usage | Info | [FromServices] has no effect on constructor parameters
ERGOSG005 | Usage | Error | Dispatch can never reach a handler
ERGOSG006 | Usage | Warning | Only subtypes of the dispatched static type are handled
ERGOSG007 | Usage | Warning | No dispatch site can reach this handler
ERGOSG008 | Performance | Info | Unreachable handler excluded from generated registration
ERGOSG009 | Usage | Disabled | Dispatch site's static message type is opaque
ERGOSG010 | Usage | Error | Multiple main handlers claim the same message at the same level
ERGOSG011 | Usage | Error | Result adapter annotation can never bind
ERGOSG012 | Usage | Error | Conflicting result-adapter annotations
ERGOSG013 | Usage | Error | Result type is not served by the configured default result adapter
ERGOSG014 | Usage | Warning | Opted-out message keeps a throwing pipeline
ERGOSG015 | Usage | Warning | Plugin assembly is excluded from reference scanning by the reserved name prefix
ERGOSG016 | Usage | Warning | Generic participant closes over no message and never executes
ERGOSG017 | Usage | Warning | Plugin service cannot receive the plugin's options
