using System;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

public readonly record struct FullDecisionKey(
    int X,
    int Y,
    int Direction,
    int Preferred0,
    string PreferredVector,
    string ChaseVector,
    string Timer61B6,
    string Timer61B7,
    string Timer61B8,
    string Timer61B9)
{
    public static FullDecisionKey FromFrame(ArcadeTraceFrame frame)
    {
        CandidateEnemyState state = CandidateEnemyState.FromFrame(frame);
        string prefVector = frame.EnemyWork.Preferred.Count == 0
            ? ""
            : string.Join(",", frame.EnemyWork.Preferred.Select(p => p.ToUpperInvariant()));
        string chaseVector = frame.EnemyWork.ChaseTimers.Count == 0
            ? ""
            : string.Join(",", frame.EnemyWork.ChaseTimers.Select(p => p.ToUpperInvariant()));

        return new FullDecisionKey(
            state.X & 0xff,
            state.Y & 0xff,
            state.Direction & 0x0f,
            GetPreferredForEnemy0(frame),
            prefVector,
            chaseVector,
            NormalizeHex(frame.Timers.Timer61B6),
            NormalizeHex(frame.Timers.Timer61B7),
            NormalizeHex(frame.Timers.Timer61B8),
            NormalizeHex(frame.Timers.Timer61B9));
    }

    public static int GetPreferredForEnemy0(ArcadeTraceFrame frame)
    {
        if (frame.EnemyWork.Preferred.Count == 0)
            return 0;

        return Hex.ToByte(frame.EnemyWork.Preferred[0]) & 0x0f;
    }

    private static string NormalizeHex(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "00";

        return value.Trim().ToUpperInvariant();
    }

    public string ShortString()
    {
        return $"pos=({X:X2},{Y:X2}) dir={Direction:X2} pref0={Preferred0:X2} B6={Timer61B6} B7={Timer61B7} B8={Timer61B8} B9={Timer61B9} prefs=[{PreferredVector}] chase=[{ChaseVector}]";
    }
}
