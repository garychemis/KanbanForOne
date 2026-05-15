using System.Windows.Input;
using System.Diagnostics;

namespace KanbanForOne.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?>? _execute;
    private readonly Func<object?, Task>? _executeAsync;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    public static event Action<Exception>? UnhandledException;

    public RelayCommand(Action execute)
        : this(_ => execute())
    {
    }

    public RelayCommand(Func<Task> execute, Predicate<object?>? canExecute = null)
        : this(_ => execute(), canExecute)
    {
    }

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _executeAsync = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            if (_execute is not null)
            {
                _execute(parameter);
                return;
            }

            if (_executeAsync is null)
            {
                return;
            }

            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _executeAsync(parameter);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            UnhandledException?.Invoke(ex);
        }
        finally
        {
            if (_isExecuting)
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
