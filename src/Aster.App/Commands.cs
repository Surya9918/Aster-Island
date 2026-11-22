using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Aster.App;

public sealed class ActionCommand(Action<object?> action) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public void Execute(object? parameter) => action(parameter);
}
public sealed class AsyncCommand(Func<object?, Task> action, Action onError) : ICommand
{
    private bool running;
    public bool CanExecute(object? parameter) => !running;
    public event EventHandler? CanExecuteChanged;
    public async void Execute(object? parameter)
    {
        if (running) return;
        running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await action(parameter); } catch { onError(); }
        finally { running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
