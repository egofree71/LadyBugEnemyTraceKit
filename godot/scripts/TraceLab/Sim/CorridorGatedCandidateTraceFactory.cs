using System;
using System.Collections.Generic;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

/// <summary>
/// Experimental v10 candidate.
///
/// Goal:
/// - keep the validated pixel-by-pixel movement pipeline;
/// - keep using the static maze allowed-direction table;
/// - avoid treating every geometric center as an arcade decision node.
///
/// The key experiment is "corridor gating":
///
///     geometric center + simple corridor => keep moving straight
///     geometric center + side exit/dead-end => try preferred direction / fallback
///
/// This tests the observation that MAME may enter the center-check path at positions
/// such as (68,86) but still not run the normal TryPreferredDirection path.
/// </summary>
public static class CorridorGatedCandidateTraceFactory
{
    private static readonly int[] FallbackOrder =
    {
        0x01, // left
        0x02, // up
        0x04, // right
        0x08, // down
    };

    public static CandidateGenerationResult Create(IReadOnlyList<ArcadeTraceFrame> mameFrames)
    {
        var result = new CandidateGenerationResult();
        if (mameFrames.Count == 0)
        {
            result.Messages.Add("Aucune frame MAME: candidate corridor-gated non générée.");
            return result;
        }

        var state = CandidateEnemyState.FromFrame(mameFrames[0]);

        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.corridorGated.v10";
        state.WriteTo(first);
        result.Frames.Add(first);

        int geometricCenterCount = 0;
        int suppressedPreferredInCorridorCount = 0;
        int trueBranchCandidateCount = 0;
        int preferredAcceptedCount = 0;
        int preferredRejectedCount = 0;
        int keepCurrentCount = 0;
        int fallbackCount = 0;
        int safetyKeepCurrentCount = 0;
        int firstDivergencePrediction = -1;
        string firstDivergenceContext = string.Empty;

        for (int i = 1; i < mameFrames.Count; i++)
        {
            // v10 deliberately keeps the "pref N+1" timing because that previous
            // experiment pushed the first divergence much further than pref N.
            ArcadeTraceFrame decisionFrame = mameFrames[i];

            string decisionContext = BuildNoDecisionContext(state);

            if (state.IsAtDecisionCenter)
            {
                geometricCenterCount++;

                int currentDir = state.Direction & 0x0f;
                int opposite = Opposite(currentDir);
                int allowedMask = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);

                bool currentStillAllowed = currentDir != 0 && (allowedMask & currentDir) != 0;
                bool hasSideExit = (allowedMask & ~(currentDir | opposite) & 0x0f) != 0;
                bool shouldTryPreferredAtCenter = !currentStillAllowed || hasSideExit;

                decisionContext =
                    $"center=1 pos=({state.X:X2},{state.Y:X2}) dir={currentDir:X2} " +
                    $"opp={opposite:X2} allowed={StaticMazeDirectionValidator.FormatMask(allowedMask)} " +
                    $"currentAllowed={currentStillAllowed} hasSideExit={hasSideExit} " +
                    $"tryPreferred={shouldTryPreferredAtCenter}";

                if (shouldTryPreferredAtCenter)
                {
                    trueBranchCandidateCount++;

                    int preferred = GetPreferredForEnemy0(decisionFrame);
                    int rejectedMask = 0;

                    if (preferred != 0 && (allowedMask & preferred) != 0)
                    {
                        state.Direction = preferred;
                        preferredAcceptedCount++;
                        decisionContext += $" preferred={preferred:X2} accepted=1";
                    }
                    else
                    {
                        if (preferred != 0)
                        {
                            rejectedMask |= preferred;
                            preferredRejectedCount++;
                        }

                        decisionContext += $" preferred={preferred:X2} accepted=0 rejected={rejectedMask:X1}";

                        if (currentStillAllowed)
                        {
                            keepCurrentCount++;
                            decisionContext += " action=keep-current";
                        }
                        else
                        {
                            rejectedMask |= currentDir;
                            int chosen = ChooseFallback(allowedMask, rejectedMask, currentDir);

                            if (chosen == currentDir)
                                safetyKeepCurrentCount++;
                            else
                                fallbackCount++;

                            state.Direction = chosen;
                            decisionContext += $" action=fallback chosen={chosen:X2} rejected={rejectedMask:X1}";
                        }
                    }
                }
                else
                {
                    suppressedPreferredInCorridorCount++;
                    keepCurrentCount++;
                    decisionContext += " action=corridor-keep-current";
                }
            }

            state.StepOnePixel();

            ArcadeTraceFrame candidateFrame = TraceFrameCloner.Clone(mameFrames[i]);
            candidateFrame.Schema = "ladybug.enemyTraceCandidate.corridorGated.v10";
            state.WriteTo(candidateFrame);
            result.Frames.Add(candidateFrame);

            if (firstDivergencePrediction < 0 && !Enemy0MovementEquals(candidateFrame, mameFrames[i]))
            {
                firstDivergencePrediction = i;
                firstDivergenceContext = decisionContext;
            }
        }

        result.Messages.Add("Candidate corridor-gated v10 générée.");
        result.Messages.Add("Règle: hors centre géométrique, continuer tout droit. À un centre géométrique, ne tenter preferred[0] que si la direction courante n'est plus autorisée ou s'il existe une sortie latérale.");
        result.Messages.Add("Timing préféré: preferred[0] lu sur le sample N+1, comme la candidate pref N+1 précédente.");
        result.Messages.Add("Validation utilisée: StaticMazeDirectionValidator.GetAllowedDirections(x,y), donc table statique 0x3911 seulement.");
        result.Messages.Add("Limites connues: cette candidate ne modélise pas encore la validation locale des portes 0x4130 ni les demi-tours forcés 0x4189/0x4347.");
        result.Messages.Add($"Décisions: centres géométriques={geometricCenterCount}, corridors gardés={suppressedPreferredInCorridorCount}, embranchements/dead-ends={trueBranchCandidateCount}, preferred acceptées={preferredAcceptedCount}, preferred rejetées={preferredRejectedCount}, keep-current={keepCurrentCount}, fallbacks={fallbackCount}, safety-keep-current={safetyKeepCurrentCount}.");

        if (firstDivergencePrediction >= 0)
        {
            ArcadeTraceFrame m = mameFrames[firstDivergencePrediction];
            ArcadeTraceFrame c = result.Frames[firstDivergencePrediction];

            result.Messages.Add(
                $"Première divergence mouvement prévue au sample {m.Sample}: MAME enemy0=raw {m.Enemies[0].Raw} pos({m.Enemies[0].X},{m.Enemies[0].Y}) dir {m.Enemies[0].Dir}, " +
                $"candidate=raw {c.Enemies[0].Raw} pos({c.Enemies[0].X},{c.Enemies[0].Y}) dir {c.Enemies[0].Dir}.");
            result.Messages.Add("Contexte décision précédent: " + firstDivergenceContext);
        }
        else
        {
            result.Messages.Add("Aucune divergence mouvement enemy0 détectée avec la candidate corridor-gated v10.");
        }

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

        // Safety fallback: the arcade has more local state, but the trace tool should not
        // stop or invent a zero direction if the static table alone cannot decide.
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

    private static int GetPreferredForEnemy0(ArcadeTraceFrame frame)
    {
        if (frame.EnemyWork.Preferred.Count == 0)
            return 0;

        return Hex.ToByte(frame.EnemyWork.Preferred[0]) & 0x0f;
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

    private static string BuildNoDecisionContext(CandidateEnemyState state)
    {
        return $"center=0 pos=({state.X:X2},{state.Y:X2}) dir={state.Direction & 0x0f:X2} action=straight";
    }
}
