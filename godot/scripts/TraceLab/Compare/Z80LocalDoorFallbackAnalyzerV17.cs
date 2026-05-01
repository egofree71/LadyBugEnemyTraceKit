using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Model;
using LadyBugEnemyTraceLab.TraceLab.Sim;

namespace LadyBugEnemyTraceLab.TraceLab.Compare;

public static class Z80LocalDoorFallbackAnalyzerV17
{
    public static IReadOnlyList<string> BuildReport(IReadOnlyList<ArcadeTraceFrame> mameFrames, IReadOnlyList<Z80EventFrame> events)
    {
        var lines = new List<string>();
        lines.Add("--- ANALYSE Z80 LOCAL-DOOR/FALLBACK v17 ---");

        if (events.Count == 0)
        {
            lines.Add("Aucun event Z80 chargé.");
            return lines;
        }

        List<Z80EventCycle> allCycles = Z80EventCycleBuilder.BuildAllCycles(events);
        List<Z80EventCycle> enemy0Cycles = allCycles.Where(c => c.IsEnemy0).ToList();
        Dictionary<int, ArcadeTraceFrame> frameBySample = mameFrames.GroupBy(f => f.Sample).ToDictionary(g => g.Key, g => g.First());

        lines.Add($"Events Z80: {events.Count}");
        lines.Add($"Cycles Enemy_UpdateOne total: {allCycles.Count}");
        lines.Add($"Cycles enemy0 inférés: {enemy0Cycles.Count}");
        lines.Add($"Centres géométriques enemy0: {enemy0Cycles.Count(c => c.IsAtCenter)}");
        lines.Add($"TryPreferred enemy0: {enemy0Cycles.Count(c => c.HasTryPreferred)}");
        lines.Add($"LocalDoorCheck enemy0: {enemy0Cycles.Count(c => c.LocalDoorCheck is not null)}");
        lines.Add($"Fallback enemy0: {enemy0Cycles.Count(c => c.HasFallback)}");
        lines.Add($"LocalDoorCheck + Fallback enemy0: {enemy0Cycles.Count(c => c.HasLocalDoorFallback)}");
        lines.Add($"ForcedReversal hit enemy0: {enemy0Cycles.Count(c => c.HasForcedReversalHit)}");
        lines.Add("");

        lines.Add("Règles local-door observées, groupées par pos + preferred/rejected:");
        foreach (var group in enemy0Cycles
                     .Where(c => c.LocalDoorCheck is not null)
                     .GroupBy(c => $"pos=({c.StartX:X2},{c.StartY:X2}) pref={c.Preferred0:X2} localC1={c.RejectedMaskAtLocalDoor:X1}")
                     .OrderBy(g => g.Key))
        {
            string samples = string.Join(",", group.Select(c => c.Sample).Take(12));
            lines.Add($"  {group.Key} count={group.Count()} samples=[{samples}]");
        }

        lines.Add("");
        lines.Add("Chemins significatifs enemy0, surtout les centres et les fallback:");

        int shown = 0;
        foreach (Z80EventCycle cycle in enemy0Cycles.Where(c => c.IsAtCenter || c.HasFallback || c.HasForcedReversalHit || c.HasTryPreferred).Take(140))
        {
            shown++;
            string transition = BuildFrameTransition(frameBySample, cycle.Sample);
            int staticAllowed = StaticMazeDirectionValidator.GetAllowedDirections(cycle.StartX, cycle.StartY);
            int v17Fallback = ChooseFallback(staticAllowed, cycle.RejectedMaskAtFallback != 0 ? cycle.RejectedMaskAtFallback : cycle.RejectedMaskAtLocalDoor, cycle.StartDir);

            lines.Add(
                $"sample {cycle.Sample:0000} center={(cycle.IsAtCenter ? 1 : 0)} " +
                $"pos=({cycle.StartX:X2},{cycle.StartY:X2}) startDir={cycle.StartDir:X2} pref0={cycle.Preferred0:X2} " +
                $"staticAllowed={staticAllowed:X1} local={(cycle.LocalDoorCheck is not null ? 1 : 0)} localC1={cycle.RejectedMaskAtLocalDoor:X1} " +
                $"fallback={(cycle.HasFallback ? 1 : 0)} fallbackC1={cycle.RejectedMaskAtFallback:X1} " +
                $"v17Fallback={v17Fallback:X2} moveDir={cycle.MoveDir:X2} commit=({cycle.CommitX:X2},{cycle.CommitY:X2}) " +
                $"path={cycle.PathKind}{transition}");
        }

        if (shown == 0)
            lines.Add("Aucun chemin significatif trouvé pour enemy0. Vérifie que le fichier Z80 events correspond bien à la trace MAME chargée.");

        lines.Add("");
        lines.Add("Lecture v17:");
        lines.Add("- startDir/pos viennent du premier événement après copie du slot vers 61BD/61BE/61BF, pas du breakpoint 42BA brut.");
        lines.Add("- v17Fallback applique l'ordre expérimental 01,02,04,08 sur staticAllowed, après rejet C1.");
        lines.Add("- Si v17Fallback == moveDir sur les fallback, on peut remplacer progressivement le guidage MoveOnePixel par une vraie logique fallback.");

        return lines;
    }

    private static string BuildFrameTransition(Dictionary<int, ArcadeTraceFrame> frameBySample, int sample)
    {
        if (!frameBySample.TryGetValue(sample - 1, out ArcadeTraceFrame? before) ||
            !frameBySample.TryGetValue(sample, out ArcadeTraceFrame? after) ||
            before.Enemies.Count == 0 || after.Enemies.Count == 0)
        {
            return string.Empty;
        }

        EnemySlotFrame b = before.Enemies[0];
        EnemySlotFrame a = after.Enemies[0];
        return $" | frame {sample - 1}->{sample}: dir {b.Dir}->{a.Dir} pos ({b.X},{b.Y})->({a.X},{a.Y})";
    }

    private static int ChooseFallback(int allowedMask, int rejectedMask, int currentDir)
    {
        int[] order = { 0x01, 0x02, 0x04, 0x08 };
        foreach (int candidate in order)
        {
            if ((rejectedMask & candidate) != 0)
                continue;
            if ((allowedMask & candidate) == 0)
                continue;
            return candidate;
        }

        return currentDir;
    }
}
