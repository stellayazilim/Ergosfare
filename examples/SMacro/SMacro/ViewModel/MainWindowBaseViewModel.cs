using BaseViewModel = SMacro.Core.ViewModel;
namespace SMacro.ViewModel;

public class MainWindowViewModel: BaseViewModel
{
    public BaseViewModel CurrentViewModel { get; } = null!;

    public MainWindowViewModel()
    {
        CurrentViewModel = new HomeViewModel();
    }
}