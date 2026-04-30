namespace LadyBugEnemyTraceLab.TraceLab.Compare;

public sealed class TraceDifference
{
    public int Sample { get; init; }
    public string Field { get; init; } = string.Empty;
    public string Mame { get; init; } = string.Empty;
    public string Candidate { get; init; } = string.Empty;

    public override string ToString()
    {
        return $"sample {Sample:D4} | {Field}: MAME={Mame} Candidate={Candidate}";
    }
}
