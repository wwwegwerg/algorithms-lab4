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
    private readonly List<CsvRowData> _originalRows = new();
    private readonly Dictionary<int, CsvRowVisual> _rowLookup = new();
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
        Rows = new ObservableCollection<CsvRowVisual>();
        BufferRows = new ObservableCollection<CsvRowVisual>();
        ColumnHeaders = new ObservableCollection<string>();
        LogEntries = new ObservableCollection<string>();
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

    public string BufferHint => $"Буфер имитирует чтение максимум {BufferCapacity} строк.";

    public bool IsPlaying {
        get => _isPlaying;
        private set {
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
            AddLog($"Файл {LoadedFileName} загружен ({dataRows.Count} строк)");

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

    public void Pause() {
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
        switch (action.Type) {
            case ExternalSortActionType.Compare:
                ApplyCompare(action);
                break;
            case ExternalSortActionType.Move:
                ApplyMove(action);
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

    private void ApplyCompare(ExternalSortAction action) {
        if (action.RowIdA.HasValue) {
            if (_rowLookup.TryGetValue(action.RowIdA.Value, out var first)) {
                first.IsComparing = true;
            }
        }

        if (action.RowIdB.HasValue) {
            if (_rowLookup.TryGetValue(action.RowIdB.Value, out var second)) {
                second.IsComparing = true;
            }
        }

        UpdateBuffer(action.RowIdA, action.RowIdB);
        AddLog(action.Message, "Сравнение");
    }

    private void ApplyMove(ExternalSortAction action) {
        if (!action.RowIdA.HasValue || !action.TargetIndex.HasValue) {
            return;
        }

        if (!_rowLookup.TryGetValue(action.RowIdA.Value, out var row)) {
            return;
        }

        var currentIndex = Rows.IndexOf(row);
        if (currentIndex < 0) {
            return;
        }

        var targetIndex = Math.Clamp(action.TargetIndex.Value, 0, Rows.Count - 1);
        if (currentIndex != targetIndex) {
            Rows.Move(currentIndex, targetIndex);
        }

        row.IsMoving = true;
        UpdateBuffer(action.RowIdA, action.RowIdB);
        AddLog(action.Message, "Перемещение");
    }

    private void ApplyPassComplete(ExternalSortAction action) {
        BufferRows.Clear();
        AddLog(action.Message, "Проход");
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
        AddLog(StatusMessage, "Готово");
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
            columnLabel);

        _pendingActions = new Queue<ExternalSortAction>(actions);
        StatusMessage = $"Подготовлено шагов: {RemainingSteps}";
        AddLog($"Готово {RemainingSteps} шагов по колонке \"{columnLabel}\" ({AlgorithmLabel})");
        UpdateActionsInfo();
    }

    private void ResetVisualRows() {
        Rows.Clear();
        BufferRows.Clear();
        _rowLookup.Clear();
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

    private void UpdateBuffer(params int?[] rowIds) {
        BufferRows.Clear();
        if (rowIds == null || rowIds.Length == 0) {
            return;
        }

        var ordered = rowIds
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .Take(BufferCapacity);

        foreach (var id in ordered) {
            if (_rowLookup.TryGetValue(id, out var visual)) {
                BufferRows.Add(visual);
            }
        }
    }

    private void UpdateTimerInterval() {
        _timer.Interval = TimeSpan.FromMilliseconds(AnimationDelayMs);
    }

    private void RebuildHeaders(IReadOnlyList<string> headerRow, IReadOnlyCollection<IReadOnlyList<string>> dataRows) {
        ColumnHeaders.Clear();
        var headers = headerRow?.ToList() ?? new List<string>();
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
        _pendingActions = new Queue<ExternalSortAction>();
        LoadedFileName = "Файл не выбран";
        UpdateActionsInfo();
        OnPropertyChanged(nameof(HasFileLoaded));
        OnPropertyChanged(nameof(CanControl));
        OnPropertyChanged(nameof(CanChangeSettings));
    }

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