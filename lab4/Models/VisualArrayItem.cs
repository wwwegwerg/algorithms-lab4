using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace lab4.Models;

public class VisualArrayItem : INotifyPropertyChanged {
    private static readonly IBrush DefaultBackground = Brush.Parse("#EEF2FF");
    private static readonly IBrush DefaultBorder = Brush.Parse("#CBD5F5");
    private static readonly IBrush CompareBackground = Brush.Parse("#FFF4D5");
    private static readonly IBrush CompareBorder = Brush.Parse("#F97316");
    private static readonly IBrush SwapBackground = Brush.Parse("#FEE2E2");
    private static readonly IBrush SwapBorder = Brush.Parse("#DC2626");
    private static readonly IBrush PivotBackground = Brush.Parse("#DBEAFE");
    private static readonly IBrush PivotBorder = Brush.Parse("#2563EB");
    private static readonly IBrush SortedBackground = Brush.Parse("#DCFCE7");
    private static readonly IBrush SortedBorder = Brush.Parse("#16A34A");

    private int _value;
    private bool _isComparing;
    private bool _isSwapping;
    private bool _isPivot;
    private bool _isSorted;
    private IBrush _backgroundBrush = DefaultBackground;
    private IBrush _borderBrush = DefaultBorder;

    public int Value {
        get => _value;
        set => SetField(ref _value, value);
    }

    public bool IsComparing {
        get => _isComparing;
        set {
            if (SetField(ref _isComparing, value)) {
                UpdateVisualState();
            }
        }
    }

    public bool IsSwapping {
        get => _isSwapping;
        set {
            if (SetField(ref _isSwapping, value)) {
                UpdateVisualState();
            }
        }
    }

    public bool IsPivot {
        get => _isPivot;
        set {
            if (SetField(ref _isPivot, value)) {
                UpdateVisualState();
            }
        }
    }

    public bool IsSorted {
        get => _isSorted;
        set {
            if (SetField(ref _isSorted, value)) {
                UpdateVisualState();
            }
        }
    }

    public IBrush BackgroundBrush {
        get => _backgroundBrush;
        private set => SetField(ref _backgroundBrush, value);
    }

    public IBrush BorderBrush {
        get => _borderBrush;
        private set => SetField(ref _borderBrush, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ClearHighlights() {
        IsComparing = false;
        IsSwapping = false;
    }

    public void ClearPivot() => IsPivot = false;

    public void ResetStates() {
        IsComparing = false;
        IsSwapping = false;
        IsPivot = false;
        IsSorted = false;
        UpdateVisualState();
    }

    protected bool SetField<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null) {
        if (Equals(storage, value)) {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateVisualState() {
        var background = DefaultBackground;
        var border = DefaultBorder;

        if (IsSwapping) {
            background = SwapBackground;
            border = SwapBorder;
        } else if (IsComparing) {
            background = CompareBackground;
            border = CompareBorder;
        } else if (IsPivot) {
            background = PivotBackground;
            border = PivotBorder;
        } else if (IsSorted) {
            background = SortedBackground;
            border = SortedBorder;
        }

        BackgroundBrush = background;
        BorderBrush = border;
    }
}