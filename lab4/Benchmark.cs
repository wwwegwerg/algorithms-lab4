using System;
using System.Diagnostics;

namespace lab4;

public static class Benchmark {
    public static void Warmup(Action action, int warmupCount) {
        for (var i = 0; i < warmupCount; i++) {
            action();
        }
    }

    public static double MeasureDurationInMs(Action action, int repetitionCount) {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < repetitionCount; i++) {
            action();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds / repetitionCount;
    }
}