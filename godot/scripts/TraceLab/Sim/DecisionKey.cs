namespace LadyBugEnemyTraceLab.TraceLab.Sim;

public readonly record struct DecisionKey(int X, int Y, int Direction, int Preferred)
{
    public static DecisionKey FromState(CandidateEnemyState state, int preferred)
    {
        return new DecisionKey(state.X & 0xff, state.Y & 0xff, state.Direction & 0x0f, preferred & 0x0f);
    }

    public override string ToString()
    {
        return $"pos=({X:X2},{Y:X2}) dir={Direction:X2} pref={Preferred:X2}";
    }
}
