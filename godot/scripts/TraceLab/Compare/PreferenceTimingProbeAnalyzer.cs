using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;
using LadyBugEnemyTraceLab.TraceLab.Sim;

namespace LadyBugEnemyTraceLab.TraceLab.Compare;

/// <summary>
/// v13 diagnostic report.
///
/// The v11 reversal probe showed that stable local reversals explain the first
/// divergence, but the next divergence is a preference timing problem:
/// sometimes the arcade decision matches preferred[0] from sample N, sometimes
/// preferred[0] from sample N+1, and sometimes neither visible value.
///
/// This report classifies every decision center by the visible source that would
/// have produced the MAME next direction.
/// </summary>
public static class PreferenceTimingProbeAnalyzer
{
    public static IEnumerable<string> BuildReport(IReadOnlyList<ArcadeTraceFrame> frames)
    {
        var lines = new List<string>
        {
            "--- TIMING-PROBE V13: ATTRIBUTION DES DÉCISIONS ENEMY0 ---",
            "Format: sample | pos | dir -> MAME | pref courant/suivant | attribution | allowed | timers/chase"
        };

        if (frames.Count < 2)
        {
            lines.Add("Trace trop courte pour analyser le timing des préférences.");
            return lines;
        }

        Dictionary<string, int> stableReversalRules = BuildStableReversalRules(frames, out int observedReversals, out int ambiguousReversalKeys);

        int centers = 0;
        int directionChanges = 0;
        int stableReversalHits = 0;
        int currentOnly = 0;
        int nextOnly = 0;
        int both = 0;
        int neither = 0;
        int keepCurrent = 0;
        int oppositeMatch = 0;
        int listed = 0;

        var basicStats = new Dictionary<string, KeyStats>();
        var enrichedStats = new Dictionary<string, KeyStats>();
        var currentNeededExamples = new List<string>();
        var nextNeededExamples = new List<string>();
        var neitherExamples = new List<string>();

        for (int i = 0; i < frames.Count - 1; i++)
        {
            ArcadeTraceFrame currentFrame = frames[i];
            ArcadeTraceFrame nextFrame = frames[i + 1];
            if (currentFrame.Enemies.Count == 0 || nextFrame.Enemies.Count == 0)
                continue;

            CandidateEnemyState state = CandidateEnemyState.FromFrame(currentFrame);
            if (!state.IsAtDecisionCenter)
                continue;

            centers++;

            int dir = state.Direction & 0x0f;
            int mameNextDir = Hex.ToByte(nextFrame.Enemies[0].Dir) & 0x0f;
            int prefCurrent = Preferred0(currentFrame);
            int prefNext = Preferred0(nextFrame);
            int opposite = Opposite(dir);
            int allowed = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);
            string visibleKey = BuildVisibleKey(state.X, state.Y, dir);
            bool isDirectionChange = mameNextDir != dir;
            bool stableReversal = stableReversalRules.TryGetValue(visibleKey, out int forcedDir) && forcedDir == mameNextDir;
            bool currentMatches = prefCurrent != 0 && prefCurrent == mameNextDir;
            bool nextMatches = prefNext != 0 && prefNext == mameNextDir;
            bool currentDirMatches = dir == mameNextDir;
            bool oppositeMatches = opposite != 0 && opposite == mameNextDir;

            if (isDirectionChange)
                directionChanges++;
            if (stableReversal)
                stableReversalHits++;
            if (currentDirMatches)
                keepCurrent++;
            if (oppositeMatches)
                oppositeMatch++;

            string attribution;
            if (stableReversal)
            {
                attribution = "stable-reversal";
            }
            else if (currentMatches && nextMatches)
            {
                both++;
                attribution = "pref-current+next";
            }
            else if (currentMatches)
            {
                currentOnly++;
                attribution = "pref-current-only";
            }
            else if (nextMatches)
            {
                nextOnly++;
                attribution = "pref-next-only";
            }
            else
            {
                neither++;
                attribution = currentDirMatches ? "keep-current-or-other" : (oppositeMatches ? "opposite-not-stable" : "unexplained-visible");
            }

            string detail =
                $"sample {currentFrame.Sample:0000}: pos=({state.X:X2},{state.Y:X2}) dir={dir:X2}->{mameNextDir:X2}, " +
                $"pref={prefCurrent:X2}/{prefNext:X2}, attribution={attribution}, allowed={StaticMazeDirectionValidator.FormatMask(allowed)}, " +
                $"prefs=[{JoinList(currentFrame.EnemyWork.Preferred)}], chase=[{JoinList(currentFrame.EnemyWork.ChaseTimers)}], rr={Safe(currentFrame.EnemyWork.ChaseRoundRobin)}, " +
                $"B6={Safe(currentFrame.Timers.Timer61B6)} B7={Safe(currentFrame.Timers.Timer61B7)} B8={Safe(currentFrame.Timers.Timer61B8)} B9={Safe(currentFrame.Timers.Timer61B9)}, " +
                $"R={Safe(currentFrame.R)} PC={Safe(currentFrame.Pc)}->{Safe(nextFrame.Pc)}";

            if (attribution == "pref-current-only" && currentNeededExamples.Count < 12)
                currentNeededExamples.Add("  " + detail);
            else if (attribution == "pref-next-only" && nextNeededExamples.Count < 12)
                nextNeededExamples.Add("  " + detail);
            else if (attribution == "unexplained-visible" && neitherExamples.Count < 12)
                neitherExamples.Add("  " + detail);

            if ((isDirectionChange || attribution.Contains("only", StringComparison.Ordinal)) && listed < 80)
            {
                lines.Add(detail);
                listed++;
            }

            AddKeyObservation(basicStats, BasicKey(state, currentFrame, nextFrame), currentFrame.Sample, mameNextDir);
            AddKeyObservation(enrichedStats, EnrichedKey(state, currentFrame, nextFrame), currentFrame.Sample, mameNextDir);
        }

        int basicConflicts = basicStats.Values.Count(s => s.NextDirections.Count > 1);
        int enrichedConflicts = enrichedStats.Values.Count(s => s.NextDirections.Count > 1);

        lines.Add($"Centres analysés: {centers}; changements de direction: {directionChanges}.");
        lines.Add($"Demi-tours MAME observés: {observedReversals}; règles stables v11: {stableReversalRules.Count}; clés de demi-tour ambiguës: {ambiguousReversalKeys}.");
        lines.Add($"Attribution hors stable-reversal: current-only={currentOnly}, next-only={nextOnly}, both={both}, neither={neither}.");
        lines.Add($"Autres indices: keep-current visible={keepCurrent}, opposite visible={oppositeMatch}, stable-reversal hits={stableReversalHits}.");
        lines.Add($"Conflits avec clé timing basique (pos+dir+prefCur+prefNext): {basicConflicts}/{basicStats.Count}.");
        lines.Add($"Conflits avec clé enrichie (prefs complets + chase + 61B6..61B9): {enrichedConflicts}/{enrichedStats.Count}.");

        if (currentNeededExamples.Count > 0)
        {
            lines.Add("Exemples où preferred[0] courant explique MAME et preferred[0] suivant trompe la candidate:");
            lines.AddRange(currentNeededExamples);
        }

        if (nextNeededExamples.Count > 0)
        {
            lines.Add("Exemples où preferred[0] suivant explique MAME et preferred[0] courant ne suffit pas:");
            lines.AddRange(nextNeededExamples);
        }

        if (neitherExamples.Count > 0)
        {
            lines.Add("Exemples encore inexpliqués par pref courant/suivant visibles:");
            lines.AddRange(neitherExamples);
        }

        lines.Add("Interprétation v13: si la clé enrichie n'a plus ou presque plus de conflits, on peut utiliser la candidate timing-probe pour vérifier que les divergences restantes viennent bien du moment exact où 61C4 et les timers sont échantillonnés, pas du déplacement pixel par pixel.");

        return lines;
    }

    private static Dictionary<string, int> BuildStableReversalRules(
        IReadOnlyList<ArcadeTraceFrame> frames,
        out int observedReversals,
        out int ambiguousKeys)
    {
        var stats = new Dictionary<string, ReversalStats>();
        observedReversals = 0;

        for (int i = 0; i < frames.Count - 1; i++)
        {
            if (frames[i].Enemies.Count == 0 || frames[i + 1].Enemies.Count == 0)
                continue;

            CandidateEnemyState state = CandidateEnemyState.FromFrame(frames[i]);
            if (!state.IsAtDecisionCenter)
                continue;

            int currentDir = state.Direction & 0x0f;
            int nextDir = Hex.ToByte(frames[i + 1].Enemies[0].Dir) & 0x0f;
            bool isReversal = currentDir != 0 && nextDir == Opposite(currentDir);
            string key = BuildVisibleKey(state.X, state.Y, currentDir);

            if (!stats.TryGetValue(key, out ReversalStats? ruleStats))
            {
                ruleStats = new ReversalStats();
                stats[key] = ruleStats;
            }

            ruleStats.Add(nextDir, isReversal);
            if (isReversal)
                observedReversals++;
        }

        var rules = new Dictionary<string, int>();
        ambiguousKeys = 0;

        foreach (KeyValuePair<string, ReversalStats> pair in stats)
        {
            ReversalStats s = pair.Value;
            if (s.ReversalCount == 0)
                continue;

            if (s.NonReversalCount == 0 && s.NextDirections.Count == 1)
                rules[pair.Key] = s.NextDirections.First();
            else
                ambiguousKeys++;
        }

        return rules;
    }

    private static void AddKeyObservation(Dictionary<string, KeyStats> stats, string key, int sample, int nextDir)
    {
        if (!stats.TryGetValue(key, out KeyStats? keyStats))
        {
            keyStats = new KeyStats();
            stats[key] = keyStats;
        }

        keyStats.Add(sample, nextDir);
    }

    private static string BasicKey(CandidateEnemyState state, ArcadeTraceFrame currentFrame, ArcadeTraceFrame nextFrame)
    {
        return $"{state.X:X2}:{state.Y:X2}:{state.Direction & 0x0f:X2}:pcur={Preferred0(currentFrame):X2}:pnext={Preferred0(nextFrame):X2}";
    }

    private static string EnrichedKey(CandidateEnemyState state, ArcadeTraceFrame currentFrame, ArcadeTraceFrame nextFrame)
    {
        int allowed = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);
        return
            $"pos={state.X:X2}:{state.Y:X2}:dir={state.Direction & 0x0f:X2}:allowed={allowed:X1}:" +
            $"prefCur0={Preferred0(currentFrame):X2}:prefNext0={Preferred0(nextFrame):X2}:" +
            $"prefsCur={JoinList(currentFrame.EnemyWork.Preferred)}:prefsNext={JoinList(nextFrame.EnemyWork.Preferred)}:" +
            $"chase={JoinList(currentFrame.EnemyWork.ChaseTimers)}:rr={Safe(currentFrame.EnemyWork.ChaseRoundRobin)}:" +
            $"b6={Safe(currentFrame.Timers.Timer61B6)}:b7={Safe(currentFrame.Timers.Timer61B7)}:b8={Safe(currentFrame.Timers.Timer61B8)}:b9={Safe(currentFrame.Timers.Timer61B9)}";
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

    private static string JoinList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "" : string.Join(",", values);
    }

    private static string Safe(string? value) => string.IsNullOrEmpty(value) ? "00" : value;

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

    private sealed class KeyStats
    {
        public HashSet<int> NextDirections { get; } = new();
        public List<int> Samples { get; } = new();

        public void Add(int sample, int nextDir)
        {
            Samples.Add(sample);
            NextDirections.Add(nextDir);
        }
    }
}
