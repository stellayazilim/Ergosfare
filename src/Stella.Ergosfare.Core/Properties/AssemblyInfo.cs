// Every project's own "<ProjectName>.Test" assembly is granted access by
// src/Directory.Build.props. Listed here is only what that cannot cover: other src
// assemblies, and test assemblies belonging to other projects.
using System.Runtime.CompilerServices;

// src assemblies
[assembly:InternalsVisibleTo("Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection")]

// test assemblies belonging to other projects
[assembly:InternalsVisibleTo("Stella.Ergosfare.Events.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Command.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Test.Fixtures")]
