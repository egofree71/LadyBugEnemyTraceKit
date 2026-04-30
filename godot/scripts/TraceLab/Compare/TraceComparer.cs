using System;
using System.Collections.Generic;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Compare;

public static class TraceComparer
{
    public static TraceComparisonSummary Compare(
        IReadOnlyList<ArcadeTraceFrame> mameFrames,
        IReadOnlyList<ArcadeTraceFrame> candidateFrames,
        int maxDiffs = 200)
    {
        var diffs = new List<TraceDifference>();
        int count = Math.Min(mameFrames.Count, candidateFrames.Count);
        int differenceCount = 0;

        for (int i = 0; i < count; i++)
        {
            ArcadeTraceFrame a = mameFrames[i];
            ArcadeTraceFrame b = candidateFrames[i];
            AddFrameDiffs(a, b, diffs, ref differenceCount, maxDiffs);
        }

        AddLengthMismatchIfNeeded(mameFrames, candidateFrames, count, diffs, ref differenceCount, maxDiffs);

        return new TraceComparisonSummary
        {
            ComparedSamples = count,
            DifferenceCount = differenceCount,
            FirstDifferences = diffs,
            HasLengthMismatch = mameFrames.Count != candidateFrames.Count,
            MameLength = mameFrames.Count,
            CandidateLength = candidateFrames.Count
        };
    }

    public static TraceComparisonSummary CompareEnemy0MovementOnly(
        IReadOnlyList<ArcadeTraceFrame> mameFrames,
        IReadOnlyList<ArcadeTraceFrame> candidateFrames,
        int maxDiffs = 200)
    {
        var diffs = new List<TraceDifference>();
        int count = Math.Min(mameFrames.Count, candidateFrames.Count);
        int differenceCount = 0;

        for (int i = 0; i < count; i++)
        {
            ArcadeTraceFrame a = mameFrames[i];
            ArcadeTraceFrame b = candidateFrames[i];
            int sample = a.Sample;

            if (a.Enemies.Count == 0 || b.Enemies.Count == 0)
            {
                CompareField(sample, "enemy0.exists", (a.Enemies.Count > 0).ToString(), (b.Enemies.Count > 0).ToString(), diffs, ref differenceCount, maxDiffs);
                continue;
            }

            EnemySlotFrame ea = a.Enemies[0];
            EnemySlotFrame eb = b.Enemies[0];

            CompareField(sample, "enemy0.raw", ea.Raw, eb.Raw, diffs, ref differenceCount, maxDiffs);
            CompareField(sample, "enemy0.dir", ea.Dir, eb.Dir, diffs, ref differenceCount, maxDiffs);
            CompareField(sample, "enemy0.x", ea.X, eb.X, diffs, ref differenceCount, maxDiffs);
            CompareField(sample, "enemy0.y", ea.Y, eb.Y, diffs, ref differenceCount, maxDiffs);
            CompareField(sample, "enemy0.collisionActive", ea.CollisionActive.ToString(), eb.CollisionActive.ToString(), diffs, ref differenceCount, maxDiffs);
        }

        AddLengthMismatchIfNeeded(mameFrames, candidateFrames, count, diffs, ref differenceCount, maxDiffs);

        return new TraceComparisonSummary
        {
            ComparedSamples = count,
            DifferenceCount = differenceCount,
            FirstDifferences = diffs,
            HasLengthMismatch = mameFrames.Count != candidateFrames.Count,
            MameLength = mameFrames.Count,
            CandidateLength = candidateFrames.Count
        };
    }

    private static void AddLengthMismatchIfNeeded(
        IReadOnlyList<ArcadeTraceFrame> mameFrames,
        IReadOnlyList<ArcadeTraceFrame> candidateFrames,
        int count,
        List<TraceDifference> diffs,
        ref int differenceCount,
        int maxDiffs)
    {
        bool lengthMismatch = mameFrames.Count != candidateFrames.Count;
        if (!lengthMismatch)
            return;

        differenceCount++;
        if (diffs.Count < maxDiffs)
        {
            diffs.Add(new TraceDifference
            {
                Sample = count,
                Field = "trace.length",
                Mame = mameFrames.Count.ToString(),
                Candidate = candidateFrames.Count.ToString()
            });
        }
    }

    private static void AddFrameDiffs(
        ArcadeTraceFrame a,
        ArcadeTraceFrame b,
        List<TraceDifference> diffs,
        ref int differenceCount,
        int maxDiffs)
    {
        int sample = a.Sample;
        CompareField(sample, "pc", a.Pc, b.Pc, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "r", a.R, b.R, diffs, ref differenceCount, maxDiffs);

        CompareField(sample, "player.raw", a.Player.Raw, b.Player.Raw, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "player.x", a.Player.X, b.Player.X, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "player.y", a.Player.Y, b.Player.Y, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "player.currentDir", a.Player.CurrentDir, b.Player.CurrentDir, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "player.turnTargetX", a.Player.TurnTargetX, b.Player.TurnTargetX, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "player.turnTargetY", a.Player.TurnTargetY, b.Player.TurnTargetY, diffs, ref differenceCount, maxDiffs);

        int enemyCount = Math.Min(a.Enemies.Count, b.Enemies.Count);
        for (int i = 0; i < enemyCount; i++)
        {
            EnemySlotFrame ea = a.Enemies[i];
            EnemySlotFrame eb = b.Enemies[i];
            string prefix = $"enemy{i}";
            CompareField(sample, prefix + ".raw", ea.Raw, eb.Raw, diffs, ref differenceCount, maxDiffs);
            CompareField(sample, prefix + ".dir", ea.Dir, eb.Dir, diffs, ref differenceCount, maxDiffs);
            CompareField(sample, prefix + ".x", ea.X, eb.X, diffs, ref differenceCount, maxDiffs);
            CompareField(sample, prefix + ".y", ea.Y, eb.Y, diffs, ref differenceCount, maxDiffs);
            CompareField(sample, prefix + ".collisionActive", ea.CollisionActive.ToString(), eb.CollisionActive.ToString(), diffs, ref differenceCount, maxDiffs);
        }

        CompareField(sample, "enemy.count", a.Enemies.Count.ToString(), b.Enemies.Count.ToString(), diffs, ref differenceCount, maxDiffs);

        CompareField(sample, "enemyWork.tempDir", a.EnemyWork.TempDir, b.EnemyWork.TempDir, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "enemyWork.tempX", a.EnemyWork.TempX, b.EnemyWork.TempX, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "enemyWork.tempY", a.EnemyWork.TempY, b.EnemyWork.TempY, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "enemyWork.rejectedMask", a.EnemyWork.RejectedMask, b.EnemyWork.RejectedMask, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "enemyWork.fallbackMask", a.EnemyWork.FallbackMask, b.EnemyWork.FallbackMask, diffs, ref differenceCount, maxDiffs);
        CompareList(sample, "enemyWork.preferred", a.EnemyWork.Preferred, b.EnemyWork.Preferred, diffs, ref differenceCount, maxDiffs);
        CompareList(sample, "enemyWork.chaseTimers", a.EnemyWork.ChaseTimers, b.EnemyWork.ChaseTimers, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "enemyWork.chaseRoundRobin", a.EnemyWork.ChaseRoundRobin, b.EnemyWork.ChaseRoundRobin, diffs, ref differenceCount, maxDiffs);

        CompareField(sample, "timer.61B6", a.Timers.Timer61B6, b.Timers.Timer61B6, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "timer.61B8", a.Timers.Timer61B8, b.Timers.Timer61B8, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "timer.61B9", a.Timers.Timer61B9, b.Timers.Timer61B9, diffs, ref differenceCount, maxDiffs);
        CompareField(sample, "timer.freeze61E1", a.Timers.Freeze61E1, b.Timers.Freeze61E1, diffs, ref differenceCount, maxDiffs);
    }

    private static void CompareList(
        int sample,
        string field,
        IReadOnlyList<string> a,
        IReadOnlyList<string> b,
        List<TraceDifference> diffs,
        ref int differenceCount,
        int maxDiffs)
    {
        int count = Math.Min(a.Count, b.Count);
        for (int i = 0; i < count; i++)
            CompareField(sample, $"{field}[{i}]", a[i], b[i], diffs, ref differenceCount, maxDiffs);

        if (a.Count != b.Count)
            CompareField(sample, field + ".length", a.Count.ToString(), b.Count.ToString(), diffs, ref differenceCount, maxDiffs);
    }

    private static void CompareField(
        int sample,
        string field,
        string a,
        string b,
        List<TraceDifference> diffs,
        ref int differenceCount,
        int maxDiffs)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
            return;

        differenceCount++;
        if (diffs.Count >= maxDiffs)
            return;

        diffs.Add(new TraceDifference
        {
            Sample = sample,
            Field = field,
            Mame = a,
            Candidate = b
        });
    }
}
