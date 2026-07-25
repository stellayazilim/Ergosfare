using Microsoft.Extensions.DependencyInjection;
using SMacro.Application.Services;
using SMacro.Core;

namespace SMacro.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSMacroApplication(this IServiceCollection services)
    {
        services.AddTransient<Func<Type, BaseViewModel>>(s => vm => (BaseViewModel) s.GetRequiredService(vm));
        services.AddSingleton<INavigationService>( s => new NavigationService(
            s.GetRequiredService<Func<Type, BaseViewModel>>()));
        return services;
    }
}