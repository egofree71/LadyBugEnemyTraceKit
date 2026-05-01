using System;
using System.Collections.Generic;
using System.Linq;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

/// <summary>
/// Experimental v13 candidate.
///
/// This is an explicit oracle/probe, not a final arcade AI.
/// It learns an enriched decision key from the loaded MAME trace and uses it only
/// at geometric decision centers. Outside centers, movement remains one pixel per
/// tick. If the enriched key has no hit, it falls back to the v11 strategy:
/// stable reversal rules, then corridor-gated preference timing N+1.
///
/// Purpose: determine whether the remaining v11 mismatch is caused by preference
/// timing / hidden state at centers rather than by the pixel movement itself.
/// </summary>
public static class TimingProbeCandidateTraceFactory
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
            result.Messages.Add("Aucune frame MAME: candidate timing-probe v13 non générée.");
            return result;
        }

        Dictionary<string, int> reversalRules = BuildStableReversalRules(mameFrames, out int observedReversals, out int ambiguousReversalKeys);
        Dictionary<string, int> decisionTable = BuildEnrichedDecisionTable(mameFrames, out int decisionConflicts);

        var state = CandidateEnemyState.FromFrame(mameFrames[0]);

        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.timingProbe.v13";
        state.WriteTo(first);
        result.Frames.Add(first);

        int geometricCenterCount = 0;
        int oracleHits = 0;
        int oracleMisses = 0;
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
            ArcadeTraceFrame previousFrame = mameFrames[i - 1];
            ArcadeTraceFrame currentFrame = mameFrames[i];
            string decisionContext = BuildNoDecisionContext(state);

            if (state.IsAtDecisionCenter)
            {
                geometricCenterCount++;

                string enrichedKey = BuildEnrichedDecisionKey(state, previousFrame, currentFrame);
                int currentDir = state.Direction & 0x0f;

                if (decisionTable.TryGetValue(enrichedKey, out int oracleDir))
                {
                    int allowedMask = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);
                    state.Direction = oracleDir;
                    oracleHits++;
                    decisionContext =
                        $"center=1 pos=({state.X:X2},{state.Y:X2}) dir={currentDir:X2} " +
                        $"allowed={StaticMazeDirectionValidator.FormatMask(allowedMask)} " +
                        $"action=enriched-timing-oracle chosen={oracleDir:X2} " +
                        $"prefCur={Preferred0(previousFrame):X2} prefNext={Preferred0(currentFrame):X2}";
                }
                else
                {
                    oracleMisses++;
                    string reversalKey = BuildVisibleKey(state.X, state.Y, currentDir);

                    if (reversalRules.TryGetValue(reversalKey, out int forcedDir))
                    {
                        int allowedMask = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);
                        state.Direction = forcedDir;
                        learnedForcedReversalCount++;
                        decisionContext =
                            $"center=1 pos=({state.X:X2},{state.Y:X2}) dir={currentDir:X2} " +
                            $"allowed={StaticMazeDirectionValidator.FormatMask(allowedMask)} " +
                            $"action=learned-reversal forced={forcedDir:X2}";
                    }
                    else
                    {
                        ApplyCorridorGatedFallback(
                            state,
                            currentFrame,
                            ref suppressedPreferredInCorridorCount,
                            ref trueBranchCandidateCount,
                            ref preferredAcceptedCount,
                            ref preferredRejectedCount,
                            ref keepCurrentCount,
                            ref fallbackCount,
                            ref safetyKeepCurrentCount,
                            out decisionContext);
                    }
                }
            }

            state.StepOnePixel();

            ArcadeTraceFrame candidateFrame = TraceFrameCloner.Clone(currentFrame);
            candidateFrame.Schema = "ladybug.enemyTraceCandidate.timingProbe.v13";
            state.WriteTo(candidateFrame);
            result.Frames.Add(candidateFrame);

            if (firstDivergencePrediction < 0 && !Enemy0MovementEquals(candidateFrame, currentFrame))
            {
                firstDivergencePrediction = i;
                firstDivergenceContext = decisionContext;
            }
        }

        result.Messages.Add("Candidate timing-probe v13 générée.");
        result.Messages.Add("Règle: aux centres géométriques, essayer d'abord une clé enrichie apprise depuis la trace MAME: pos, dir, allowed, preferred courant/suivant, vecteurs preferred, chase, 61B6..61B9.");
        result.Messages.Add("Important: c'est une candidate oracle/diagnostic. Elle apprend la trace chargée et ne doit pas être copiée telle quelle dans le jeu final.");
        result.Messages.Add("But: vérifier si les divergences restantes de v11 viennent du timing exact de 61C4/timers plutôt que du déplacement pixel.");
        result.Messages.Add($"Table enrichie: entrées={decisionTable.Count}, conflits={decisionConflicts}, oracle hits={oracleHits}, oracle misses={oracleMisses}.");
        result.Messages.Add($"Demi-tours MAME observés={observedReversals}, règles stables v11={reversalRules.Count}, clés de demi-tour ambiguës={ambiguousReversalKeys}, reversals fallback utilisés={learnedForcedReversalCount}.");
        result.Messages.Add($"Décisions fallback corridor-gated: centres={geometricCenterCount}, corridors gardés={suppressedPreferredInCorridorCount}, embranchements/dead-ends={trueBranchCandidateCount}, preferred acceptées={preferredAcceptedCount}, preferred rejetées={preferredRejectedCount}, keep-current={keepCurrentCount}, fallbacks={fallbackCount}, safety-keep-current={safetyKeepCurrentCount}.");

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
            result.Messages.Add("Aucune divergence mouvement enemy0 détectée avec la candidate timing-probe v13.");
        }

        return result;
    }

    private static Dictionary<string, int> BuildEnrichedDecisionTable(IReadOnlyList<ArcadeTraceFrame> frames, out int conflictCount)
    {
        var table = new Dictionary<string, int>();
        conflictCount = 0;

        for (int i = 0; i < frames.Count - 1; i++)
        {
            if (frames[i].Enemies.Count == 0 || frames[i + 1].Enemies.Count == 0)
                continue;

            CandidateEnemyState state = CandidateEnemyState.FromFrame(frames[i]);
            if (!state.IsAtDecisionCenter)
                continue;

            string key = BuildEnrichedDecisionKey(state, frames[i], frames[i + 1]);
            int nextDir = Hex.ToByte(frames[i + 1].Enemies[0].Dir) & 0x0f;

            if (table.TryGetValue(key, out int existing))
            {
                if (existing != nextDir)
                    conflictCount++;
            }
            else
            {
                table[key] = nextDir;
            }
        }

        return table;
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
                rules[pair.Key] = s.NextDirections.First();
            else
                ambiguousKeys++;
        }

        return rules;
    }

    private static void ApplyCorridorGatedFallback(
        CandidateEnemyState state,
        ArcadeTraceFrame decisionFrame,
        ref int suppressedPreferredInCorridorCount,
        ref int trueBranchCandidateCount,
        ref int preferredAcceptedCount,
        ref int preferredRejectedCount,
        ref int keepCurrentCount,
        ref int fallbackCount,
        ref int safetyKeepCurrentCount,
        out string decisionContext)
    {
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
            $"tryPreferred={shouldTryPreferredAtCenter} action=v11-fallback";

        if (shouldTryPreferredAtCenter)
        {
            trueBranchCandidateCount++;
            int preferred = Preferred0(decisionFrame);
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

    private static string BuildEnrichedDecisionKey(CandidateEnemyState state, ArcadeTraceFrame currentFrame, ArcadeTraceFrame nextFrame)
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

    private static string JoinList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "" : string.Join(",", values);
    }

    private static string Safe(string? value) => string.IsNullOrEmpty(value) ? "00" : value;

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
