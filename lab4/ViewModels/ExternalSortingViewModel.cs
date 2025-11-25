using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Threading;
using lab4.Models;
using lab4.Services;

namespace lab4.ViewModels;

public class ExternalSortingViewModel : ViewModelBase {
    private const double MinDelayMs = 50;
    private const double MaxDelayMs = 2000;
    private const int BufferCapacity = 4;

    private readonly DispatcherTimer _timer;
    private readonly List<CsvRowData> _originalRows = [];
    private readonly Dictionary<int, CsvRowVisual> _rowLookup = new();
    private readonly Dictionary<string, ExternalTapeVisual> _tapeLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ExternalRunVisual> _runVisualLookup = new();
    private readonly Dictionary<int, string> _runAttachment = new();
    private Queue<ExternalSortAction> _pendingActions = new();

    private bool _isPlaying;
    private double _animationDelayMs = 800;
    private string _statusMessage = "Загрузите CSV-файл";
    private string _loadedFileName = "Файл не выбран";
    private ExternalMergeAlgorithm _selectedAlgorithm = ExternalMergeAlgorithm.StraightMerge;
    private string? _selectedColumnHeader;
    private int _selectedColumnIndex;
    private bool _suppressColumnChange;

    public ExternalSortingViewModel() {
        Rows = [];
        BufferRows = [];
        TapeVisuals = [];
        TapeNames = [];
        TapeRows = [];
        ColumnHeaders = [];
        LogEntries = [];
        AlgorithmOptions = new List<KeyValuePair<ExternalMergeAlgorithm, string>> {
            new(ExternalMergeAlgorithm.StraightMerge, "Прямое слияние"),
            new(ExternalMergeAlgorithm.NaturalMerge, "Естественное слияние"),
            new(ExternalMergeAlgorithm.MultiwayMerge, "Многопутевое слияние")
        };

        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => ProcessNextAction();
        UpdateTimerInterval();
    }

    public ObservableCollection<CsvRowVisual> Rows { get; }

    public ObservableCollection<CsvRowVisual> BufferRows { get; }

    public ObservableCollection<ExternalTapeVisual> TapeVisuals { get; }

    public ObservableCollection<string> TapeNames { get; }

    public ObservableCollection<ExternalTapeRowVisual> TapeRows { get; }

    public ObservableCollection<string> ColumnHeaders { get; }

    public ObservableCollection<string> LogEntries { get; }

    public IReadOnlyList<KeyValuePair<ExternalMergeAlgorithm, string>> AlgorithmOptions { get; }

    public string LoadedFileName {
        get => _loadedFileName;
        private set => SetField(ref _loadedFileName, value);
    }

    public string StatusMessage {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public ExternalMergeAlgorithm SelectedAlgorithm {
        get => _selectedAlgorithm;
        set {
            if (!SetField(ref _selectedAlgorithm, value)) {
                return;
            }

            if (HasFileLoaded) {
                ClearLogs();
                AddLog($"Выбран алгоритм: {GetAlgorithmLabel(value)}");
                PrepareActions();
            }

            OnPropertyChanged(nameof(AlgorithmLabel));
        }
    }

    public string AlgorithmLabel => GetAlgorithmLabel(SelectedAlgorithm);

    public string? SelectedColumnHeader {
        get => _selectedColumnHeader;
        set {
            if (!SetField(ref _selectedColumnHeader, value)) {
                return;
            }

            _selectedColumnIndex = ColumnHeaders.IndexOf(value ?? string.Empty);
            if (_selectedColumnIndex < 0) {
                _selectedColumnIndex = 0;
            }

            if (_suppressColumnChange || !HasFileLoaded) {
                return;
            }

            ClearLogs();
            AddLog($"Выбран столбец: {SelectedColumnHeader ?? $"Колонка {_selectedColumnIndex + 1}"}");
            PrepareActions();
        }
    }

    public double AnimationDelayMs {
        get => _animationDelayMs;
        set {
            var normalized = Math.Clamp(value, MinDelayMs, MaxDelayMs);
            if (!SetField(ref _animationDelayMs, normalized)) {
                return;
            }

            UpdateTimerInterval();
            OnPropertyChanged(nameof(AnimationDelayLabel));
        }
    }

    public string AnimationDelayLabel => $"{AnimationDelayMs:0} мс";

    public bool HasFileLoaded => _originalRows.Count > 0;

    public bool CanChangeSettings => HasFileLoaded && !IsPlaying;

    public bool CanControl => HasFileLoaded;

    public bool CanLoadFile => !IsPlaying;

    public string BufferHint => $"Буфер удерживает не более {BufferCapacity} строк — всё остальное читается с диска.";

    private bool IsPlaying {
        get => _isPlaying;
        set {
            if (!SetField(ref _isPlaying, value)) {
                return;
            }

            OnPropertyChanged(nameof(CanChangeSettings));
            OnPropertyChanged(nameof(CanLoadFile));
            OnPropertyChanged(nameof(PlayButtonLabel));
        }
    }

    public string PlayButtonLabel => IsPlaying ? "Пауза" : "Пуск";

    public bool HasPendingActions => _pendingActions.Count > 0;

    public int RemainingSteps => _pendingActions.Count;

    public bool LoadFromFile(string filePath) {
        Pause();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
            StatusMessage = "Файл не найден";
            AddLog("Не удалось открыть файл", "Ошибка");
            return false;
        }

        try {
            var rows = ReadCsvFile(filePath);
            if (rows.Count == 0) {
                StatusMessage = "Файл пуст";
                AddLog("Файл пуст, нечего сортировать", "Предупреждение");
                ResetDataState();
                return false;
            }

            var header = rows[0];
            var dataRows = rows.Skip(1).Where(r => r.Count > 0).ToList();
            if (dataRows.Count == 0) {
                StatusMessage = "Нет строк с данными";
                AddLog("CSV содержит только заголовок", "Предупреждение");
                ResetDataState();
                return false;
            }

            _originalRows.Clear();
            var nextId = 0;
            foreach (var row in dataRows) {
                _originalRows.Add(new CsvRowData(nextId++, row));
            }

            RebuildHeaders(header, dataRows);
            LoadedFileName = Path.GetFileName(filePath);
            StatusMessage = $"Загружено строк: {dataRows.Count}";
            AddLog($"Файл {LoadedFileName} загружен (строк: {dataRows.Count})");

            PrepareActions();
            OnPropertyChanged(nameof(HasFileLoaded));
            OnPropertyChanged(nameof(CanControl));
            OnPropertyChanged(nameof(CanChangeSettings));
            return true;
        } catch (Exception ex) {
            StatusMessage = "Ошибка при чтении файла";
            AddLog($"Ошибка CSV: {ex.Message}", "Ошибка");
            ResetDataState();
            return false;
        }
    }

    public void TogglePlayPause() {
        if (IsPlaying) {
            Pause();
        } else {
            Start();
        }
    }

    public void Step() {
        if (IsPlaying) {
            Pause();
        }

        if (!HasPendingActions) {
            StatusMessage = "Нет подготовленных шагов";
            return;
        }

        ProcessNextAction();
    }

    private void Pause() {
        if (_timer.IsEnabled) {
            _timer.Stop();
        }

        if (IsPlaying) {
            AddLog("Анимация на паузе");
        }

        IsPlaying = false;
    }

    private void Start() {
        if (!HasFileLoaded) {
            StatusMessage = "Сначала загрузите CSV-файл";
            return;
        }

        if (!HasPendingActions) {
            PrepareActions();
        }

        if (!HasPendingActions) {
            StatusMessage = "Шаги ещё не готовы";
            return;
        }

        UpdateTimerInterval();
        _timer.Start();
        IsPlaying = true;
        StatusMessage = "Анимация запущена";
    }

    private void ProcessNextAction() {
        if (!HasPendingActions) {
            Pause();
            StatusMessage = "Все шаги завершены";
            return;
        }

        var action = _pendingActions.Dequeue();
        ClearHighlights();
        ExecuteAction(action);
        UpdateActionsInfo();
    }

    private void ExecuteAction(ExternalSortAction action) {
        if (action.TapeSnapshot != null && action.TapeSnapshot.Count > 0) {
            ApplyTapeSnapshot(action.TapeSnapshot);
        }

        switch (action.Type) {
            case ExternalSortActionType.BufferLoad:
            case ExternalSortActionType.BufferSorted:
                ApplyBufferAction(action);
                break;
            case ExternalSortActionType.RunWritten:
                ApplyRunWritten(action);
                break;
            case ExternalSortActionType.MergeCompare:
                ApplyMergeCompare(action);
                break;
            case ExternalSortActionType.MergeWrite:
                ApplyMergeWrite(action);
                break;
            case ExternalSortActionType.PassComplete:
                ApplyPassComplete(action);
                break;
            case ExternalSortActionType.Finished:
                ApplyFinished(action);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action.Type), action.Type, null);
        }
    }

    private void ApplyBufferAction(ExternalSortAction action) {
        UpdateBuffer(action.BufferRowIds);
        StatusMessage = action.Message;
        AddLog(action.Message, action.Category);
    }

    private void ApplyRunWritten(ExternalSortAction action) {
        StatusMessage = action.Message;
        AddLog(action.Message, action.Category);
    }

    private void ApplyMergeCompare(ExternalSortAction action) {
        if (action.RowIdA.HasValue && _rowLookup.TryGetValue(action.RowIdA.Value, out var first)) {
            first.IsComparing = true;
        }

        if (action.RowIdB.HasValue && _rowLookup.TryGetValue(action.RowIdB.Value, out var second)) {
            second.IsComparing = true;
        }

        UpdateBuffer(action.BufferRowIds);
        AddLog(action.Message, action.Category);
    }

    private void ApplyMergeWrite(ExternalSortAction action) {
        if (action.RowIdA.HasValue && _rowLookup.TryGetValue(action.RowIdA.Value, out var row)) {
            row.IsMoving = true;
        }

        UpdateBuffer(action.BufferRowIds);
        AddLog(action.Message, action.Category);
    }

    private void ApplyPassComplete(ExternalSortAction action) {
        BufferRows.Clear();
        AddLog(action.Message, action.Category);
        StatusMessage = action.Message;
    }

    private void ApplyFinished(ExternalSortAction action) {
        Pause();
        foreach (var row in Rows) {
            row.IsSorted = true;
            row.ClearStates();
        }

        BufferRows.Clear();
        StatusMessage = string.IsNullOrWhiteSpace(action.Message)
            ? "Сортировка завершена"
            : action.Message;
        AddLog(StatusMessage, action.Category);
    }

    private void UpdateActionsInfo() {
        OnPropertyChanged(nameof(RemainingSteps));
        OnPropertyChanged(nameof(HasPendingActions));
    }

    private void PrepareActions() {
        if (!HasFileLoaded) {
            BufferRows.Clear();
            _pendingActions = new Queue<ExternalSortAction>();
            UpdateActionsInfo();
            return;
        }

        ResetVisualRows();
        BufferRows.Clear();
        ClearHighlights();

        var keyIndex = Math.Clamp(_selectedColumnIndex, 0, Math.Max(0, ColumnHeaders.Count - 1));
        var columnLabel = SelectedColumnHeader ?? $"Колонка {keyIndex + 1}";

        var actions = ExternalMergeEngines.BuildActions(
            _originalRows,
            SelectedAlgorithm,
            keyIndex,
            columnLabel,
            BufferCapacity);

        _pendingActions = new Queue<ExternalSortAction>(actions);
        StatusMessage = $"Подготовлено шагов: {RemainingSteps}";
        AddLog($"Готово {RemainingSteps} шагов по колонке \"{columnLabel}\" ({AlgorithmLabel})");
        UpdateActionsInfo();
    }

    private void ResetVisualRows() {
        Rows.Clear();
        BufferRows.Clear();
        _rowLookup.Clear();
        ResetTapeVisuals();
        foreach (var row in _originalRows) {
            var visual = new CsvRowVisual(row.Id, row.Cells);
            Rows.Add(visual);
            _rowLookup[row.Id] = visual;
        }
    }

    private void ClearHighlights() {
        foreach (var row in Rows) {
            row.ClearStates();
        }

        BufferRows.Clear();
    }

    private void UpdateBuffer(IEnumerable<int>? rowIds) {
        BufferRows.Clear();
        if (rowIds == null) {
            return;
        }

        foreach (var id in rowIds
                     .Distinct()
                     .Take(BufferCapacity)) {
            if (_rowLookup.TryGetValue(id, out var visual)) {
                BufferRows.Add(visual);
            }
        }
    }

    private void ApplyTapeSnapshot(IReadOnlyList<ExternalTapeSnapshot> snapshot) {
        if (snapshot.Count == 0) {
            ResetTapeVisuals();
            return;
        }

        var seenTapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < snapshot.Count; i++) {
            var tapeSnapshot = snapshot[i];
            seenTapes.Add(tapeSnapshot.TapeName);
            var tapeVisual = GetOrCreateTapeVisual(tapeSnapshot.TapeName, i);
            UpdateTapeRuns(tapeVisual, tapeSnapshot);
        }

        for (var index = TapeVisuals.Count - 1; index >= 0; index--) {
            var tape = TapeVisuals[index];
            if (!seenTapes.Contains(tape.Name)) {
                TapeVisuals.RemoveAt(index);
                _tapeLookup.Remove(tape.Name);
            }
        }

        UpdateTapeNames();
        RebuildTapeRows();
    }

    private ExternalTapeVisual GetOrCreateTapeVisual(string name, int desiredIndex) {
        if (!_tapeLookup.TryGetValue(name, out var visual)) {
            visual = new ExternalTapeVisual(name);
            _tapeLookup[name] = visual;
            TapeVisuals.Add(visual);
        }

        var currentIndex = TapeVisuals.IndexOf(visual);
        if (currentIndex >= 0 && currentIndex != desiredIndex && desiredIndex >= 0 && desiredIndex < TapeVisuals.Count) {
            TapeVisuals.Move(currentIndex, desiredIndex);
        }

        return visual;
    }

    private void UpdateTapeRuns(ExternalTapeVisual tapeVisual, ExternalTapeSnapshot snapshot) {
        var seenRuns = new HashSet<int>();
        for (var i = 0; i < snapshot.Runs.Count; i++) {
            var runSnapshot = snapshot.Runs[i];
            seenRuns.Add(runSnapshot.RunId);
            if (!_runVisualLookup.TryGetValue(runSnapshot.RunId, out var runVisual)) {
                runVisual = new ExternalRunVisual(runSnapshot.RunId);
                _runVisualLookup[runSnapshot.RunId] = runVisual;
            }

            if (_runAttachment.TryGetValue(runSnapshot.RunId, out var previousTape) &&
                !string.Equals(previousTape, tapeVisual.Name, StringComparison.OrdinalIgnoreCase) &&
                _tapeLookup.TryGetValue(previousTape, out var previousTapeVisual)) {
                previousTapeVisual.Runs.Remove(runVisual);
            }

            _runAttachment[runSnapshot.RunId] = tapeVisual.Name;
            if (!tapeVisual.Runs.Contains(runVisual)) {
                tapeVisual.Runs.Add(runVisual);
            }

            var currentIndex = tapeVisual.Runs.IndexOf(runVisual);
            if (currentIndex >= 0 && currentIndex != i && i >= 0 && i < tapeVisual.Runs.Count) {
                tapeVisual.Runs.Move(currentIndex, i);
            }

            runVisual.TapeName = tapeVisual.Name;
            runVisual.OrderIndex = i;
            runVisual.RowCount = runSnapshot.RowIds.Count;
            runVisual.DisplayIndex = runSnapshot.DisplayIndex;
            runVisual.IsActive = runSnapshot.IsActive;
            runVisual.IsOutput = runSnapshot.IsOutput;
            PopulateRunRows(runVisual, runSnapshot);
        }

        for (var index = tapeVisual.Runs.Count - 1; index >= 0; index--) {
            var run = tapeVisual.Runs[index];
            if (!seenRuns.Contains(run.RunId)) {
                tapeVisual.Runs.RemoveAt(index);
                _runAttachment.Remove(run.RunId);
                _runVisualLookup.Remove(run.RunId);
            }
        }
    }

    private void PopulateRunRows(ExternalRunVisual runVisual, ExternalRunSnapshot snapshot) {
        runVisual.Rows.Clear();
        if (snapshot.RowIds.Count == 0) {
            return;
        }

        foreach (var id in snapshot.RowIds) {
            if (!_rowLookup.TryGetValue(id, out var visualRow)) {
                continue;
            }

            var cells = visualRow.Cells ?? Array.Empty<string>();
            var keyValue = _selectedColumnIndex >= 0 && _selectedColumnIndex < cells.Count
                ? cells[_selectedColumnIndex]
                : visualRow.DisplayText;

            var rowVisual = new RunRowVisual(visualRow.Id, string.IsNullOrWhiteSpace(keyValue) ? "—" : keyValue);
            var columnCount = Math.Max(ColumnHeaders.Count, cells.Count);
            for (var column = 0; column < columnCount; column++) {
                var header = column < ColumnHeaders.Count
                    ? ColumnHeaders[column]
                    : $"Колонка {column + 1}";
                var value = column < cells.Count ? cells[column] : string.Empty;
                rowVisual.Cells.Add(new RowCellVisual(header, string.IsNullOrWhiteSpace(value) ? "—" : value));
            }

            runVisual.Rows.Add(rowVisual);
        }
    }

    private void UpdateTapeNames() {
        TapeNames.Clear();
        foreach (var tape in TapeVisuals) {
            TapeNames.Add(tape.Name);
        }
    }

    private void RebuildTapeRows() {
        TapeRows.Clear();
        if (TapeVisuals.Count == 0) {
            return;
        }

        var maxRuns = TapeVisuals.Max(t => t.Runs.Count);
        if (maxRuns == 0) {
            var emptyRow = new ExternalTapeRowVisual();
            foreach (var tape in TapeVisuals) {
                emptyRow.Cells.Add(new ExternalTapeCellVisual(tape.Name, null));
            }

            TapeRows.Add(emptyRow);
            return;
        }

        for (var rowIndex = 0; rowIndex < maxRuns; rowIndex++) {
            var rowVisual = new ExternalTapeRowVisual();
            foreach (var tape in TapeVisuals) {
                ExternalRunVisual? run = rowIndex < tape.Runs.Count ? tape.Runs[rowIndex] : null;
                rowVisual.Cells.Add(new ExternalTapeCellVisual(tape.Name, run));
            }

            TapeRows.Add(rowVisual);
        }
    }

    private void ResetTapeVisuals() {
        TapeVisuals.Clear();
        TapeNames.Clear();
        TapeRows.Clear();
        _tapeLookup.Clear();
        _runVisualLookup.Clear();
        _runAttachment.Clear();
    }

    private void UpdateTimerInterval() {
        _timer.Interval = TimeSpan.FromMilliseconds(AnimationDelayMs);
    }

    private void RebuildHeaders(IReadOnlyList<string> headerRow, IReadOnlyCollection<IReadOnlyList<string>> dataRows) {
        ColumnHeaders.Clear();
        var headers = headerRow?.ToList() ?? [];
        if (headers.Count == 0) {
            var maxColumns = dataRows.Any() ? dataRows.Max(r => r.Count) : 0;
            for (var i = 0; i < maxColumns; i++) {
                headers.Add($"Колонка {i + 1}");
            }
        }

        var columnIndex = 1;
        foreach (var header in headers) {
            var normalized = string.IsNullOrWhiteSpace(header)
                ? $"Колонка {columnIndex}"
                : header.Trim();
            ColumnHeaders.Add(normalized);
            columnIndex++;
        }

        if (ColumnHeaders.Count == 0) {
            ColumnHeaders.Add("Колонка 1");
        }

        _suppressColumnChange = true;
        SelectedColumnHeader = ColumnHeaders[0];
        _selectedColumnIndex = 0;
        _suppressColumnChange = false;
    }

    private void ResetDataState() {
        _originalRows.Clear();
        Rows.Clear();
        BufferRows.Clear();
        ColumnHeaders.Clear();
        _rowLookup.Clear();
        ResetTapeVisuals();
        _pendingActions = new Queue<ExternalSortAction>();
        LoadedFileName = "Файл не выбран";
        UpdateActionsInfo();
        OnPropertyChanged(nameof(HasFileLoaded));
        OnPropertyChanged(nameof(CanControl));
        OnPropertyChanged(nameof(CanChangeSettings));
    }

    private void ClearLogs() => LogEntries.Clear();

    private void AddLog(string? message, string category = "Инфо") {
        if (string.IsNullOrWhiteSpace(message)) {
            return;
        }

        var text = string.IsNullOrWhiteSpace(category)
            ? message
            : $"[{category}] {message}";
        LogEntries.Add($"{DateTime.Now:HH:mm:ss}: {text}");

        const int maxEntries = 300;
        if (LogEntries.Count > maxEntries) {
            LogEntries.RemoveAt(0);
        }
    }

    private string GetAlgorithmLabel(ExternalMergeAlgorithm algorithm) =>
        AlgorithmOptions.FirstOrDefault(a => a.Key == algorithm).Value
        ?? algorithm.ToString();

    private static List<IReadOnlyList<string>> ReadCsvFile(string filePath) {
        var rows = new List<IReadOnlyList<string>>();
        using var reader = new StreamReader(filePath, Encoding.UTF8);

        string? line;
        char? delimiter = null;
        while ((line = reader.ReadLine()) != null) {
            line = line.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) {
                continue;
            }

            delimiter ??= DetectDelimiter(line);
            rows.Add(ParseLine(line, delimiter.Value));
        }

        return rows;
    }

    private static char DetectDelimiter(string sample) {
        var commaCount = sample.Count(c => c == ',');
        var semicolonCount = sample.Count(c => c == ';');
        if (semicolonCount > commaCount) {
            return ';';
        }

        if (commaCount > 0) {
            return ',';
        }

        return ';';
    }

    private static IReadOnlyList<string> ParseLine(string line, char delimiter) {
        var result = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++) {
            var ch = line[i];
            if (ch == '"') {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') {
                    builder.Append('"');
                    i++;
                } else {
                    inQuotes = !inQuotes;
                }
            } else if (ch == delimiter && !inQuotes) {
                result.Add(builder.ToString().Trim());
                builder.Clear();
            } else {
                builder.Append(ch);
            }
        }

        result.Add(builder.ToString().Trim());
        return result;
    }
}