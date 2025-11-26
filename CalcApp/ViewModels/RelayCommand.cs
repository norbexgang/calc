using System;
using System.Windows.Input;

namespace CalcApp.ViewModels
{
    /// <summary>
    /// Egy parancs, amelynek végrehajtási logikáját delegáltakra továbbítja.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        /// <summary>
        /// Létrehoz egy új parancsot.
        /// </summary>
        /// <param name="execute">A végrehajtási logika.</param>
        /// <param name="canExecute">A végrehajtási állapot logikája.</param>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Meghatározza, hogy a parancs végrehajtható-e a jelenlegi állapotában.
        /// </summary>
        /// <param name="parameter">A parancs által használt adat. Ha a parancs nem igényel adatokat, ez az objektum nullára állítható.</param>
        /// <returns>igaz, ha ez a parancs végrehajtható; egyébként hamis.</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// Végrehajtja a parancsot.
        /// </summary>
        /// <param name="parameter">A parancs által használt adat. Ha a parancs nem igényel adatokat, ez az objektum nullára állítható.</param>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// Akkor következik be, amikor a parancs végrehajthatóságát befolyásoló változások történnek.
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
