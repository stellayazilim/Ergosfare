// IVT policy: src/Directory.Build.props grants every project its own "<ProjectName>.Test"
// automatically; this file carries only what that blanket cannot express — grants to other
// src assemblies, and to test assemblies that are not this project's own.
using System.Runtime.CompilerServices;

// src-to-src
[assembly:InternalsVisibleTo("Stella.Ergosfare.Core")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Events")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Events.Abstractions")]
// The stream invoker's fast lane builds execution contexts directly, so it needs the pool
// that moved here with ErgosfareContext (K7). Core and Events are already granted above.
[assembly:InternalsVisibleTo("Stella.Ergosfare.Queries")]

// foreign test assemblies
[assembly:InternalsVisibleTo("Stella.Ergosfare.Core.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Command.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Test.Fixtures")]
