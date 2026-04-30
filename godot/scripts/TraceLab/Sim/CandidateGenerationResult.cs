using System.Collections.Generic;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

public sealed class CandidateGenerationResult
{
    public List<ArcadeTraceFrame> Frames { get; init; } = new();
    public List<string> Messages { get; init; } = new();
}
