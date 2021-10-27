using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Concise.ViewModels
{
    public class ConciseCommand : ICommand
    {
        private readonly Action<object>? _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _isEnabled = true;

        public event EventHandler? CanExecuteChanged;

        public void ChangeCanExecute()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value == _isEnabled)
                    return;

                _isEnabled = value;
                ChangeCanExecute();
            }
        }

        private ConciseCommand(Action<object> execute, Func<object, bool> canExecute) =>
            (_execute, _canExecute) = (execute, (parameter) => _isEnabled && canExecute(parameter));

        private ConciseCommand(Action execute, Func<object, bool> canExecute) :
            this((_) => execute?.Invoke(), canExecute) { }

        public ConciseCommand(Action execute)
            : this(execute, canExecute: (_) => true) { }

        public ConciseCommand(Func<Task> execute)
            : this(async () => await execute(), canExecute: (_) => true) { }

        public ConciseCommand(Func<Task> execute, Expression<Func<bool>> propExpr)
            : this((Action)(async () => await execute()), propExpr: propExpr) { }

        public ConciseCommand(Action execute, Expression<Func<bool>> propExpr)
            : this(() => execute(), canExecute: (_) => propExpr.Compile().Invoke())
        {
            var member = propExpr.Body as MemberExpression;

            if (member == null)
                throw new ArgumentException($"Expression '{propExpr}' must be a property.");

            var expression = member.Expression as ConstantExpression;

            if (expression == null)
                throw new ArgumentException($"Expression '{propExpr}' must be a constant expression");

            var viewModel = expression.Value as INotifyPropertyChanged;

            if (viewModel == null)
                throw new ArgumentException($"Expression '{propExpr}' must implement INotifyPropertyChanged");

            var propInfo = member.Member as PropertyInfo;

            if (propInfo == null)
                throw new ArgumentException($"Expression '{propExpr}' refers to a field, not a property.");

            var propertyName = propInfo.Name;

            viewModel.PropertyChanged += (sender, e) => {
                if (_isEnabled && e.PropertyName == propertyName)
                    ChangeCanExecute();
            };
        }

        public ConciseCommand(Func<ViewModel> createViewModel) :
            this(() => createViewModel().PresentView()) { }

        public ConciseCommand(Func<ViewModel> createViewModel, Expression<Func<bool>> propExpr) :
            this(() => createViewModel().PresentView(), propExpr) { }

        public ConciseCommand(bool isEnabled=true) =>
            (_execute, _isEnabled, _canExecute) = (null, isEnabled, (_) => _isEnabled);

        public ConciseCommand SetIsEnabled(bool isEnabled)
        {
            this.IsEnabled = isEnabled;
            return this;
        }

        public bool CanExecute(object parameter) => _canExecute(parameter);
        public void Execute(object parameter) => _execute?.Invoke(parameter);
    }
}
