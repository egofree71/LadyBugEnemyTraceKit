using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;
using LadyBugEnemyTraceLab.TraceLab.Sim;

namespace LadyBugEnemyTraceLab.TraceLab.Compare;

/// <summary>
/// v14 diagnostic report.
///
/// v13 proved that an enriched oracle key can replay the observed enemy0 movement.
/// This analyzer deliberately moves in the other direction: it maps the remaining
/// questions back to concrete Z80 routines and proposes the next MAME trace points
/// needed to replace the oracle with explicit arcade-like logic.
/// </summary>
public static class CodePathTracePlanAnalyzer
{
    public static IEnumerable<string> BuildReport(IReadOnlyList<ArcadeTraceFrame> frames)
    {
        var lines = new List<string>
        {
            "--- V14: PLAN DE TRACE ORIENTÉ CODE Z80 ---",
            "But: remplacer l'oracle v13 par des traces directes des routines ennemies, au lieu de deviner depuis frame_done.",
            ""
        };

        lines.AddRange(BuildRoutineMap());
        lines.Add("");

        if (frames.Count < 2)
        {
            lines.Add("Trace trop courte: charge une trace MAME avant de lancer cette analyse.");
            return lines;
        }

        int centers = 0;
        int turns = 0;
        int reversals = 0;
        int currentOnly = 0;
        int nextOnly = 0;
        int both = 0;
        int neither = 0;
        int stableReversal = 0;
        int localDoorSuspects = 0;
        int chaseActiveCenters = 0;

        var pcPairs = new Dictionary<string, PcPairStats>();
        var byPosition = new Dictionary<string, PositionStats>();
        var casesToTrace = new List<string>();

        Dictionary<string, int> stableReversalRules = BuildStableReversalRules(frames);

        for (int i = 0; i < frames.Count - 1; i++)
        {
            ArcadeTraceFrame current = frames[i];
            ArcadeTraceFrame next = frames[i + 1];
            if (current.Enemies.Count == 0 || next.Enemies.Count == 0)
                continue;

            CandidateEnemyState state = CandidateEnemyState.FromFrame(current);
            if (!state.IsAtDecisionCenter)
                continue;

            centers++;

            int dir = state.Direction & 0x0f;
            int nextDir = Hex.ToByte(next.Enemies[0].Dir) & 0x0f;
            int prefCur = Preferred0(current);
            int prefNext = Preferred0(next);
            int allowed = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);
            int opposite = Opposite(dir);
            string visibleKey = BuildVisibleKey(state.X, state.Y, dir);
            string posKey = $"({state.X:X2},{state.Y:X2})";
            string pcPair = $"{Safe(current.Pc)}->{Safe(next.Pc)}";
            bool isTurn = nextDir != dir;
            bool isReversal = nextDir == opposite && opposite != 0;
            bool isStableReversal = stableReversalRules.TryGetValue(visibleKey, out int stableDir) && stableDir == nextDir;
            bool currentMatches = prefCur != 0 && prefCur == nextDir;
            bool nextMatches = prefNext != 0 && prefNext == nextDir;
            bool chaseActive = current.EnemyWork.ChaseTimers.Any(v => Hex.ToByte(v) != 0);
            bool localDoorSuspicious = (allowed & nextDir) == 0 || isStableReversal || (!currentMatches && !nextMatches && isTurn);

            if (isTurn)
                turns++;
            if (isReversal)
                reversals++;
            if (isStableReversal)
                stableReversal++;
            if (chaseActive)
                chaseActiveCenters++;
            if (localDoorSuspicious)
                localDoorSuspects++;

            if (isStableReversal)
            {
                // Stable reversal is tracked separately because it is likely not a normal preferred-direction decision.
            }
            else if (currentMatches && nextMatches)
            {
                both++;
            }
            else if (currentMatches)
            {
                currentOnly++;
            }
            else if (nextMatches)
            {
                nextOnly++;
            }
            else
            {
                neither++;
            }

            AddPcPair(pcPairs, pcPair, current.Sample, state.X, state.Y, dir, nextDir);
            AddPosition(byPosition, posKey, current.Sample, dir, nextDir, prefCur, prefNext, allowed, current, next, isStableReversal, chaseActive);

            if (casesToTrace.Count < 20 && ShouldHighlightCase(isTurn, isStableReversal, currentMatches, nextMatches, chaseActive, allowed, nextDir))
            {
                casesToTrace.Add(
                    $"  sample {current.Sample:0000}: pos={posKey} dir={dir:X2}->{nextDir:X2} " +
                    $"pref={prefCur:X2}/{prefNext:X2} allowed={allowed:X1} " +
                    $"stableRev={isStableReversal} chase={JoinList(current.EnemyWork.ChaseTimers)} " +
                    $"B6={Safe(current.Timers.Timer61B6)} B7={Safe(current.Timers.Timer61B7)} B8={Safe(current.Timers.Timer61B8)} B9={Safe(current.Timers.Timer61B9)} " +
                    $"PC={pcPair} R={Safe(current.R)}");
            }
        }

        lines.Add("Résumé de la trace frame_done chargée:");
        lines.Add($"  centres géométriques enemy0: {centers}");
        lines.Add($"  changements de direction: {turns}");
        lines.Add($"  demi-tours: {reversals}, dont stables v11: {stableReversal}");
        lines.Add($"  centres avec chase timer non nul: {chaseActiveCenters}");
        lines.Add($"  cas suspects local-door / ordre interne: {localDoorSuspects}");
        lines.Add($"  attribution hors stable-reversal: pref-current-only={currentOnly}, pref-next-only={nextOnly}, both={both}, neither={neither}");
        lines.Add("");

        lines.Add("Paires PC observées autour des centres enemy0:");
        foreach (KeyValuePair<string, PcPairStats> pair in pcPairs.OrderByDescending(p => p.Value.Count).ThenBy(p => p.Key).Take(16))
        {
            PcPairStats s = pair.Value;
            lines.Add($"  {pair.Key}: count={s.Count}, turns={s.Turns}, samples=[{string.Join(',', s.Samples.Take(8))}] firstPos=({s.FirstX:X2},{s.FirstY:X2}) {s.FirstDir:X2}->{s.FirstNextDir:X2}");
        }
        lines.Add("");

        lines.Add("Positions à tracer en priorité:");
        foreach (KeyValuePair<string, PositionStats> pair in byPosition.OrderByDescending(p => p.Value.Score).ThenBy(p => p.Key).Take(14))
        {
            PositionStats s = pair.Value;
            lines.Add($"  {pair.Key}: score={s.Score}, count={s.Count}, turns={s.Turns}, reversals={s.Reversals}, stableRev={s.StableReversals}, chase={s.ChaseCenters}, next=[{string.Join(',', s.NextDirections.Select(d => d.ToString("X2")))}], samples=[{string.Join(',', s.Samples.Take(10))}]");
        }
        lines.Add("");

        if (casesToTrace.Count > 0)
        {
            lines.Add("Cas concrets à ouvrir dans MAME avec les breakpoints v14:");
            lines.AddRange(casesToTrace);
            lines.Add("");
        }

        lines.AddRange(BuildBreakpointPlan());
        lines.Add("");
        lines.Add("Interprétation v14: la trace frame_done est utile pour reproduire la trajectoire, mais elle ne suffit pas pour savoir quelle branche Z80 a produit la décision. Les breakpoints proposés ciblent le moment où l'arcade charge preferred, rejette localement, choisit un fallback, inverse, bouge et commit.");

        return lines;
    }

    private static IEnumerable<string> BuildRoutineMap()
    {
        return new[]
        {
            "Routines Z80 ciblées:",
            "  407E/40AE/40B1 : Enemy_UpdateAll prépare preferred puis parcourt les slots actifs.",
            "  42BA            : Enemy_UpdateOne, entrée par ennemi.",
            "  427E            : test centre géométrique X&0F=08 et Y&0F=06.",
            "  42E6..4337      : chemin centre: preferred, validation maze/local-door, fallback.",
            "  4130            : Enemy_CheckLocalDoorBlock, validation locale des portes/tuiles.",
            "  4189            : Enemy_CheckDoorForcedReversal.",
            "  4347            : Enemy_ReverseTempDirection.",
            "  4224            : Enemy_MoveTempOnePixel.",
            "  43CE            : Enemy_CommitTempState."
        };
    }

    private static IEnumerable<string> BuildBreakpointPlan()
    {
        return new[]
        {
            "Breakpoints recommandés pour la prochaine trace MAME:",
            "  42BA enter_update_one      : slot HL, raw/x/y avant copie vers 61BD..61BF.",
            "  42E6 try_preferred        : preferred visible avant validation.",
            "  430F preferred_loaded     : nouvelle direction temporaire écrite dans 61BD.",
            "  4325 local_door_check     : direction temporaire + D/E probe avant 4130.",
            "  4334 fallback_enter       : 61C1/61C2 + temp dir avant fallback.",
            "  4342 forced_reversal_test : temp dir/x/y avant 4189.",
            "  4347 forced_reversal_hit  : temp dir/x/y + A/HL quand le demi-tour est réellement pris.",
            "  4224 move_one_pixel       : temp dir/x/y juste avant déplacement.",
            "  43CE commit               : temp final + slot final juste avant retour."
        };
    }

    private static bool ShouldHighlightCase(bool isTurn, bool stableRev, bool currentMatches, bool nextMatches, bool chaseActive, int allowed, int nextDir)
    {
        if (stableRev)
            return true;
        if (chaseActive && isTurn)
            return true;
        if ((allowed & nextDir) == 0)
            return true;
        if (isTurn && !currentMatches && !nextMatches)
            return true;
        if (isTurn && currentMatches != nextMatches)
            return true;
        return false;
    }

    private static Dictionary<string, int> BuildStableReversalRules(IReadOnlyList<ArcadeTraceFrame> frames)
    {
        var stats = new Dictionary<string, ReversalStats>();

        for (int i = 0; i < frames.Count - 1; i++)
        {
            if (frames[i].Enemies.Count == 0 || frames[i + 1].Enemies.Count == 0)
                continue;

            CandidateEnemyState state = CandidateEnemyState.FromFrame(frames[i]);
            if (!state.IsAtDecisionCenter)
                continue;

            int dir = state.Direction & 0x0f;
            int nextDir = Hex.ToByte(frames[i + 1].Enemies[0].Dir) & 0x0f;
            bool isReversal = dir != 0 && nextDir == Opposite(dir);
            string key = BuildVisibleKey(state.X, state.Y, dir);

            if (!stats.TryGetValue(key, out ReversalStats? s))
            {
                s = new ReversalStats();
                stats[key] = s;
            }

            s.Add(nextDir, isReversal);
        }

        var rules = new Dictionary<string, int>();
        foreach (KeyValuePair<string, ReversalStats> pair in stats)
        {
            ReversalStats s = pair.Value;
            if (s.ReversalCount > 0 && s.NonReversalCount == 0 && s.NextDirections.Count == 1)
                rules[pair.Key] = s.NextDirections.First();
        }

        return rules;
    }

    private static void AddPcPair(Dictionary<string, PcPairStats> stats, string key, int sample, int x, int y, int dir, int nextDir)
    {
        if (!stats.TryGetValue(key, out PcPairStats? s))
        {
            s = new PcPairStats(x, y, dir, nextDir);
            stats[key] = s;
        }

        s.Add(sample, dir, nextDir);
    }

    private static void AddPosition(
        Dictionary<string, PositionStats> stats,
        string key,
        int sample,
        int dir,
        int nextDir,
        int prefCur,
        int prefNext,
        int allowed,
        ArcadeTraceFrame current,
        ArcadeTraceFrame next,
        bool stableReversal,
        bool chaseActive)
    {
        if (!stats.TryGetValue(key, out PositionStats? s))
        {
            s = new PositionStats();
            stats[key] = s;
        }

        s.Add(sample, dir, nextDir, prefCur, prefNext, allowed, current, next, stableReversal, chaseActive);
    }

    private static int Preferred0(ArcadeTraceFrame frame)
    {
        if (frame.EnemyWork.Preferred.Count == 0)
            return 0;
        return Hex.ToByte(frame.EnemyWork.Preferred[0]) & 0x0f;
    }

    private static int Opposite(int dir)
    {
        return dir switch
        {
            0x01 => 0x04,
            0x04 => 0x01,
            0x02 => 0x08,
            0x08 => 0x02,
            _ => 0x00,
        };
    }

    private static string BuildVisibleKey(int x, int y, int dir) => $"{x:X2}:{y:X2}:{dir:X2}";
    private static string JoinList(IReadOnlyList<string> values) => values.Count == 0 ? "" : string.Join(',', values);
    private static string Safe(string? value) => string.IsNullOrEmpty(value) ? "00" : value;

    private sealed class PcPairStats
    {
        public int Count { get; private set; }
        public int Turns { get; private set; }
        public List<int> Samples { get; } = new();
        public int FirstX { get; }
        public int FirstY { get; }
        public int FirstDir { get; }
        public int FirstNextDir { get; }

        public PcPairStats(int firstX, int firstY, int firstDir, int firstNextDir)
        {
            FirstX = firstX;
            FirstY = firstY;
            FirstDir = firstDir;
            FirstNextDir = firstNextDir;
        }

        public void Add(int sample, int dir, int nextDir)
        {
            Count++;
            if (dir != nextDir)
                Turns++;
            if (Samples.Count < 16)
                Samples.Add(sample);
        }
    }

    private sealed class PositionStats
    {
        public int Count { get; private set; }
        public int Turns { get; private set; }
        public int Reversals { get; private set; }
        public int StableReversals { get; private set; }
        public int ChaseCenters { get; private set; }
        public int Score => Turns * 3 + Reversals * 4 + StableReversals * 4 + ChaseCenters * 2 + NextDirections.Count;
        public HashSet<int> NextDirections { get; } = new();
        public List<int> Samples { get; } = new();

        public void Add(int sample, int dir, int nextDir, int prefCur, int prefNext, int allowed, ArcadeTraceFrame current, ArcadeTraceFrame next, bool stableReversal, bool chaseActive)
        {
            Count++;
            if (dir != nextDir)
                Turns++;
            if (Opposite(dir) == nextDir)
                Reversals++;
            if (stableReversal)
                StableReversals++;
            if (chaseActive)
                ChaseCenters++;
            NextDirections.Add(nextDir);
            if (Samples.Count < 16)
                Samples.Add(sample);
        }
    }

    private sealed class ReversalStats
    {
        public int Total { get; private set; }
        public int ReversalCount { get; private set; }
        public int NonReversalCount => Total - ReversalCount;
        public HashSet<int> NextDirections { get; } = new();

        public void Add(int nextDir, bool isReversal)
        {
            Total++;
            if (isReversal)
                ReversalCount++;
            NextDirections.Add(nextDir);
        }
    }
}
