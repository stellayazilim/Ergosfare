using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Logging.Abstractions;
using Ergosfare.Logging.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ergosfare.Logging.Test;

public class ErgosfareLoggingModuleTests
{
    [Fact]
    public void ShouldHaveLoggingConfiguration()
    {
        var services = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddLoggingModule();
            })
            .BuildServiceProvider();
        
        
        var loggingConfiguration = services.GetRequiredService<IErgosfareLoggingConfiguration>();
        Assert.NotNull(loggingConfiguration);
        Assert.NotNull(loggingConfiguration.CommandLoggerSettings.Formatter);
        Assert.NotNull(loggingConfiguration.EventLoggerSettings.Formatter);
        Assert.NotNull(loggingConfiguration.QueryLoggerSettings.Formatter);
        
        
        Assert.Equal(LogLevel.Information, loggingConfiguration.CommandLoggerSettings.LogLevel);
        Assert.Equal(LogLevel.Information, loggingConfiguration.EventLoggerSettings.LogLevel);
        Assert.Equal(LogLevel.Information, loggingConfiguration.QueryLoggerSettings.LogLevel);
    }
}