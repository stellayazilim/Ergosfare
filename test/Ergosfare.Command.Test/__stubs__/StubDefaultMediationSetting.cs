using Ergosfare.Commands.Abstractions;

namespace Ergosfare.Command.Test.__stubs__;

public class StubDefaultMediationSetting
{
    public static CommandMediationSettings CommandDefaultSetting = new CommandMediationSettings()
    {
        Filters = { Groups = ["default"]}
    };
}