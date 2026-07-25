using System.Windows.Input;

namespace SMacro.Core;

public abstract class BaseCommand: ICommand
{
    public virtual bool CanExecute(object? parameter)
    {
        return true;
    }
    
    public abstract void Execute(object? parameter);

    public event EventHandler? CanExecuteChanged;
}