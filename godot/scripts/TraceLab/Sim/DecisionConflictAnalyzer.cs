using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

public static class DecisionConflictAnalyzer
{
    public static List<string> BuildVisibleKeyConflictReport(IReadOnlyList<ArcadeTraceFrame> frames, int maxGroups = 20)
    {
        var lines = new List<string>();
        if (frames.Count < 2)
        {
            lines.Add("Pas assez de samples pour analyser les conflits de décision.");
            return lines;
        }

        var groups = new Dictionary<DecisionKey, List<DecisionObservation>>();
        int centerDecisions = 0;

        for (int i = 0; i < frames.Count - 1; i++)
        {
            if (frames[i].Enemies.Count == 0 || frames[i + 1].Enemies.Count == 0)
                continue;

            var state = CandidateEnemyState.FromFrame(frames[i]);
            if (!state.IsAtDecisionCenter)
                continue;

            centerDecisions++;
            int preferred = FullDecisionKey.GetPreferredForEnemy0(frames[i]);
            int nextDir = Hex.ToByte(frames[i + 1].Enemies[0].Dir) & 0x0f;
            var key = DecisionKey.FromState(state, preferred);

            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<DecisionObservation>();
                groups[key] = list;
            }

            list.Add(DecisionObservation.FromFrames(i, frames[i], frames[i + 1], nextDir));
        }

        var conflicts = groups
            .Where(kvp => kvp.Value.Select(o => o.NextDir).Distinct().Count() > 1)
            .OrderBy(kvp => kvp.Key.Y)
            .ThenBy(kvp => kvp.Key.X)
            .ThenBy(kvp => kvp.Key.Direction)
            .ThenBy(kvp => kvp.Key.Preferred)
            .ToList();

        lines.Add("--- CONFLITS DE DÉCISION MAME PAR CLÉ VISIBLE ---");
        lines.Add("Clé visible = position + direction actuelle + preferred[0].");
        lines.Add($"Centres analysés: {centerDecisions}; clés visibles distinctes: {groups.Count}; clés conflictuelles: {conflicts.Count}.");

        if (conflicts.Count == 0)
        {
            lines.Add("Aucun conflit: les champs visibles suffisent pour cette trace.");
            return lines;
        }

        int emitted = 0;
        foreach (var pair in conflicts)
        {
            if (emitted >= maxGroups)
            {
                lines.Add($"Rapport tronqué à {maxGroups} groupes conflictuels.");
                break;
            }

            lines.Add($"Conflit {emitted + 1}: {pair.Key}");
            foreach (DecisionObservation obs in pair.Value.Take(12))
                lines.Add("  " + obs.ToReportLine());
            if (pair.Value.Count > 12)
                lines.Add($"  ... {pair.Value.Count - 12} observation(s) supplémentaire(s).");
            emitted++;
        }

        lines.Add("Interprétation: si la même clé visible mène à des directions différentes, la décision arcade dépend d'autres variables: timers, vecteur de préférences complet, chase, local-door/antre, ou état dynamique du labyrinthe.");
        return lines;
    }

    private sealed class DecisionObservation
    {
        public int Sample { get; private init; }
        public int NextDir { get; private init; }
        public string CurrentDir { get; private init; } = "00";
        public string Preferred0 { get; private init; } = "00";
        public string PreferredVector { get; private init; } = "";
        public string ChaseVector { get; private init; } = "";
        public string Timer61B6 { get; private init; } = "00";
        public string Timer61B7 { get; private init; } = "00";
        public string Timer61B8 { get; private init; } = "00";
        public string Timer61B9 { get; private init; } = "00";
        public string R { get; private init; } = "00";
        public string Pc { get; private init; } = "0000";

        public static DecisionObservation FromFrames(int index, ArcadeTraceFrame current, ArcadeTraceFrame next, int nextDir)
        {
            string prefVector = current.EnemyWork.Preferred.Count == 0 ? "" : string.Join(",", current.EnemyWork.Preferred);
            string chaseVector = current.EnemyWork.ChaseTimers.Count == 0 ? "" : string.Join(",", current.EnemyWork.ChaseTimers);

            return new DecisionObservation
            {
                Sample = current.Sample,
                NextDir = nextDir,
                CurrentDir = current.Enemies[0].Dir,
                Preferred0 = current.EnemyWork.Preferred.Count > 0 ? current.EnemyWork.Preferred[0] : "00",
                PreferredVector = prefVector,
                ChaseVector = chaseVector,
                Timer61B6 = current.Timers.Timer61B6,
                Timer61B7 = current.Timers.Timer61B7,
                Timer61B8 = current.Timers.Timer61B8,
                Timer61B9 = current.Timers.Timer61B9,
                R = current.R,
                Pc = current.Pc
            };
        }

        public string ToReportLine()
        {
            return $"sample {Sample:D4}: dir {CurrentDir}->{NextDir:X2}, pref0={Preferred0}, prefs=[{PreferredVector}], chase=[{ChaseVector}], B6={Timer61B6}, B7={Timer61B7}, B8={Timer61B8}, B9={Timer61B9}, R={R}, PC={Pc}";
        }
    }
}
