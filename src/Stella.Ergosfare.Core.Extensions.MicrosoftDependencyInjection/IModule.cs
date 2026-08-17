namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;


/// <summary>
/// A unit of registration: one call that adds a set of participants and services to the
/// container.
/// </summary>
/// <remarks>
/// The message modules — commands, queries, events — are modules, and so is the facade a
/// plugin's generator writes.
/// </remarks>
public interface IModule
{
    /// <summary>
    /// Adds this module's registrations to the container being built.
    /// </summary>
    /// <param name="configuration">
    /// What the module registers into: the service collection, and the record of what this
    /// container selected.
    /// </param>
    void Build(IModuleConfiguration configuration);
}
