using System;
using System.Diagnostics;
using System.Windows.Input;

namespace WpfApplication.Infrastructure
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _methodToExecute;
        private readonly Func<object?, bool>? _canExecuteEvaluator;

        public RelayCommand(Action<object?> methodToExecute)
            : this(methodToExecute, null) { }

        public RelayCommand(Action<object?> methodToExecute, Func<object?, bool>? canExecuteEvaluator)
        {
            _methodToExecute = methodToExecute ?? throw new ArgumentNullException(nameof(methodToExecute));
            _canExecuteEvaluator = canExecuteEvaluator;
        }

        [DebuggerStepThrough]
        public bool CanExecute(object? parameter)
        {
            return _canExecuteEvaluator?.Invoke(parameter) ?? true;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void Execute(object? parameter)
        {
            _methodToExecute.Invoke(parameter);
        }
    }
}
