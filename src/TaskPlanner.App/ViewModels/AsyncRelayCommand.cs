using System.Windows.Input;

namespace TaskPlanner.App.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<CancellationToken, Task> executeAsync;
    private readonly Func<bool>? canExecute;
    private bool isExecuting;

    public AsyncRelayCommand(Func<CancellationToken, Task> executeAsync, Func<bool>? canExecute = null)
    {
        this.executeAsync = executeAsync;
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !isExecuting && (canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await executeAsync(CancellationToken.None);
        }
        finally
        {
            isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
