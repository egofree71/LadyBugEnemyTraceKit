using System.Collections.Generic;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab;

/// <summary>
/// This is deliberately not the final Lady Bug enemy AI.
/// It creates a small candidate trace useful for checking that the diff pipeline works.
/// Replace this with an adapter around the real ported enemy classes when the lab is wired to the real implementation.
/// </summary>
public static class NaiveCandidateTraceFactory
{
    public static List<ArcadeTraceFrame> CreateFromMameShape(IReadOnlyList<ArcadeTraceFrame> mameFrames)
    {
        var result = new List<ArcadeTraceFrame>();
        if (mameFrames.Count == 0)
            return result;

        ArcadeTraceFrame current = TraceFrameCloner.Clone(mameFrames[0]);
        current.Schema = "ladybug.enemyTraceCandidate.naive.v1";
        result.Add(TraceFrameCloner.Clone(current));

        for (int i = 1; i < mameFrames.Count; i++)
        {
            current = TraceFrameCloner.Clone(current);
            current.Sample = mameFrames[i].Sample;
            current.MameFrame = mameFrames[i].MameFrame;
            StepEnemy0OnePixelFromCurrentDirection(current);
            result.Add(TraceFrameCloner.Clone(current));
        }

        return result;
    }

    private static void StepEnemy0OnePixelFromCurrentDirection(ArcadeTraceFrame frame)
    {
        if (frame.Enemies.Count == 0)
            return;

        EnemySlotFrame enemy = frame.Enemies[0];
        int raw = System.Convert.ToInt32(enemy.Raw, 16);
        int dir = (raw >> 4) & 0x0f;
        int x = System.Convert.ToInt32(enemy.X, 16);
        int y = System.Convert.ToInt32(enemy.Y, 16);

        switch (dir)
        {
            case 0x01: x--; break;
            case 0x02: y--; break;
            case 0x04: x++; break;
            case 0x08: y++; break;
        }

        enemy.X = (x & 0xff).ToString("X2");
        enemy.Y = (y & 0xff).ToString("X2");
        frame.EnemyWork.TempDir = dir.ToString("X2");
        frame.EnemyWork.TempX = enemy.X;
        frame.EnemyWork.TempY = enemy.Y;
    }
}
