using System;
using System.Windows.Input;

namespace CalcApp.ViewModels;

/// <summary>
/// Parancs implementáció, amely delegáltakra továbbítja a végrehajtási logikát.
/// Támogatja a végrehajthatóság ellenőrzését és automatikusan frissül a CommandManager-rel.
/// </summary>
public sealed class RelayCommand : ICommand
{
    #region Fields

    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    #endregion

    #region Constructors

    /// <summary>
    /// Létrehoz egy új RelayCommand példányt.
    /// </summary>
    /// <param name="execute">A végrehajtási logika.</param>
    /// <exception cref="ArgumentNullException">Ha az execute paraméter null.</exception>
    public RelayCommand(Action<object?> execute)
        : this(execute, canExecute: null)
    {
    }

    /// <summary>
    /// Létrehoz egy új RelayCommand példányt végrehajthatósági ellenőrzéssel.
    /// </summary>
    /// <param name="execute">A végrehajtási logika.</param>
    /// <param name="canExecute">A végrehajthatósági feltétel.</param>
    /// <exception cref="ArgumentNullException">Ha az execute paraméter null.</exception>
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    #endregion

    #region ICommand Implementation

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter)
        => _canExecute?.Invoke(parameter) ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter)
        => _execute(parameter);

    #endregion

    #region Public Methods

    /// <summary>
    /// Értesíti a WPF-et, hogy a parancs végrehajthatósága megváltozhatott.
    /// </summary>
    public static void RaiseCanExecuteChanged()
        => CommandManager.InvalidateRequerySuggested();

    #endregion
}
