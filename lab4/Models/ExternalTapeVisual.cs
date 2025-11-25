using System.Collections.ObjectModel;
using Avalonia.Media;
using lab4.ViewModels;

namespace lab4.Models;

public class ExternalTapeVisual : ViewModelBase {
    public ExternalTapeVisual(string name) {
        Name = name;
        Runs = new ObservableCollection<ExternalRunVisual>();
    }

    public string Name { get; }

    public ObservableCollection<ExternalRunVisual> Runs { get; }
}

public class ExternalRunVisual : ViewModelBase {
    private static readonly IBrush DefaultBackground = Brush.Parse("#F9FAFB");
    private static readonly IBrush DefaultBorder = Brush.Parse("#E5E7EB");
    private static readonly IBrush ActiveBackground = Brush.Parse("#FEF3C7");
    private static readonly IBrush ActiveBorder = Brush.Parse("#F59E0B");
    private static readonly IBrush OutputBackground = Brush.Parse("#DCFCE7");
    private static readonly IBrush OutputBorder = Brush.Parse("#16A34A");

    private int _orderIndex;
    private int _rowCount;
    private int _displayIndex;
    private bool _isActive;
    private bool _isOutput;
    private string _tapeName = string.Empty;
    private IBrush _backgroundBrush = DefaultBackground;
    private IBrush _borderBrush = DefaultBorder;

    public ExternalRunVisual(int runId) {
        RunId = runId;
        Rows = new ObservableCollection<RunRowVisual>();
    }

    public int RunId { get; }

    public string TapeName {
        get => _tapeName;
        set {
            if (SetField(ref _tapeName, value)) {
                OnPropertyChanged(nameof(DisplayLabel));
            }
        }
    }

    public ObservableCollection<RunRowVisual> Rows { get; }

    public int OrderIndex {
        get => _orderIndex;
        set {
            if (SetField(ref _orderIndex, value)) {
                OnPropertyChanged(nameof(DisplayLabel));
                OnPropertyChanged(nameof(RunLabel));
            }
        }
    }

    public int RowCount {
        get => _rowCount;
        set {
            if (SetField(ref _rowCount, value)) {
                OnPropertyChanged(nameof(RowSummary));
            }
        }
    }

    public int DisplayIndex {
        get => _displayIndex;
        set {
            if (SetField(ref _displayIndex, value)) {
                OnPropertyChanged(nameof(RunLabel));
                OnPropertyChanged(nameof(DisplayLabel));
            }
        }
    }

    public bool IsActive {
        get => _isActive;
        set {
            if (SetField(ref _isActive, value)) {
                UpdateBrushes();
            }
        }
    }

    public bool IsOutput {
        get => _isOutput;
        set {
            if (SetField(ref _isOutput, value)) {
                UpdateBrushes();
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

    public string DisplayLabel => string.IsNullOrWhiteSpace(TapeName)
        ? RunLabel
        : $"{TapeName}: {RunLabel}";

    public string RunLabel => DisplayIndex > 0 ? $"Серия #{DisplayIndex}" : $"Серия #{OrderIndex + 1}";

    public string RowSummary => $"Строк: {RowCount}";

    private void UpdateBrushes() {
        if (IsOutput) {
            BackgroundBrush = OutputBackground;
            BorderBrush = OutputBorder;
        } else if (IsActive) {
            BackgroundBrush = ActiveBackground;
            BorderBrush = ActiveBorder;
        } else {
            BackgroundBrush = DefaultBackground;
            BorderBrush = DefaultBorder;
        }
    }
}

public class RunRowVisual : ViewModelBase {
    public RunRowVisual(int rowId, string keyValue) {
        RowId = rowId;
        KeyValue = keyValue;
        Cells = new ObservableCollection<RowCellVisual>();
    }

    public int RowId { get; }

    public string KeyValue { get; }

    public ObservableCollection<RowCellVisual> Cells { get; }

    public string Summary => string.IsNullOrWhiteSpace(KeyValue)
        ? $"#{DisplayIndex}"
        : $"#{DisplayIndex}: {KeyValue}";

    public int DisplayIndex => RowId + 1;
}

public class RowCellVisual : ViewModelBase {
    public RowCellVisual(string header, string value) {
        Header = header;
        Value = value;
    }

    public string Header { get; }

    public string Value { get; }
}

public class ExternalTapeRowVisual : ViewModelBase {
    public ExternalTapeRowVisual() {
        Cells = new ObservableCollection<ExternalTapeCellVisual>();
    }

    public ObservableCollection<ExternalTapeCellVisual> Cells { get; }
}

public class ExternalTapeCellVisual : ViewModelBase {
    private ExternalRunVisual? _run;

    public ExternalTapeCellVisual(string tapeName, ExternalRunVisual? run) {
        TapeName = tapeName;
        Run = run;
    }

    public string TapeName { get; }

    public ExternalRunVisual? Run {
        get => _run;
        set {
            if (SetField(ref _run, value)) {
                OnPropertyChanged(nameof(HasRun));
                OnPropertyChanged(nameof(HasNoRun));
            }
        }
    }

    public bool HasRun => Run != null;

    public bool HasNoRun => !HasRun;
}