using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab;
using LadyBugEnemyTraceLab.TraceLab.Compare;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

/// <summary>
/// v16 diagnostic candidate driven by real Z80 event cycles.
///
/// This is deliberately not final arcade AI. It uses the MoveOnePixel_4224 event direction
/// emitted by the debugger/Lua trace for enemy0, then applies the same one-pixel movement in C#.
/// Its purpose is to validate event-to-frame alignment and to isolate the missing rule:
/// local door/tile rejection followed by fallback at decision centers.
/// </summary>
public static class Z80EventGuidedCandidateTraceFactory
{
    public static CandidateGenerationResult Create(IReadOnlyList<ArcadeTraceFrame> mameFrames, IReadOnlyList<Z80EventFrame> z80Events)
    {
        var result = new CandidateGenerationResult();
        if (mameFrames.Count == 0)
        {
            result.Messages.Add("Aucune frame MAME: candidate Z80-guided v16 non générée.");
            return result;
        }
        if (z80Events.Count == 0)
        {
            result.Messages.Add("Aucun event Z80: candidate Z80-guided v16 non générée.");
            return result;
        }

        List<Z80EventCycle> cycles = Z80EventCycleBuilder.BuildEnemy0Cycles(z80Events);
        Dictionary<int, Z80EventCycle> cycleBySample = cycles
            .Where(c => c.Move is not null)
            .GroupBy(c => c.Sample)
            .ToDictionary(g => g.Key, g => g.First());

        var state = CandidateEnemyState.FromFrame(mameFrames[0]);
        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.z80Guided.v16";
        state.WriteTo(first);
        result.Frames.Add(first);

        int guidedSamples = 0;
        int missingCycles = 0;
        int centerGuided = 0;
        int localDoorFallbackGuided = 0;
        int fallbackGuided = 0;
        int forcedReversalGuided = 0;
        int straightGuided = 0;
        int firstDivergence = -1;
        string firstDivergenceContext = string.Empty;

        for (int i = 1; i < mameFrames.Count; i++)
        {
            ArcadeTraceFrame targetFrame = mameFrames[i];
            int sample = targetFrame.Sample;
            string context;

            if (cycleBySample.TryGetValue(sample, out Z80EventCycle? cycle))
            {
                int moveDir = cycle.MoveDir & 0x0f;
                if (moveDir != 0)
                    state.Direction = moveDir;

                guidedSamples++;
                if (cycle.IsAtCenter)
                    centerGuided++;
                if (cycle.HasLocalDoorFallback)
                    localDoorFallbackGuided++;
                if (cycle.HasFallback)
                    fallbackGuided++;
                if (cycle.HasForcedReversalHit)
                    forcedReversalGuided++;
                if (!cycle.IsAtCenter && !cycle.HasFallback && !cycle.HasForcedReversalHit)
                    straightGuided++;

                context =
                    $"sample={sample} z80={cycle.PathKind} enter=({cycle.EnterX:X2},{cycle.EnterY:X2}) " +
                    $"enterDir={cycle.EnterDir:X2} pref0={cycle.Preferred0:X2} moveDir={moveDir:X2} " +
                    $"localC1={cycle.RejectedMaskAtLocalDoor:X2} fallbackC1={cycle.RejectedMaskAtFallback:X2}";
            }
            else
            {
                missingCycles++;
                context = $"sample={sample} aucun cycle Z80 enemy0 trouvé; conservation direction={state.Direction:X2}";
            }

            state.StepOnePixel();

            ArcadeTraceFrame candidateFrame = TraceFrameCloner.Clone(targetFrame);
            candidateFrame.Schema = "ladybug.enemyTraceCandidate.z80Guided.v16";
            state.WriteTo(candidateFrame);
            result.Frames.Add(candidateFrame);

            if (firstDivergence < 0 && !Enemy0MovementEquals(candidateFrame, targetFrame))
            {
                firstDivergence = sample;
                firstDivergenceContext = context;
            }
        }

        result.Messages.Add("Candidate Z80-guided v16 générée.");
        result.Messages.Add("Important: diagnostic, pas IA finale. Elle utilise les directions MoveOnePixel_4224 issues du vrai code Z80 pour valider l'alignement event->frame.");
        result.Messages.Add($"Cycles enemy0 disponibles={cycles.Count}, samples guidés={guidedSamples}, cycles manquants={missingCycles}.");
        result.Messages.Add($"Guidage centres={centerGuided}, local-door+fallback={localDoorFallbackGuided}, fallback total={fallbackGuided}, forced-reversal hit={forcedReversalGuided}, straight/test-only={straightGuided}.");

        if (firstDivergence >= 0)
        {
            result.Messages.Add($"Première divergence mouvement enemy0 au sample {firstDivergence}.");
            result.Messages.Add("Contexte: " + firstDivergenceContext);
        }
        else
        {
            result.Messages.Add("Aucune divergence mouvement enemy0 détectée avec la candidate Z80-guided v16.");
        }

        result.Messages.Add("Prochaine étape après validation: remplacer ce guidage par une vraie implémentation MovementValidator = static maze 0x3911 + local door/tile 0x4130 + fallback 0x4241.");
        return result;
    }

    private static bool Enemy0MovementEquals(ArcadeTraceFrame a, ArcadeTraceFrame b)
    {
        if (a.Enemies.Count == 0 || b.Enemies.Count == 0)
            return false;

        EnemySlotFrame ea = a.Enemies[0];
        EnemySlotFrame eb = b.Enemies[0];

        return string.Equals(ea.Raw, eb.Raw, StringComparison.Ordinal)
               && string.Equals(ea.Dir, eb.Dir, StringComparison.Ordinal)
               && string.Equals(ea.X, eb.X, StringComparison.Ordinal)
               && string.Equals(ea.Y, eb.Y, StringComparison.Ordinal)
               && ea.CollisionActive == eb.CollisionActive;
    }
}
