namespace lab4.Models;

public enum ExternalMergeAlgorithm {
    StraightMerge,
    NaturalMerge,
    MultiwayMerge
}

public enum ExternalSortActionType {
    Compare,
    Move,
    PassComplete,
    Finished
}

public class ExternalSortAction {
    public ExternalSortAction(
        ExternalSortActionType type,
        int? rowIdA = null,
        int? rowIdB = null,
        int? sourceIndex = null,
        int? targetIndex = null,
        string? valueA = null,
        string? valueB = null,
        string? message = null,
        int? passNumber = null) {
        Type = type;
        RowIdA = rowIdA;
        RowIdB = rowIdB;
        SourceIndex = sourceIndex;
        TargetIndex = targetIndex;
        ValueA = valueA;
        ValueB = valueB;
        Message = message ?? string.Empty;
        PassNumber = passNumber;
    }

    public ExternalSortActionType Type { get; }
    public int? RowIdA { get; }
    public int? RowIdB { get; }
    public int? SourceIndex { get; }
    public int? TargetIndex { get; }
    public string? ValueA { get; }
    public string? ValueB { get; }
    public string Message { get; }
    public int? PassNumber { get; }
}
