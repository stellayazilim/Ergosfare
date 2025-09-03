namespace Ergosfare.Examples.PluginExample;

public class ExamplePluginBuilder
{
    internal bool EnableHelloWorld { get; private set; }

 
    public ExamplePluginBuilder SetupPlugin()
    {
        EnableHelloWorld = true;
        return this;
    }
}