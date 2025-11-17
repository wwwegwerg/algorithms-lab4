using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace lab4.Models;

public class CsvRowVisual : INotifyPropertyChanged {
    private static readonly IBrush DefaultBackground = Brush.Parse("#F9FAFB");
    private static readonly IBrush DefaultBorder = Brush.Parse("#E5E7EB");
    private static readonly IBrush CompareBackground = Brush.Parse("#FEF3C7");
    private static readonly IBrush CompareBorder = Brush.Parse("#F59E0B");
    private static readonly IBrush MoveBackground = Brush.Parse("#FDE2E4");
    private static readonly IBrush MoveBorder = Brush.Parse("#E11D48");
    private static readonly IBrush SortedBackground = Brush.Parse("#DCFCE7");
    private static readonly IBrush SortedBorder = Brush.Parse("#16A34A");

    private bool _isComparing;
    private bool _isMoving;
    private bool _isSorted;
    private IBrush _backgroundBrush = DefaultBackground;
    private IBrush _borderBrush = DefaultBorder;

    public CsvRowVisual(int id, IReadOnlyList<string> cells) {
        Id = id;
        Cells = cells;
    }

    public int Id { get; }

    private IReadOnlyList<string> Cells { get; }

    public string DisplayText => string.Join(" | ", Cells);

    public bool IsComparing {
        get => _isComparing;
        set {
            if (SetField(ref _isComparing, value)) {
                UpdateVisualState();
            }
        }
    }

    public bool IsMoving {
        get => _isMoving;
        set {
            if (SetField(ref _isMoving, value)) {
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

    public void ClearStates() {
        IsComparing = false;
        IsMoving = false;
        if (!IsSorted) {
            UpdateVisualState();
        }
    }

    public void Reset() {
        IsComparing = false;
        IsMoving = false;
        IsSorted = false;
        UpdateVisualState();
    }

    private bool SetField<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null) {
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

        if (IsMoving) {
            background = MoveBackground;
            border = MoveBorder;
        } else if (IsComparing) {
            background = CompareBackground;
            border = CompareBorder;
        } else if (IsSorted) {
            background = SortedBackground;
            border = SortedBorder;
        }

        BackgroundBrush = background;
        BorderBrush = border;
    }
}