using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Compare;

public sealed class Z80EventCycle
{
    public int Slot { get; init; }
    public int Sample { get; init; }
    public int MameFrame { get; init; }
    public List<Z80EventFrame> Events { get; } = new();

    public Z80EventFrame? Enter => First("Enemy_UpdateOne_ENTER");
    public Z80EventFrame? TryPreferred => First("TryPreferred_ENTER");
    public Z80EventFrame? PreferredLoaded => First("PreferredLoaded_430F");
    public Z80EventFrame? LocalDoorCheck => First("LocalDoorCheck_4325");
    public Z80EventFrame? Fallback => First("Fallback_ENTER_4241");
    public Z80EventFrame? ForcedReversalTest => First("ForcedReversal_TEST_4342");
    public Z80EventFrame? ForcedReversalHit => Events.FirstOrDefault(e => e.Tag.Contains("ForcedReversal_HIT", StringComparison.Ordinal));
    public Z80EventFrame? Move => First("MoveOnePixel_4224");
    public Z80EventFrame? Commit => First("CommitTempState_43CE");

    /// <summary>
    /// Best snapshot for the state actually being processed by this enemy cycle.
    ///
    /// The 42BA event is taken before Enemy_UpdateOne has copied the current slot
    /// into 61BD/61BE/61BF. Once enemy1 is active, the temp registers can still
    /// contain the previous slot values at 42BA. The first branch event after that
    /// copy, typically TryPreferred or ForcedReversalTest, is therefore the safer
    /// start snapshot for position/dir diagnostics.
    /// </summary>
    public Z80EventFrame? DecisionStart => TryPreferred ?? ForcedReversalTest ?? Move ?? Commit ?? Enter;

    public bool IsEnemy0 => Slot == 0;
    public bool IsAtCenter => DecisionStart is not null && IsCenter(DecisionStart);
    public bool HasLocalDoorFallback => LocalDoorCheck is not null && Fallback is not null;
    public bool HasFallback => Fallback is not null;
    public bool HasTryPreferred => TryPreferred is not null;
    public bool HasForcedReversalHit => ForcedReversalHit is not null;

    public int EnterDir => Byte(Enter?.TmpDir);
    public int EnterX => Byte(Enter?.TmpX);
    public int EnterY => Byte(Enter?.TmpY);

    public int StartDir => Byte(DecisionStart?.TmpDir) & 0x0f;
    public int StartX => Byte(DecisionStart?.TmpX);
    public int StartY => Byte(DecisionStart?.TmpY);

    public int MoveDir => Byte(Move?.TmpDir) & 0x0f;
    public int CommitDir => Byte(Commit?.TmpDir) & 0x0f;
    public int CommitX => Byte(Commit?.TmpX);
    public int CommitY => Byte(Commit?.TmpY);

    public int Preferred0
    {
        get
        {
            Z80EventFrame? source = TryPreferred ?? DecisionStart ?? Enter;
            return source is not null && source.Preferred.Count > 0 ? Byte(source.Preferred[0]) & 0x0f : 0;
        }
    }

    public int RejectedMaskAtFallback => Byte(Fallback?.RejectedMask) & 0x0f;
    public int RejectedMaskAtLocalDoor => Byte(LocalDoorCheck?.RejectedMask) & 0x0f;

    public string PathKind
    {
        get
        {
            if (HasForcedReversalHit)
                return "forced-reversal-hit";
            if (HasLocalDoorFallback)
                return "local-door-reject -> fallback";
            if (HasFallback)
                return "fallback";
            if (HasTryPreferred && Move is not null)
                return MoveDir == Preferred0 ? "preferred accepted" : "preferred/validation path";
            if (ForcedReversalTest is not null)
                return "straight / forced-reversal-test-only";
            return "other";
        }
    }

    public Z80EventFrame? First(string tag) => Events.FirstOrDefault(e => string.Equals(e.Tag, tag, StringComparison.Ordinal));

    private static bool IsCenter(Z80EventFrame evt) => (Byte(evt.TmpX) & 0x0f) == 0x08 && (Byte(evt.TmpY) & 0x0f) == 0x06;
    private static int Byte(string? value) => Hex.ToByte(value) & 0xff;
}

public static class Z80EventCycleBuilder
{
    public static List<Z80EventCycle> BuildEnemy0Cycles(IReadOnlyList<Z80EventFrame> events)
    {
        var allCycles = BuildAllCycles(events);
        return allCycles.Where(c => c.IsEnemy0).ToList();
    }

    public static List<Z80EventCycle> BuildAllCycles(IReadOnlyList<Z80EventFrame> events)
    {
        var cycles = new List<Z80EventCycle>();
        Z80EventCycle? current = null;
        int currentSample = int.MinValue;
        int slotOrdinalInSample = -1;

        foreach (Z80EventFrame evt in events)
        {
            if (evt.Tag == "Enemy_UpdateOne_ENTER")
            {
                if (current is not null)
                    cycles.Add(current);

                if (evt.FrameSample != currentSample)
                {
                    currentSample = evt.FrameSample;
                    slotOrdinalInSample = 0;
                }
                else
                {
                    slotOrdinalInSample++;
                }

                // The arcade loop walks enemy slots in RAM order: enemy0, enemy1, enemy2, enemy3.
                // Do not infer the slot from 61BD/61BE/61BF at 42BA: those temp registers can
                // still describe the previously processed slot at the instant this breakpoint fires.
                current = new Z80EventCycle
                {
                    Slot = slotOrdinalInSample,
                    Sample = evt.FrameSample,
                    MameFrame = evt.MameFrame
                };
                current.Events.Add(evt);
                continue;
            }

            if (current is not null)
            {
                // Stay inside the current per-enemy update until the next Enemy_UpdateOne_ENTER.
                // UpdateAll callsite/enter events belong to frame-level context, not to this cycle.
                if (!evt.Tag.StartsWith("Enemy_UpdateAll", StringComparison.Ordinal))
                    current.Events.Add(evt);
            }
        }

        if (current is not null)
            cycles.Add(current);

        return cycles;
    }
}
