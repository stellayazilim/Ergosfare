; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ERGO001 | Usage | Warning | Registrable type is not accessible from generated registration code
ERGO002 | Usage | Warning | Registrable type in a referenced assembly is not visible to generated registration code
ERGO003 | Performance | Info | Multiple public constructors keep the handler on the container path
ERGO004 | Usage | Info | [FromServices] has no effect on constructor parameters
ERGO005 | Usage | Error | Dispatch can never reach a handler
ERGO006 | Usage | Warning | Only subtypes of the dispatched static type are handled
ERGO007 | Usage | Warning | No dispatch site can reach this handler
ERGO008 | Performance | Info | Unreachable handler excluded from generated registration
ERGO009 | Usage | Disabled | Dispatch site's static message type is opaque
ERGO010 | Usage | Error | Multiple main handlers claim the same message at the same level
ERGO011 | Usage | Error | Result adapter annotation can never bind
ERGO012 | Usage | Error | Conflicting result-adapter annotations
ERGO013 | Usage | Error | Result type is not served by the configured default result adapter
ERGO014 | Usage | Warning | Opted-out message keeps a throwing pipeline
ERGO015 | Usage | Warning | Plugin assembly is excluded from reference scanning by the reserved name prefix
ERGO016 | Usage | Warning | Generic participant closes over no message and never executes
ERGO017 | Usage | Warning | Plugin service cannot receive the plugin's options
ERGO018 | Usage | Error | Registered type is not known at compile time
