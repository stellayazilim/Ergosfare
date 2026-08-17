// Every project's own "<ProjectName>.Test" assembly is granted access by
// src/Directory.Build.props. Listed here is only what that cannot cover: other src
// assemblies, and test assemblies belonging to other projects.
using System.Runtime.CompilerServices;

// src assemblies
[assembly:InternalsVisibleTo("Stella.Ergosfare.Core")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Events")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Events.Abstractions")]
// Queries builds execution contexts itself on its streaming path, so it needs the context
// pool that lives here alongside ErgosfareContext.
[assembly:InternalsVisibleTo("Stella.Ergosfare.Queries")]

// test assemblies belonging to other projects
[assembly:InternalsVisibleTo("Stella.Ergosfare.Core.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Command.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Test.Fixtures")]
