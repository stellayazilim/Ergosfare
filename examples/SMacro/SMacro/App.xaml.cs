
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SMacro.Application;
using SMacro.Application.Services;
using SMacro.Commands;
using SMacro.Layouts;
using SMacro.ViewModel;

namespace SMacro;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IServiceProvider _serviceProvider;
    
    protected override void OnStartup(StartupEventArgs e)
    {

        _serviceProvider = new ServiceCollection()
            .AddSMacroApplication()
            .AddTransient(typeof(NavigateCommand<>))
            .AddSingleton<MainWindowBaseViewModel>()
            .AddSingleton<HomeViewModel>()
            .AddTransient<MacroEditorVm>()
            .AddSingleton<TestViewModel>()
            .AddSingleton(typeof(NavigateCommand<>))
            .AddSingleton<MainWindow>()
            .BuildServiceProvider();

        // initial page
        _serviceProvider.GetRequiredService<INavigationService>()
            .Navigate<MacroEditorVm>();
        
        MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow.Show();
        
       
        
        base.OnStartup(e);
    }
}