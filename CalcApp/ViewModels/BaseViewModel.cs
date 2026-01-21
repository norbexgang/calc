using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CalcApp.ViewModels;

/// <summary>
/// Alap nézetmodell osztály, amely implementálja az INotifyPropertyChanged interfészt.
/// Gyorsítótárazza a PropertyChangedEventArgs objektumokat a GC terhelés csökkentése érdekében.
/// </summary>
public abstract class BaseViewModel : INotifyPropertyChanged
{
    #region Static Fields

    /// <summary>
    /// PropertyChangedEventArgs gyorsítótár a memóriafoglalás csökkentéséhez.
    /// </summary>
    private static readonly ConcurrentDictionary<string, PropertyChangedEventArgs> PropertyChangedArgsCache = new();

    #endregion

    #region Events

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region Protected Methods

    /// <summary>
    /// Kiváltja a PropertyChanged eseményt.
    /// </summary>
    /// <param name="propertyName">A megváltozott tulajdonság neve. Automatikusan kitöltődik a hívó nevével.</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == null) return;

        var args = PropertyChangedArgsCache.GetOrAdd(
            propertyName,
            static name => new PropertyChangedEventArgs(name));

        PropertyChanged?.Invoke(this, args);
    }

    /// <summary>
    /// Beállítja a mező értékét és kiváltja a PropertyChanged eseményt, ha az érték megváltozott.
    /// </summary>
    /// <typeparam name="T">A mező típusa.</typeparam>
    /// <param name="field">A mező referenciája.</param>
    /// <param name="value">Az új érték.</param>
    /// <param name="propertyName">A tulajdonság neve.</param>
    /// <returns>Igaz, ha az érték megváltozott, egyébként hamis.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
