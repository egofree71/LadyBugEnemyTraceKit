using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;
using LadyBugEnemyTraceLab.TraceLab.Sim;

namespace LadyBugEnemyTraceLab.TraceLab.Compare;

/// <summary>
/// v11 diagnostic report.
///
/// This intentionally does not try to prove the arcade algorithm. It surfaces the
/// places where MAME reverses enemy0, especially when the visible preferred
/// direction and the static maze table do not explain the decision.
/// </summary>
public static class ForcedReversalAnalyzer
{
    public static IEnumerable<string> BuildReport(IReadOnlyList<ArcadeTraceFrame> frames)
    {
        var lines = new List<string>
        {
            "--- DEMI-TOURS ENEMY0 OBSERVÉS DANS MAME ---",
            "Format: sample | pos | dir avant -> dir après | pref courant/suivant | allowed | timers/chase | PC"
        };

        if (frames.Count < 2)
        {
            lines.Add("Trace trop courte pour analyser les demi-tours.");
            return lines;
        }

        var statsByVisibleKey = new Dictionary<string, VisibleKeyStats>();
        int centers = 0;
        int directionChanges = 0;
        int reversals = 0;
        int nonCenterReversals = 0;

        for (int i = 0; i < frames.Count - 1; i++)
        {
            ArcadeTraceFrame currentFrame = frames[i];
            ArcadeTraceFrame nextFrame = frames[i + 1];
            if (currentFrame.Enemies.Count == 0 || nextFrame.Enemies.Count == 0)
                continue;

            CandidateEnemyState state = CandidateEnemyState.FromFrame(currentFrame);
            int currentDir = state.Direction & 0x0f;
            int nextDir = Hex.ToByte(nextFrame.Enemies[0].Dir) & 0x0f;
            int opposite = Opposite(currentDir);
            int allowedMask = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);
            bool isCenter = state.IsAtDecisionCenter;
            bool isDirectionChange = nextDir != currentDir;
            bool isReversal = currentDir != 0 && nextDir == opposite;
            string key = BuildVisibleKey(state.X, state.Y, currentDir);

            if (isCenter)
                centers++;
            if (isDirectionChange)
                directionChanges++;
            if (isReversal)
                reversals++;
            if (isReversal && !isCenter)
                nonCenterReversals++;

            if (!statsByVisibleKey.TryGetValue(key, out VisibleKeyStats? stats))
            {
                stats = new VisibleKeyStats(state.X, state.Y, currentDir);
                statsByVisibleKey[key] = stats;
            }
            stats.Add(currentFrame.Sample, nextDir, isReversal);

            if (!isReversal)
                continue;

            int prefCurrent = Preferred0(currentFrame);
            int prefNext = Preferred0(nextFrame);
            string chase = Join4(currentFrame.EnemyWork.ChaseTimers);
            string prefs = Join4(currentFrame.EnemyWork.Preferred);

            lines.Add(
                $"{currentFrame.Sample:0000} | ({state.X:X2},{state.Y:X2}) | {currentDir:X2}->{nextDir:X2} " +
                $"| pref={prefCurrent:X2}/{prefNext:X2} prefs=[{prefs}] " +
                $"| allowed={StaticMazeDirectionValidator.FormatMask(allowedMask)} center={isCenter} " +
                $"| rej={currentFrame.EnemyWork.RejectedMask} fb={currentFrame.EnemyWork.FallbackMask} " +
                $"| chase=[{chase}] rr={currentFrame.EnemyWork.ChaseRoundRobin} " +
                $"| B6={currentFrame.Timers.Timer61B6} B7={currentFrame.Timers.Timer61B7} B8={currentFrame.Timers.Timer61B8} B9={currentFrame.Timers.Timer61B9} " +
                $"| R={currentFrame.R} PC={currentFrame.Pc}->{nextFrame.Pc}");
        }

        lines.Add($"Centres géométriques observés: {centers}");
        lines.Add($"Changements de direction observés: {directionChanges}");
        lines.Add($"Demi-tours observés: {reversals}, dont hors centre: {nonCenterReversals}");

        var stableReversalKeys = statsByVisibleKey.Values
            .Where(s => s.Total > 0 && s.ReversalCount > 0 && s.NonReversalCount == 0 && s.NextDirections.Count == 1)
            .OrderBy(s => s.X)
            .ThenBy(s => s.Y)
            .ThenBy(s => s.CurrentDir)
            .ToList();

        lines.Add("Clés visibles stables qui ressemblent à des règles de demi-tour:");
        if (stableReversalKeys.Count == 0)
        {
            lines.Add("  aucune clé stable détectée");
        }
        else
        {
            foreach (VisibleKeyStats s in stableReversalKeys)
            {
                int nextDir = s.NextDirections.First();
                lines.Add($"  pos=({s.X:X2},{s.Y:X2}) dir={s.CurrentDir:X2}->{nextDir:X2} count={s.ReversalCount} samples=[{string.Join(',', s.Samples)}]");
            }
        }

        var ambiguousKeys = statsByVisibleKey.Values
            .Where(s => s.ReversalCount > 0 && (s.NonReversalCount > 0 || s.NextDirections.Count > 1))
            .OrderByDescending(s => s.Total)
            .Take(12)
            .ToList();

        lines.Add("Clés visibles ambiguës, à ne PAS convertir en règle simple:");
        if (ambiguousKeys.Count == 0)
        {
            lines.Add("  aucune clé ambiguë détectée");
        }
        else
        {
            foreach (VisibleKeyStats s in ambiguousKeys)
            {
                string dirs = string.Join(',', s.NextDirections.OrderBy(v => v).Select(v => v.ToString("X2")));
                lines.Add($"  pos=({s.X:X2},{s.Y:X2}) dir={s.CurrentDir:X2} next=[{dirs}] reversals={s.ReversalCount} nonReversals={s.NonReversalCount} samples=[{string.Join(',', s.Samples.Take(12))}]");
            }
        }

        lines.Add("Interprétation v11: si une clé stable réapparaît, la candidate reversal-probe peut la forcer avant la logique corridor-gated. Les clés ambiguës indiquent qu'il manque encore un état interne visible: timers, chase, porte locale, antre, ou timing exact de 61C4.");
        return lines;
    }

    private static string BuildVisibleKey(int x, int y, int dir) => $"{x:X2}:{y:X2}:{dir:X2}";

    private static int Preferred0(ArcadeTraceFrame frame)
    {
        if (frame.EnemyWork.Preferred.Count == 0)
            return 0;
        return Hex.ToByte(frame.EnemyWork.Preferred[0]) & 0x0f;
    }

    private static string Join4(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return "";
        return string.Join(',', values.Take(4));
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

    private sealed class VisibleKeyStats
    {
        public int X { get; }
        public int Y { get; }
        public int CurrentDir { get; }
        public int Total { get; private set; }
        public int ReversalCount { get; private set; }
        public int NonReversalCount => Total - ReversalCount;
        public HashSet<int> NextDirections { get; } = new();
        public List<int> Samples { get; } = new();

        public VisibleKeyStats(int x, int y, int currentDir)
        {
            X = x;
            Y = y;
            CurrentDir = currentDir;
        }

        public void Add(int sample, int nextDir, bool isReversal)
        {
            Total++;
            if (isReversal)
                ReversalCount++;
            NextDirections.Add(nextDir);
            Samples.Add(sample);
        }
    }
}
