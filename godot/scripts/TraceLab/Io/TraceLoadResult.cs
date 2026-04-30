using System.Collections.Generic;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Io;

public sealed class TraceLoadResult
{
    public string Path { get; init; } = string.Empty;
    public List<ArcadeTraceFrame> Frames { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
