using System.Collections.Generic;

namespace LadyBugEnemyTraceLab.TraceLab.Compare;

public sealed class TraceComparisonSummary
{
    public int ComparedSamples { get; init; }
    public int DifferenceCount { get; init; }
    public List<TraceDifference> FirstDifferences { get; init; } = new();
    public bool HasLengthMismatch { get; init; }
    public int MameLength { get; init; }
    public int CandidateLength { get; init; }
}
