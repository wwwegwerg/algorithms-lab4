using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace lab4.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null) {
        if (Equals(storage, value)) {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}