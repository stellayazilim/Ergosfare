using Stella.Ergosfare.Commands.Abstractions;
// ReSharper disable ClassNeverInstantiated.Global

namespace Stella.Ergosfare.Command.Test.__stubs__;


/// <summary>
/// Provides default command mediation settings for tests.
/// </summary>
public class StubDefaultMediationSetting
{
    
    /// <summary>
    /// Default <see cref="CommandMediationSettings"/> instance with the "default" group applied.
    /// </summary>
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    public static CommandMediationSettings CommandDefaultSetting = new CommandMediationSettings()
    {
        Filters = { Groups = ["default"]}
    };
}