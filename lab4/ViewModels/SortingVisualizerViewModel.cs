using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using lab4.Models;
using lab4.Services;

namespace lab4.ViewModels;

public class SortingVisualizerViewModel : ViewModelBase {
    private readonly Random _random = new();
    private readonly DispatcherTimer _timer;
    private Queue<SortAction> _pendingActions = new();

    private string _manualInput = string.Empty;
    private bool _highlightComparisons = true;
    private bool _highlightSwaps = true;
    private double _animationSpeed = 1.0;
    private bool _isPlaying;
    private SortAlgorithm _selectedAlgorithm = SortAlgorithm.Bubble;
    private string _statusMessage = "Готов к визуализации";

    public SortingVisualizerViewModel() {
        Items = new ObservableCollection<VisualArrayItem>();
        LogEntries = new ObservableCollection<string>();
        AlgorithmOptions = new List<KeyValuePair<SortAlgorithm, string>> {
            new(SortAlgorithm.Bubble, "Bubble sort"),
            new(SortAlgorithm.Insertion, "Insertion sort"),
            new(SortAlgorithm.Heap, "Heap sort"),
            new(SortAlgorithm.Quick, "Quick sort")
        };

        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => ProcessNextAction();
        UpdateTimerInterval();

        GenerateRandomArray();
    }

    public ObservableCollection<VisualArrayItem> Items { get; }

    public ObservableCollection<string> LogEntries { get; }

    public IReadOnlyList<KeyValuePair<SortAlgorithm, string>> AlgorithmOptions { get; }

    public string ManualInput {
        get => _manualInput;
        set => SetField(ref _manualInput, value);
    }

    public SortAlgorithm SelectedAlgorithm {
        get => _selectedAlgorithm;
        set {
            if (!SetField(ref _selectedAlgorithm, value)) {
                return;
            }

            LogEntries.Clear();
            AddLog($"Выбран алгоритм: {GetAlgorithmLabel(value)}");
            PrepareActions();
        }
    }

    public bool HighlightComparisons {
        get => _highlightComparisons;
        set => SetField(ref _highlightComparisons, value);
    }

    public bool HighlightSwaps {
        get => _highlightSwaps;
        set => SetField(ref _highlightSwaps, value);
    }

    public double AnimationSpeed {
        get => _animationSpeed;
        set {
            var normalized = Math.Clamp(value, 0.25, 3.0);
            if (!SetField(ref _animationSpeed, normalized)) {
                return;
            }

            UpdateTimerInterval();
            OnPropertyChanged(nameof(AnimationSpeedLabel));
        }
    }

    public string AnimationSpeedLabel => $"{AnimationSpeed:0.0}x";

    public bool IsPlaying {
        get => _isPlaying;
        private set {
            if (!SetField(ref _isPlaying, value)) {
                return;
            }

            OnPropertyChanged(nameof(PlayButtonLabel));
            OnPropertyChanged(nameof(CanEditArray));
        }
    }

    public string PlayButtonLabel => IsPlaying ? "Пауза" : "Пуск";

    public bool CanEditArray => !IsPlaying;

    public int RemainingSteps => _pendingActions.Count;

    public bool HasPendingActions => _pendingActions.Count > 0;

    public string StatusMessage {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
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
            StatusMessage = "Нет доступных шагов. Сгенерируйте массив или измените алгоритм.";
            return;
        }

        ProcessNextAction();
    }

    public void GenerateRandomArray() {
        Pause();

        var length = _random.Next(6, 11);
        var values = Enumerable.Range(0, length)
            .Select(_ => _random.Next(5, 100))
            .ToList();

        ApplyNewArray(values, "Создан случайный массив");
    }

    public bool ApplyManualArray() {
        Pause();

        if (!TryParseManualInput(ManualInput, out var numbers, out var error)) {
            StatusMessage = error;
            AddLog(error);
            return false;
        }

        ApplyNewArray(numbers, "Применён пользовательский массив");
        return true;
    }

    private void Start() {
        if (!HasPendingActions) {
            PrepareActions();
        }

        if (!HasPendingActions) {
            StatusMessage = "Действий для выбранного набора нет.";
            return;
        }

        UpdateTimerInterval();
        _timer.Start();
        IsPlaying = true;
        StatusMessage = "Анимация запущена";
    }

    public void Pause() {
        if (!_timer.IsEnabled) {
            IsPlaying = false;
            return;
        }

        _timer.Stop();
        IsPlaying = false;
        StatusMessage = "Анимация на паузе";
    }

    private void ProcessNextAction() {
        if (!HasPendingActions) {
            Pause();
            StatusMessage = "Все шаги выполнены";
            return;
        }

        var action = _pendingActions.Dequeue();
        ClearHighlights();
        ExecuteAction(action);
        UpdateActionsInfo();
    }

    private void ExecuteAction(SortAction action) {
        switch (action.Type) {
            case SortActionType.Compare:
                ApplyComparison(action);
                break;
            case SortActionType.Swap:
                ApplySwap(action);
                break;
            case SortActionType.PassComplete:
                ApplyPassComplete(action);
                break;
            case SortActionType.PivotSelect:
                ApplyPivotSelect(action);
                break;
            case SortActionType.Finished:
                ApplyFinished(action);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action.Type), action.Type, null);
        }
    }

    private void ApplyComparison(SortAction action) {
        if (HighlightComparisons) {
            var first = GetItem(action.IndexA);
            var second = GetItem(action.IndexB);
            if (first != null) {
                first.IsComparing = true;
            }

            if (second != null) {
                second.IsComparing = true;
            }
        }

        AddLog(action.Message, "Сравнение");
    }

    private void ApplySwap(SortAction action) {
        var firstIndex = action.IndexA;
        var secondIndex = action.IndexB;

        if (!IsValidIndex(firstIndex) || !IsValidIndex(secondIndex)) {
            return;
        }

        var first = Items[firstIndex];
        var second = Items[secondIndex];

        if (HighlightSwaps) {
            first.IsSwapping = true;
            second.IsSwapping = true;
        }

        Items[firstIndex] = second;
        Items[secondIndex] = first;

        AddLog(action.Message, "Перестановка");
    }

    private void ApplyPassComplete(SortAction action) {
        var endIndex = action.IndexA;
        if (endIndex >= 0) {
            var startIndex = action.IndexB >= 0
                ? Math.Min(action.IndexB, endIndex)
                : endIndex;

            for (var i = startIndex; i <= endIndex; i++) {
                var item = GetItem(i);
                if (item == null) {
                    continue;
                }

                item.IsSorted = true;
                item.ClearPivot();
            }
        }

        AddLog(string.IsNullOrWhiteSpace(action.Message)
            ? "Проход завершён"
            : action.Message, "Шаг");
    }

    private void ApplyPivotSelect(SortAction action) {
        ClearPivotHighlights();

        var pivot = GetItem(action.IndexA);
        if (pivot != null) {
            pivot.IsPivot = true;
        }

        var message = string.IsNullOrWhiteSpace(action.Message)
            ? "Выбран новый опорный элемент"
            : action.Message;
        AddLog(message, "Опорный");
    }

    private void ApplyFinished(SortAction action) {
        Pause();
        foreach (var item in Items) {
            item.IsSorted = true;
            item.ClearHighlights();
            item.ClearPivot();
        }

        AddLog(string.IsNullOrWhiteSpace(action.Message)
            ? "Сортировка завершена"
            : action.Message, "Готово");
        StatusMessage = "Массив отсортирован";
    }

    private void ApplyNewArray(IReadOnlyCollection<int> values, string logMessage) {
        Items.Clear();
        foreach (var value in values) {
            Items.Add(new VisualArrayItem { Value = value });
        }

        ManualInput = string.Join(" ", values);
        LogEntries.Clear();
        AddLog($"{logMessage}: {ManualInput}");
        StatusMessage = $"Массив из {values.Count} элементов готов.";

        PrepareActions();
    }

    private void PrepareActions() {
        Pause();
        _pendingActions = Items.Count == 0
            ? new Queue<SortAction>()
            : new Queue<SortAction>(SortingEngines.BuildActions(
                Items.Select(i => i.Value).ToArray(),
                SelectedAlgorithm));

        foreach (var item in Items) {
            item.ResetStates();
        }

        UpdateActionsInfo();

        if (HasPendingActions) {
            AddLog($"Подготовлено {RemainingSteps} шагов для {GetAlgorithmLabel(SelectedAlgorithm)}");
        }
    }

    private void UpdateActionsInfo() {
        OnPropertyChanged(nameof(RemainingSteps));
        OnPropertyChanged(nameof(HasPendingActions));
    }

    private VisualArrayItem? GetItem(int index) =>
        index >= 0 && index < Items.Count ? Items[index] : null;

    private bool IsValidIndex(int index) => index >= 0 && index < Items.Count;

    private void ClearHighlights() {
        foreach (var item in Items) {
            item.ClearHighlights();
        }
    }

    private void ClearPivotHighlights() {
        foreach (var item in Items) {
            item.ClearPivot();
        }
    }

    private void UpdateTimerInterval() {
        var delay = TimeSpan.FromMilliseconds(1200 / AnimationSpeed);
        _timer.Interval = delay;
    }

    private void AddLog(string? message, string? category = null) {
        if (string.IsNullOrWhiteSpace(message)) {
            return;
        }

        var text = string.IsNullOrWhiteSpace(category)
            ? message
            : $"[{category}] {message}";
        LogEntries.Add($"{DateTime.Now:HH:mm:ss}: {text}");

        const int maxLogEntries = 200;
        if (LogEntries.Count > maxLogEntries) {
            LogEntries.RemoveAt(0);
        }
    }

    private static bool TryParseManualInput(string input, out List<int> values, out string error) {
        values = new List<int>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input)) {
            error = "Введите хотя бы два числа.";
            return false;
        }

        var parts = input
            .Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts) {
            if (!int.TryParse(part, out var number)) {
                error = $"Не удалось распознать \"{part}\" как целое число.";
                values.Clear();
                return false;
            }

            values.Add(number);
        }

        if (values.Count < 2) {
            error = "Для визуализации нужны минимум два элемента.";
            values.Clear();
            return false;
        }

        if (values.Count > 16) {
            error = "Пожалуйста, введите не больше 16 чисел.";
            values.Clear();
            return false;
        }

        return true;
    }

    private string GetAlgorithmLabel(SortAlgorithm algorithm) =>
        AlgorithmOptions.FirstOrDefault(x => x.Key == algorithm).Value
        ?? algorithm.ToString();
}
