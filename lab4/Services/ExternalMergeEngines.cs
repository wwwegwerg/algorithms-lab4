using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using lab4.Models;

namespace lab4.Services;

public static class ExternalMergeEngines {
    public static IReadOnlyList<ExternalSortAction> BuildActions(
        IReadOnlyList<CsvRowData> rows,
        ExternalMergeAlgorithm algorithm,
        int keyColumnIndex,
        string columnLabel,
        int bufferCapacity) {
        if (rows.Count == 0) {
            return [
                new ExternalSortAction(
                    ExternalSortActionType.Finished,
                    message: "Нет данных для сортировки",
                    category: "Готово")
            ];
        }

        var normalizedIndex = Math.Max(0, keyColumnIndex);
        var label = string.IsNullOrWhiteSpace(columnLabel)
            ? $"Колонка {normalizedIndex + 1}"
            : columnLabel;

        using var simulator = new ExternalMergeSimulator(rows, normalizedIndex, label, bufferCapacity);
        return algorithm switch {
            ExternalMergeAlgorithm.StraightMerge => simulator.RunStraightMerge(),
            ExternalMergeAlgorithm.NaturalMerge => simulator.RunNaturalMerge(),
            ExternalMergeAlgorithm.MultiwayMerge => simulator.RunMultiwayMerge(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };
    }

    private sealed class ExternalMergeSimulator : IDisposable {
        private readonly IReadOnlyList<CsvRowData> _rows;
        private readonly Dictionary<int, CsvRowData> _lookup;
        private readonly int _keyColumnIndex;
        private readonly string _columnLabel;
        private readonly int _memorySize;
        private readonly string _tempDir;
        private readonly string _sourceFile;
        private readonly List<ExternalSortAction> _actions = new();
        private readonly Dictionary<string, List<RunFile>> _tapes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _tapeRunCounters = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _tapeOrder = new();
        private int _runSequence;

        public ExternalMergeSimulator(
            IReadOnlyList<CsvRowData> rows,
            int keyColumnIndex,
            string columnLabel,
            int bufferCapacity) {
            _rows = rows;
            _lookup = rows.ToDictionary(r => r.Id);
            _keyColumnIndex = keyColumnIndex;
            _columnLabel = columnLabel;
            _memorySize = Math.Max(2, bufferCapacity);
            _tempDir = Path.Combine(Path.GetTempPath(), $"lab4_extsort_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _sourceFile = WriteSourceFile();
        }

        public IReadOnlyList<ExternalSortAction> RunStraightMerge() {
            var inputTapes = new[] { "Лента A", "Лента B" };
            var outputTapes = new[] { "Лента C", "Лента D" };
            EnsureTapesRegistered(inputTapes.Concat(outputTapes));

            var runs = CreateInitialRunsFromChunks(inputTapes);
            if (runs.Count == 1) {
                AddAction(new ExternalSortAction(
                    ExternalSortActionType.Finished,
                    message: "Данные уже отсортированы",
                    tapeSnapshot: CaptureSnapshot(),
                    category: "Готово"));
                return _actions.ToList();
            }

            var currentRuns = runs;
            var pass = 1;
            while (currentRuns.Count > 1) {
                var nextRuns = new List<RunFile>();
                var outputIndex = 0;
                for (var i = 0; i < currentRuns.Count; i += 2) {
                    var block = currentRuns.Skip(i).Take(2).ToList();
                    var outputTape = outputTapes[outputIndex % outputTapes.Length];
                    var merged = MergeBlock(block, outputTape);
                    nextRuns.Add(merged);
                    outputIndex++;
                }

                AddAction(new ExternalSortAction(
                    ExternalSortActionType.PassComplete,
                    message: $"Проход #{pass}: серий получено — {nextRuns.Count}",
                    passNumber: pass,
                    tapeSnapshot: CaptureSnapshot(),
                    category: "Проход"));

                currentRuns = nextRuns;
                pass++;
                (inputTapes, outputTapes) = (outputTapes, inputTapes);
            }

            AddAction(new ExternalSortAction(
                ExternalSortActionType.Finished,
                message: "Прямое слияние завершено",
                tapeSnapshot: CaptureSnapshot(),
                category: "Готово"));
            return _actions.ToList();
        }

        public IReadOnlyList<ExternalSortAction> RunNaturalMerge() {
            var inputTapes = new[] { "Лента A", "Лента B" };
            var outputTapes = new[] { "Лента C", "Лента D" };
            EnsureTapesRegistered(inputTapes.Concat(outputTapes));

            var runs = CreateNaturalRuns(inputTapes);
            if (runs.Count == 1) {
                AddAction(new ExternalSortAction(
                    ExternalSortActionType.Finished,
                    message: "Естественное слияние завершено",
                    tapeSnapshot: CaptureSnapshot(),
                    category: "Готово"));
                return _actions.ToList();
            }

            var currentRuns = runs;
            var pass = 1;
            while (currentRuns.Count > 1) {
                var nextRuns = new List<RunFile>();
                var outputIndex = 0;
                for (var i = 0; i < currentRuns.Count; i += 2) {
                    var block = currentRuns.Skip(i).Take(2).ToList();
                    var outputTape = outputTapes[outputIndex % outputTapes.Length];
                    var merged = MergeBlock(block, outputTape);
                    nextRuns.Add(merged);
                    outputIndex++;
                }

                AddAction(new ExternalSortAction(
                    ExternalSortActionType.PassComplete,
                    message: $"Естественный проход #{pass}: серий осталось — {nextRuns.Count}",
                    passNumber: pass,
                    tapeSnapshot: CaptureSnapshot(),
                    category: "Проход"));

                currentRuns = nextRuns;
                pass++;
                (inputTapes, outputTapes) = (outputTapes, inputTapes);
            }

            AddAction(new ExternalSortAction(
                ExternalSortActionType.Finished,
                message: "Естественное слияние завершено",
                tapeSnapshot: CaptureSnapshot(),
                category: "Готово"));
            return _actions.ToList();
        }

        public IReadOnlyList<ExternalSortAction> RunMultiwayMerge() {
            var fanIn = Math.Clamp(3, 2, _memorySize);
            var inputTapes = new[] { "Лента A", "Лента B", "Лента C" };
            var outputTapes = new[] { "Лента D", "Лента E", "Лента F" };
            EnsureTapesRegistered(inputTapes.Concat(outputTapes));

            var runs = CreateInitialRunsFromChunks(inputTapes);
            if (runs.Count == 1) {
                AddAction(new ExternalSortAction(
                    ExternalSortActionType.Finished,
                    message: "Многопутевое слияние завершено",
                    tapeSnapshot: CaptureSnapshot(),
                    category: "Готово"));
                return _actions.ToList();
            }

            var currentRuns = runs;
            var pass = 1;
            while (currentRuns.Count > 1) {
                var nextRuns = new List<RunFile>();
                var outputIndex = 0;
                for (var i = 0; i < currentRuns.Count; i += fanIn) {
                    var block = currentRuns.Skip(i).Take(fanIn).ToList();
                    var outputTape = outputTapes[outputIndex % outputTapes.Length];
                    var merged = MergeBlock(block, outputTape);
                    nextRuns.Add(merged);
                    outputIndex++;
                }

                AddAction(new ExternalSortAction(
                    ExternalSortActionType.PassComplete,
                    message: $"Многопутевой проход #{pass}: серий получено — {nextRuns.Count}",
                    passNumber: pass,
                    tapeSnapshot: CaptureSnapshot(),
                    category: "Проход"));

                currentRuns = nextRuns;
                pass++;
                (inputTapes, outputTapes) = (outputTapes, inputTapes);
            }

            AddAction(new ExternalSortAction(
                ExternalSortActionType.Finished,
                message: "Многопутевое слияние завершено",
                tapeSnapshot: CaptureSnapshot(),
                category: "Готово"));
            return _actions.ToList();
        }

        private List<RunFile> CreateInitialRunsFromChunks(IReadOnlyList<string> tapeCycle) {
            var runs = new List<RunFile>();
            using var reader = new StreamReader(_sourceFile);
            var chunk = new List<int>(_memorySize);
            var tapeIndex = 0;
            string? line;
            while ((line = reader.ReadLine()) != null) {
                if (!int.TryParse(line, out var id)) {
                    continue;
                }

                chunk.Add(id);
                if (chunk.Count == _memorySize) {
                    runs.Add(FlushChunk(chunk, tapeCycle[tapeIndex % tapeCycle.Count]));
                    tapeIndex++;
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0) {
                runs.Add(FlushChunk(chunk, tapeCycle[tapeIndex % tapeCycle.Count]));
            }

            return runs;
        }

        private List<RunFile> CreateNaturalRuns(IReadOnlyList<string> tapeCycle) {
            var runs = new List<RunFile>();
            using var reader = new StreamReader(_sourceFile);
            var currentRun = new List<int>();
            string? line;
            string? previousKey = null;
            var tapeIndex = 0;

            while ((line = reader.ReadLine()) != null) {
                if (!int.TryParse(line, out var id)) {
                    continue;
                }

                var key = GetKeyValue(id);
                if (previousKey != null && CompareKeys(previousKey, key) > 0 && currentRun.Count > 0) {
                    runs.Add(FlushNaturalRun(currentRun, tapeCycle[tapeIndex % tapeCycle.Count]));
                    tapeIndex++;
                    currentRun = new List<int>();
                }

                currentRun.Add(id);
                previousKey = key;
            }

            if (currentRun.Count > 0) {
                runs.Add(FlushNaturalRun(currentRun, tapeCycle[tapeIndex % tapeCycle.Count]));
            }

            return runs;
        }

        private RunFile FlushNaturalRun(List<int> runIds, string tapeName) {
            var snapshot = runIds.ToList();
            AddAction(new ExternalSortAction(
                ExternalSortActionType.BufferLoad,
                message: $"Обнаружена возрастающая серия (строк: {snapshot.Count})",
                bufferRowIds: snapshot,
                tapeSnapshot: CaptureSnapshot(),
                category: "Буфер"));
            return WriteRun(snapshot, tapeName, alreadySorted: true);
        }

        private RunFile FlushChunk(List<int> chunk, string tapeName) {
            var bufferSnapshot = chunk.ToList();
            AddAction(new ExternalSortAction(
                ExternalSortActionType.BufferLoad,
                message: $"Загружено строк в буфер: {bufferSnapshot.Count}",
                bufferRowIds: bufferSnapshot.ToList(),
                tapeSnapshot: CaptureSnapshot(),
                category: "Буфер"));
            var sortedSnapshot = bufferSnapshot.ToList();
            sortedSnapshot.Sort(CompareByRowId);
            AddAction(new ExternalSortAction(
                ExternalSortActionType.BufferSorted,
                message: $"Буфер отсортирован по {_columnLabel}",
                bufferRowIds: sortedSnapshot.ToList(),
                tapeSnapshot: CaptureSnapshot(),
                category: "Буфер"));
            return WriteRun(sortedSnapshot, tapeName, alreadySorted: true);
        }

        private RunFile MergeBlock(
            IReadOnlyList<RunFile> runs,
            string outputTape) {
            if (runs.Count == 0) {
                throw new InvalidOperationException("Нет серий для слияния");
            }

            var runIds = runs.Select(r => r.RunId).ToList();
            var outputRun = PrepareOutputRun(outputTape);
            var readers = runs
                .Select(run => new RunReader(run.FilePath, run.RunId, GetKeyValue))
                .ToList();
            try {
                foreach (var reader in readers) {
                    reader.MoveNext();
                }

                using var writer = new StreamWriter(outputRun.FilePath, append: false);
                var active = readers.Where(r => r.HasValue).ToList();
                while (active.Count > 0) {
                    active.Sort((a, b) => CompareKeys(
                        a.CurrentKey ?? string.Empty,
                        b.CurrentKey ?? string.Empty));

                    var winner = active[0];
                    var bufferIds = active
                        .Where(r => r.CurrentId.HasValue)
                        .Select(r => r.CurrentId!.Value)
                        .ToList();

                    var second = active.Count > 1 ? active[1] : null;
                    if (second != null) {
                        AddAction(new ExternalSortAction(
                            ExternalSortActionType.MergeCompare,
                            rowIdA: winner.CurrentId,
                            rowIdB: second.CurrentId,
                            message: string.Format(
                                CultureInfo.InvariantCulture,
                                "Сравниваем \"{0}\" и \"{1}\"",
                                Shorten(winner.CurrentKey),
                                Shorten(second.CurrentKey)),
                            bufferRowIds: bufferIds,
                            tapeSnapshot: CaptureSnapshot(runIds, new[] { outputRun.RunId }),
                            category: "Слияние"));
                    }

                    if (winner.CurrentId.HasValue) {
                        var rowId = winner.CurrentId.Value;
                        writer.WriteLine(rowId);
                        outputRun.RowIds.Add(rowId);
                        var runLabel = GetRunLabel(outputTape, outputRun.RunId);
                        AddAction(new ExternalSortAction(
                            ExternalSortActionType.MergeWrite,
                            rowIdA: rowId,
                            message: $"Записываем \"{Shorten(GetKeyValue(rowId))}\" → {runLabel}",
                            bufferRowIds: bufferIds,
                            tapeSnapshot: CaptureSnapshot(runIds, new[] { outputRun.RunId }),
                            category: "Запись"));
                    }

                    if (!winner.MoveNext()) {
                        active.RemoveAt(0);
                    }
                }
            } finally {
                foreach (var reader in readers) {
                    reader.Dispose();
                    RemoveRun(reader.RunId);
                }
            }

            var outputLabel = GetRunLabel(outputTape, outputRun.RunId);
            AddAction(new ExternalSortAction(
                ExternalSortActionType.RunWritten,
                message: $"{outputLabel} завершена (строк: {outputRun.RowIds.Count})",
                tapeSnapshot: CaptureSnapshot(outputRunIds: new[] { outputRun.RunId }),
                category: "Диск"));

            return outputRun;
        }

        private RunFile PrepareOutputRun(string tapeName) {
            var runId = ++_runSequence;
            var path = Path.Combine(_tempDir, $"run_{runId}.txt");
            var run = new RunFile(runId, path, tapeName);
            MoveRunToTape(run, tapeName);
            return run;
        }

        private RunFile WriteRun(List<int> rowIds, string tapeName, bool alreadySorted) {
            var run = PrepareOutputRun(tapeName);
            var ordered = alreadySorted ? rowIds : rowIds.OrderBy(id => id, Comparer<int>.Create(CompareByRowId)).ToList();
            using (var writer = new StreamWriter(run.FilePath, append: false)) {
                foreach (var id in ordered) {
                    writer.WriteLine(id);
                    run.RowIds.Add(id);
                }
            }

            var label = GetRunLabel(tapeName, run.RunId);
            AddAction(new ExternalSortAction(
                ExternalSortActionType.RunWritten,
                message: $"{label} готова (строк: {run.RowIds.Count})",
                tapeSnapshot: CaptureSnapshot(outputRunIds: new[] { run.RunId }),
                category: "Диск"));
            return run;
        }

        private string GetRunLabel(string tapeName, int runId) {
            if (_tapes.TryGetValue(tapeName, out var runs)) {
                var index = runs.FindIndex(r => r.RunId == runId);
                if (index >= 0) {
                    return $"{tapeName}: серия #{index + 1}";
                }
            }

            return $"{tapeName}: серия #{runId}";
        }

        private void RemoveRun(int runId) {
            foreach (var tape in _tapes.Values) {
                var index = tape.FindIndex(r => r.RunId == runId);
                if (index >= 0) {
                    var run = tape[index];
                    tape.RemoveAt(index);
                    if (File.Exists(run.FilePath)) {
                        File.Delete(run.FilePath);
                    }
                    return;
                }
            }
        }

        private void MoveRunToTape(RunFile run, string tapeName) {
            if (!string.IsNullOrWhiteSpace(run.TapeName) &&
                _tapes.TryGetValue(run.TapeName, out var oldList)) {
                oldList.Remove(run);
            }

            run.TapeName = tapeName;
            EnsureTapeExists(tapeName);
            _tapes[tapeName].Add(run);
            if (!_tapeRunCounters.TryGetValue(tapeName, out var counter)) {
                counter = 0;
            }

            run.DisplayIndex = ++counter;
            _tapeRunCounters[tapeName] = counter;
        }

        private void EnsureTapesRegistered(IEnumerable<string> names) {
            foreach (var name in names) {
                EnsureTapeExists(name);
            }
        }

        private void EnsureTapeExists(string name) {
            if (_tapes.ContainsKey(name)) {
                return;
            }

            _tapes[name] = new List<RunFile>();
            _tapeOrder.Add(name);
        }

        private IReadOnlyList<ExternalTapeSnapshot> CaptureSnapshot(
            IEnumerable<int>? activeRunIds = null,
            IEnumerable<int>? outputRunIds = null) {
            var active = activeRunIds?.ToHashSet() ?? new HashSet<int>();
            var output = outputRunIds?.ToHashSet() ?? new HashSet<int>();
            var result = new List<ExternalTapeSnapshot>();
            foreach (var tapeName in _tapeOrder) {
                var runs = _tapes.TryGetValue(tapeName, out var value) ? value : new List<RunFile>();
                var runSnapshots = runs
                    .Select(r => new ExternalRunSnapshot(
                        r.RunId,
                        r.DisplayIndex,
                        r.RowIds.ToList(),
                        active.Contains(r.RunId),
                        output.Contains(r.RunId)))
                    .ToList();
                result.Add(new ExternalTapeSnapshot(tapeName, runSnapshots));
            }

            if (result.Count == 0) {
                result.Add(new ExternalTapeSnapshot("Лента A", Array.Empty<ExternalRunSnapshot>()));
            }

            return result;
        }

        private void AddAction(ExternalSortAction action) {
            _actions.Add(action);
        }

        private int CompareByRowId(int leftId, int rightId) =>
            CompareKeys(GetKeyValue(leftId), GetKeyValue(rightId));

        private string GetKeyValue(int rowId) =>
            _lookup.TryGetValue(rowId, out var row)
                ? row.GetCell(_keyColumnIndex)?.Trim() ?? string.Empty
                : string.Empty;

        private static int CompareKeys(string? left, string? right) {
            var leftValue = left?.Trim() ?? string.Empty;
            var rightValue = right?.Trim() ?? string.Empty;

            if (double.TryParse(leftValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var leftNumber) &&
                double.TryParse(rightValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var rightNumber)) {
                return leftNumber.CompareTo(rightNumber);
            }

            return string.Compare(leftValue, rightValue, StringComparison.OrdinalIgnoreCase);
        }

        private static string Shorten(string? text, int max = 30) {
            if (string.IsNullOrWhiteSpace(text)) {
                return string.Empty;
            }

            return text.Length <= max ? text : text[..max] + "...";
        }

        private string WriteSourceFile() {
            var path = Path.Combine(_tempDir, "source.txt");
            using var writer = new StreamWriter(path, append: false);
            foreach (var row in _rows) {
                writer.WriteLine(row.Id);
            }

            return path;
        }

        public void Dispose() {
            try {
                if (Directory.Exists(_tempDir)) {
                    Directory.Delete(_tempDir, recursive: true);
                }
            } catch {
                // ignored
            }
        }

        private sealed class RunFile {
            public RunFile(int runId, string filePath, string tapeName) {
                RunId = runId;
                FilePath = filePath;
                TapeName = tapeName;
            }

            public int RunId { get; }
            public string FilePath { get; }
            public string TapeName { get; set; }
            public int DisplayIndex { get; set; }
            public List<int> RowIds { get; } = new();
        }

        private sealed class RunReader : IDisposable {
            private readonly StreamReader _reader;
            private readonly Func<int, string> _keySelector;
            private bool _disposed;

            public RunReader(string filePath, int runId, Func<int, string> keySelector) {
                _reader = new StreamReader(filePath);
                RunId = runId;
                _keySelector = keySelector;
            }

            public int RunId { get; }
            public int? CurrentId { get; private set; }
            public string? CurrentKey { get; private set; }
            public bool HasValue => CurrentId.HasValue;

            public bool MoveNext() {
                if (_disposed) {
                    return false;
                }

                string? line;
                while ((line = _reader.ReadLine()) != null) {
                    if (int.TryParse(line, out var id)) {
                        CurrentId = id;
                        CurrentKey = _keySelector(id);
                        return true;
                    }
                }

                CurrentId = null;
                CurrentKey = null;
                return false;
            }

            public void Dispose() {
                if (_disposed) {
                    return;
                }

                _reader.Dispose();
                _disposed = true;
            }
        }
    }
}