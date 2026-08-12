// IVT policy: src/Directory.Build.props grants every project its own "<ProjectName>.Test"
// automatically; this file carries only what that blanket cannot express — grants to other
// src assemblies, and to test assemblies that are not this project's own.
using System.Runtime.CompilerServices;

// src-to-src
[assembly:InternalsVisibleTo("Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection")]

// foreign test assemblies
[assembly:InternalsVisibleTo("Stella.Ergosfare.Events.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Command.Test")]
[assembly:InternalsVisibleTo("Stella.Ergosfare.Test.Fixtures")]
