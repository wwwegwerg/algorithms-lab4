using System.Collections.Generic;

namespace lab4.Models;

public class ExternalTapeSnapshot {
    public ExternalTapeSnapshot(string tapeName, IReadOnlyList<ExternalRunSnapshot> runs) {
        TapeName = tapeName;
        Runs = runs;
    }

    public string TapeName { get; }

    public IReadOnlyList<ExternalRunSnapshot> Runs { get; }
}

public class ExternalRunSnapshot {
    public ExternalRunSnapshot(int runId, int displayIndex, IReadOnlyList<int> rowIds, bool isActive, bool isOutput) {
        RunId = runId;
        DisplayIndex = displayIndex;
        RowIds = rowIds;
        IsActive = isActive;
        IsOutput = isOutput;
    }

    public int RunId { get; }

    public int DisplayIndex { get; }

    public IReadOnlyList<int> RowIds { get; }

    public bool IsActive { get; }

    public bool IsOutput { get; }
}