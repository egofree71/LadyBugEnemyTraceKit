using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab;
using LadyBugEnemyTraceLab.TraceLab.Compare;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

/// <summary>
/// v17 diagnostic candidate.
///
/// This is a step beyond v16: it no longer copies the final MoveOnePixel_4224 direction.
/// It uses the Z80 event path only to know which branch was taken by the arcade
/// (TryPreferred, LocalDoorCheck, Fallback, ForcedReversal), then recomputes the
/// final direction with an explicit rule:
///
/// - preferred accepted when there is no rejection/fallback;
/// - local-door rejection contributes to C1;
/// - fallback chooses the first non-rejected direction in 01,02,04,08 allowed by the static maze validator;
/// - local-door rejection without fallback keeps current direction;
/// - no TryPreferred keeps current direction.
///
/// It is still diagnostic because it uses Z80 events for branch/rejection masks,
/// but it deliberately does not use MoveOnePixel_4224 as a direction oracle.
/// </summary>
public static class LocalDoorFallbackCandidateTraceFactoryV17
{
    private static readonly int[] FallbackOrder = { 0x01, 0x02, 0x04, 0x08 };

    public static CandidateGenerationResult Create(IReadOnlyList<ArcadeTraceFrame> mameFrames, IReadOnlyList<Z80EventFrame> z80Events)
    {
        var result = new CandidateGenerationResult();
        if (mameFrames.Count == 0)
        {
            result.Messages.Add("Aucune frame MAME: candidate local-door/fallback v17 non générée.");
            return result;
        }
        if (z80Events.Count == 0)
        {
            result.Messages.Add("Aucun event Z80: candidate local-door/fallback v17 non générée.");
            return result;
        }

        List<Z80EventCycle> cycles = Z80EventCycleBuilder.BuildEnemy0Cycles(z80Events);
        Dictionary<int, Z80EventCycle> cycleBySample = cycles
            .Where(c => c.Move is not null)
            .GroupBy(c => c.Sample)
            .ToDictionary(g => g.Key, g => g.First());

        var state = CandidateEnemyState.FromFrame(mameFrames[0]);
        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.localDoorFallback.v17";
        state.WriteTo(first);
        result.Frames.Add(first);

        int samplesWithCycle = 0;
        int missingCycles = 0;
        int noTryKeepCurrent = 0;
        int preferredAccepted = 0;
        int localDoorRejected = 0;
        int localDoorNoFallbackKeepCurrent = 0;
        int fallbackUsed = 0;
        int forcedReversalUsed = 0;
        int fallbackMatchesMove = 0;
        int fallbackMismatchesMove = 0;
        int firstDivergence = -1;
        string firstDivergenceContext = string.Empty;

        for (int i = 1; i < mameFrames.Count; i++)
        {
            ArcadeTraceFrame targetFrame = mameFrames[i];
            int sample = targetFrame.Sample;
            string context;

            if (cycleBySample.TryGetValue(sample, out Z80EventCycle? cycle))
            {
                samplesWithCycle++;
                int currentDir = state.Direction & 0x0f;
                int chosenDir = currentDir;
                int preferred = cycle.Preferred0 & 0x0f;
                int rejectedMask = 0;
                int staticAllowed = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);

                if (cycle.HasForcedReversalHit)
                {
                    chosenDir = Opposite(currentDir);
                    forcedReversalUsed++;
                    context =
                        $"sample={sample} action=forced-reversal current={currentDir:X2} chosen={chosenDir:X2} " +
                        $"pos=({state.X:X2},{state.Y:X2})";
                }
                else if (cycle.HasTryPreferred)
                {
                    if (cycle.LocalDoorCheck is not null)
                    {
                        rejectedMask |= cycle.RejectedMaskAtLocalDoor;
                        localDoorRejected++;
                    }

                    if (cycle.HasFallback)
                    {
                        rejectedMask |= cycle.RejectedMaskAtFallback;
                        chosenDir = ChooseFallback(staticAllowed, rejectedMask, currentDir);
                        fallbackUsed++;

                        if (chosenDir == cycle.MoveDir)
                            fallbackMatchesMove++;
                        else
                            fallbackMismatchesMove++;

                        context =
                            $"sample={sample} action=fallback pos=({state.X:X2},{state.Y:X2}) " +
                            $"current={currentDir:X2} pref={preferred:X2} rejected={rejectedMask:X1} " +
                            $"staticAllowed={staticAllowed:X1} chosen={chosenDir:X2} z80Move={cycle.MoveDir:X2}";
                    }
                    else if (preferred != 0 && (rejectedMask & preferred) != 0)
                    {
                        chosenDir = currentDir;
                        localDoorNoFallbackKeepCurrent++;
                        context =
                            $"sample={sample} action=local-door-reject-keep-current pos=({state.X:X2},{state.Y:X2}) " +
                            $"current={currentDir:X2} pref={preferred:X2} rejected={rejectedMask:X1}";
                    }
                    else
                    {
                        chosenDir = preferred != 0 ? preferred : currentDir;
                        preferredAccepted++;
                        context =
                            $"sample={sample} action=preferred-accepted pos=({state.X:X2},{state.Y:X2}) " +
                            $"current={currentDir:X2} pref={preferred:X2} chosen={chosenDir:X2}";
                    }
                }
                else
                {
                    noTryKeepCurrent++;
                    context =
                        $"sample={sample} action=no-try-keep-current pos=({state.X:X2},{state.Y:X2}) " +
                        $"current={currentDir:X2}";
                }

                if (chosenDir != 0)
                    state.Direction = chosenDir;
            }
            else
            {
                missingCycles++;
                context = $"sample={sample} aucun cycle Z80 enemy0 trouvé; conservation direction={state.Direction:X2}";
            }

            state.StepOnePixel();

            ArcadeTraceFrame candidateFrame = TraceFrameCloner.Clone(targetFrame);
            candidateFrame.Schema = "ladybug.enemyTraceCandidate.localDoorFallback.v17";
            state.WriteTo(candidateFrame);
            result.Frames.Add(candidateFrame);

            if (firstDivergence < 0 && !Enemy0MovementEquals(candidateFrame, targetFrame))
            {
                firstDivergence = sample;
                firstDivergenceContext = context;
            }
        }

        result.Messages.Add("Candidate local-door/fallback v17 générée.");
        result.Messages.Add("Important: diagnostic intermédiaire. Elle n'utilise plus MoveOnePixel_4224 comme direction oracle, mais utilise encore les events Z80 pour connaître les branches et les masques C1.");
        result.Messages.Add($"Cycles enemy0 disponibles={cycles.Count}, samples avec cycle={samplesWithCycle}, cycles manquants={missingCycles}.");
        result.Messages.Add($"Chemins: no-try keep-current={noTryKeepCurrent}, preferred accepted={preferredAccepted}, local-door rejected={localDoorRejected}, local-door no-fallback keep-current={localDoorNoFallbackKeepCurrent}, fallback used={fallbackUsed}, forced-reversal used={forcedReversalUsed}.");
        result.Messages.Add($"Fallback recomputé: matches MoveOnePixel={fallbackMatchesMove}, mismatches MoveOnePixel={fallbackMismatchesMove}.");
        result.Messages.Add("Règle fallback testée: choisir dans l'ordre 01,02,04,08 la première direction autorisée par static-maze 0x3911 et absente du masque C1.");

        if (firstDivergence >= 0)
        {
            result.Messages.Add($"Première divergence mouvement enemy0 au sample {firstDivergence}.");
            result.Messages.Add("Contexte: " + firstDivergenceContext);
        }
        else
        {
            result.Messages.Add("Aucune divergence mouvement enemy0 détectée avec la candidate local-door/fallback v17.");
        }

        result.Messages.Add("Prochaine étape: remplacer les masques C1 fournis par Z80 par une vraie validation locale porte/tile 0x4130 côté C#.");
        return result;
    }

    private static int ChooseFallback(int allowedMask, int rejectedMask, int currentDir)
    {
        foreach (int candidate in FallbackOrder)
        {
            if ((rejectedMask & candidate) != 0)
                continue;
            if ((allowedMask & candidate) == 0)
                continue;
            return candidate;
        }

        return currentDir;
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
