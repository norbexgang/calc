using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CalcApp.ViewModels
{
    /// <summary>
    /// Egy alap nézetmodell, amely implementálja az INotifyPropertyChanged interfészt a tulajdonságváltozások jelzésére.
    /// </summary>
    public class BaseViewModel : INotifyPropertyChanged
    {
        // Cache PropertyChangedEventArgs to reduce GC pressure
        private static readonly ConcurrentDictionary<string, PropertyChangedEventArgs> _propertyChangedCache = new();

        /// <summary>
        /// Esemény, amely akkor következik be, ha egy tulajdonság értéke megváltozik.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Kiváltja a PropertyChanged eseményt.
        /// </summary>
        /// <param name="propertyName">A megváltozott tulajdonság neve. Automatikusan kitöltődik a hívó nevével.</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName == null) return;
            var args = _propertyChangedCache.GetOrAdd(propertyName, static name => new PropertyChangedEventArgs(name));
            PropertyChanged?.Invoke(this, args);
        }
    }
}
