using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

/// <summary>
/// Experimental v11 candidate.
///
/// It keeps the v10 corridor-gated logic, but adds a diagnostic layer before it:
/// if the loaded MAME trace shows an unambiguous repeated reversal for the same
/// visible key (x, y, current direction), force that reversal.
///
/// This is intentionally a probe, not a final arcade implementation: it learns
/// from the trace being compared. If it improves the diff, we know the missing
/// model is a forced-reversal layer rather than normal maze navigation.
/// </summary>
public static class LearnedReversalProbeCandidateTraceFactory
{
    private static readonly int[] FallbackOrder =
    {
        0x01,
        0x02,
        0x04,
        0x08,
    };

    public static CandidateGenerationResult Create(IReadOnlyList<ArcadeTraceFrame> mameFrames)
    {
        var result = new CandidateGenerationResult();
        if (mameFrames.Count == 0)
        {
            result.Messages.Add("Aucune frame MAME: candidate reversal-probe v11 non générée.");
            return result;
        }

        Dictionary<string, int> reversalRules = BuildStableReversalRules(mameFrames, out int observedReversals, out int ambiguousKeys);
        var state = CandidateEnemyState.FromFrame(mameFrames[0]);

        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.reversalProbe.v11";
        state.WriteTo(first);
        result.Frames.Add(first);

        int geometricCenterCount = 0;
        int learnedForcedReversalCount = 0;
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
            ArcadeTraceFrame decisionFrame = mameFrames[i];
            string decisionContext = BuildNoDecisionContext(state);

            if (state.IsAtDecisionCenter)
            {
                geometricCenterCount++;

                int currentDir = state.Direction & 0x0f;
                string reversalKey = BuildVisibleKey(state.X, state.Y, currentDir);

                if (reversalRules.TryGetValue(reversalKey, out int forcedDir))
                {
                    state.Direction = forcedDir;
                    learnedForcedReversalCount++;
                    int allowedMask = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);
                    decisionContext =
                        $"center=1 pos=({state.X:X2},{state.Y:X2}) dir={currentDir:X2} " +
                        $"allowed={StaticMazeDirectionValidator.FormatMask(allowedMask)} " +
                        $"action=learned-reversal forced={forcedDir:X2}";
                }
                else
                {
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
            }

            state.StepOnePixel();

            ArcadeTraceFrame candidateFrame = TraceFrameCloner.Clone(mameFrames[i]);
            candidateFrame.Schema = "ladybug.enemyTraceCandidate.reversalProbe.v11";
            state.WriteTo(candidateFrame);
            result.Frames.Add(candidateFrame);

            if (firstDivergencePrediction < 0 && !Enemy0MovementEquals(candidateFrame, mameFrames[i]))
            {
                firstDivergencePrediction = i;
                firstDivergenceContext = decisionContext;
            }
        }

        result.Messages.Add("Candidate reversal-probe v11 générée.");
        result.Messages.Add("Règle: appliquer d'abord les demi-tours stables appris dans la trace MAME, puis retomber sur la logique corridor-gated v10.");
        result.Messages.Add("Important: cette candidate est un outil de diagnostic. Elle apprend la trace courante et ne doit pas être copiée telle quelle dans le jeu final.");
        result.Messages.Add($"Demi-tours MAME observés={observedReversals}, clés de demi-tour stables apprises={reversalRules.Count}, clés ambiguës ignorées={ambiguousKeys}.");
        result.Messages.Add($"Décisions: centres géométriques={geometricCenterCount}, learned-reversals={learnedForcedReversalCount}, corridors gardés={suppressedPreferredInCorridorCount}, embranchements/dead-ends={trueBranchCandidateCount}, preferred acceptées={preferredAcceptedCount}, preferred rejetées={preferredRejectedCount}, keep-current={keepCurrentCount}, fallbacks={fallbackCount}, safety-keep-current={safetyKeepCurrentCount}.");

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
            result.Messages.Add("Aucune divergence mouvement enemy0 détectée avec la candidate reversal-probe v11.");
        }

        return result;
    }

    private static Dictionary<string, int> BuildStableReversalRules(
        IReadOnlyList<ArcadeTraceFrame> frames,
        out int observedReversals,
        out int ambiguousKeys)
    {
        var stats = new Dictionary<string, RuleStats>();
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

            if (!stats.TryGetValue(key, out RuleStats? ruleStats))
            {
                ruleStats = new RuleStats();
                stats[key] = ruleStats;
            }

            ruleStats.Add(nextDir, isReversal);
            if (isReversal)
                observedReversals++;
        }

        var rules = new Dictionary<string, int>();
        ambiguousKeys = 0;

        foreach (KeyValuePair<string, RuleStats> pair in stats)
        {
            RuleStats s = pair.Value;
            if (s.ReversalCount == 0)
                continue;

            if (s.NonReversalCount == 0 && s.NextDirections.Count == 1)
            {
                rules[pair.Key] = s.NextDirections.First();
            }
            else
            {
                ambiguousKeys++;
            }
        }

        return rules;
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

    private static string BuildVisibleKey(int x, int y, int dir) => $"{x:X2}:{y:X2}:{dir:X2}";

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

    private sealed class RuleStats
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
