using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Compare;

public static class Z80LocalDoorFallbackAnalyzer
{
    public static IReadOnlyList<string> BuildReport(IReadOnlyList<ArcadeTraceFrame> mameFrames, IReadOnlyList<Z80EventFrame> events)
    {
        var lines = new List<string>();
        lines.Add("--- ANALYSE Z80 LOCAL-DOOR/FALLBACK v16 ---");

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
        lines.Add("Chemins significatifs enemy0, surtout les centres et les fallback:");

        int shown = 0;
        foreach (Z80EventCycle cycle in enemy0Cycles.Where(c => c.IsAtCenter || c.HasFallback || c.HasForcedReversalHit || c.HasTryPreferred).Take(120))
        {
            shown++;
            string transition = BuildFrameTransition(frameBySample, cycle.Sample);
            lines.Add(
                $"sample {cycle.Sample:0000} center={(cycle.IsAtCenter ? 1 : 0)} " +
                $"pos=({cycle.EnterX:X2},{cycle.EnterY:X2}) enterDir={cycle.EnterDir:X2} pref0={cycle.Preferred0:X2} " +
                $"local={(cycle.LocalDoorCheck is not null ? 1 : 0)} localC1={cycle.RejectedMaskAtLocalDoor:X2} " +
                $"fallback={(cycle.HasFallback ? 1 : 0)} fallbackC1={cycle.RejectedMaskAtFallback:X2} " +
                $"moveDir={cycle.MoveDir:X2} commit=({cycle.CommitX:X2},{cycle.CommitY:X2}) " +
                $"path={cycle.PathKind}{transition}");
        }

        if (shown == 0)
            lines.Add("Aucun chemin significatif trouvé pour enemy0. Vérifie que le fichier Z80 events correspond bien à la trace MAME chargée.");

        lines.Add("");
        lines.Add("Lecture v16: si tu vois local=1 + fallback=1, la direction préférée a été rejetée par la validation locale porte/tile avant le fallback arcade.");
        lines.Add("C'est le cas à vérifier pour les anciens demi-tours apparents comme (68,96) 04->01 et (48,96) 01->02.");

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
}
