using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ViewContentNavigator.Mvvm
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Predicate<object> _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
            : this(_ => execute(), canExecute == null ? (Predicate<object>)null : _ => canExecute())
        {
        }

        public AsyncRelayCommand(Func<object, Task> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                _isExecuting = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool CanExecute(object parameter) =>
            !_isExecuting && (_canExecute == null || _canExecute(parameter));

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                IsExecuting = true;
                await _execute(parameter);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
