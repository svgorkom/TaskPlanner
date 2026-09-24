using System.Windows.Input;

namespace TaskPlanner.App.ViewModels;

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> execute;

    public RelayCommand(Action<T?> execute)
    {
        this.execute = execute;
    }

    event EventHandler? ICommand.CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        execute((T?)parameter);
    }
}
