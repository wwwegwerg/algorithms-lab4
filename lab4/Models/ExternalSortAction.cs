using System.Collections.Generic;

namespace lab4.Models;

public enum ExternalMergeAlgorithm {
    StraightMerge,
    NaturalMerge,
    MultiwayMerge
}

public enum ExternalSortActionType {
    BufferLoad,
    BufferSorted,
    RunWritten,
    MergeCompare,
    MergeWrite,
    PassComplete,
    Finished
}

public class ExternalSortAction {
    public ExternalSortAction(
        ExternalSortActionType type,
        int? rowIdA = null,
        int? rowIdB = null,
        string? message = null,
        int? passNumber = null,
        IReadOnlyList<int>? bufferRowIds = null,
        IReadOnlyList<ExternalTapeSnapshot>? tapeSnapshot = null,
        string? category = null) {
        Type = type;
        RowIdA = rowIdA;
        RowIdB = rowIdB;
        BufferRowIds = bufferRowIds;
        TapeSnapshot = tapeSnapshot;
        Message = message ?? string.Empty;
        PassNumber = passNumber;
        Category = string.IsNullOrWhiteSpace(category) ? "Инфо" : category.Trim();
    }

    public ExternalSortActionType Type { get; }
    public int? RowIdA { get; }
    public int? RowIdB { get; }
    public IReadOnlyList<int>? BufferRowIds { get; }
    public IReadOnlyList<ExternalTapeSnapshot>? TapeSnapshot { get; }
    public string Message { get; }
    public int? PassNumber { get; }
    public string Category { get; }
}