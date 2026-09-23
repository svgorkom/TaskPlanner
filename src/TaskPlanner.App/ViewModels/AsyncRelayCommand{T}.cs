using System.Windows.Input;

namespace TaskPlanner.App.ViewModels;

public sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, CancellationToken, Task> executeAsync;
    private readonly Func<T?, bool>? canExecute;
    private bool isExecuting;

    public AsyncRelayCommand(Func<T?, CancellationToken, Task> executeAsync, Func<T?, bool>? canExecute = null)
    {
        this.executeAsync = executeAsync;
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !isExecuting && (canExecute?.Invoke((T?)parameter) ?? true);
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
            await executeAsync((T?)parameter, CancellationToken.None);
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
