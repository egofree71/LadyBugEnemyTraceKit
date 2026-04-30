using System;
using System.Collections.Generic;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

public static class CandidateEnemySimulator
{
    public static CandidateGenerationResult CreatePixelCenterCandidate(IReadOnlyList<ArcadeTraceFrame> mameFrames)
    {
        var result = new CandidateGenerationResult();
        if (mameFrames.Count == 0)
        {
            result.Messages.Add("Aucune frame MAME: candidate non générée.");
            return result;
        }

        var state = CandidateEnemyState.FromFrame(mameFrames[0]);
        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.pixelCenter.v1";
        state.WriteTo(first);
        result.Frames.Add(first);

        int firstDivergencePrediction = -1;

        for (int i = 1; i < mameFrames.Count; i++)
        {
            ArcadeTraceFrame previousMame = mameFrames[i - 1];

            if (state.IsAtDecisionCenter)
            {
                int preferred = GetPreferredForEnemy0(previousMame);
                if (preferred != 0)
                    state.Direction = preferred;
            }

            state.StepOnePixel();

            ArcadeTraceFrame candidate = TraceFrameCloner.Clone(mameFrames[i]);
            candidate.Schema = "ladybug.enemyTraceCandidate.pixelCenter.v1";
            state.WriteTo(candidate);
            result.Frames.Add(candidate);

            if (firstDivergencePrediction < 0 && !Enemy0MovementEquals(candidate, mameFrames[i]))
                firstDivergencePrediction = i;
        }

        result.Messages.Add("Candidate pixel+centres générée.");
        result.Messages.Add("Règle utilisée: à un centre de décision, prendre preferred[0] du sample MAME précédent, puis avancer d'un pixel.");
        result.Messages.Add("Cette candidate ne valide pas encore le labyrinthe, les portes, les fallbacks ni les demi-tours forcés.");
        if (firstDivergencePrediction >= 0)
        {
            ArcadeTraceFrame m = mameFrames[firstDivergencePrediction];
            ArcadeTraceFrame c = result.Frames[firstDivergencePrediction];
            result.Messages.Add(
                $"Première divergence mouvement prévue au sample {m.Sample}: MAME enemy0=raw {m.Enemies[0].Raw} pos({m.Enemies[0].X},{m.Enemies[0].Y}) dir {m.Enemies[0].Dir}, " +
                $"candidate=raw {c.Enemies[0].Raw} pos({c.Enemies[0].X},{c.Enemies[0].Y}) dir {c.Enemies[0].Dir}.");
        }
        else
        {
            result.Messages.Add("Aucune divergence mouvement enemy0 détectée par cette candidate.");
        }

        return result;
    }


    public static CandidateGenerationResult CreateStaticMazeCandidate(IReadOnlyList<ArcadeTraceFrame> mameFrames)
    {
        var result = new CandidateGenerationResult();
        if (mameFrames.Count == 0)
        {
            result.Messages.Add("Aucune frame MAME: candidate non générée.");
            return result;
        }

        var state = CandidateEnemyState.FromFrame(mameFrames[0]);
        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.staticMaze.v1";
        state.WriteTo(first);
        result.Frames.Add(first);

        int firstDivergencePrediction = -1;
        int preferredAcceptedCount = 0;
        int preferredRejectedCount = 0;
        int keepCurrentCount = 0;
        int fallbackCount = 0;
        int[] fallbackOrder = { 0x01, 0x02, 0x04, 0x08 };

        for (int i = 1; i < mameFrames.Count; i++)
        {
            ArcadeTraceFrame previousMame = mameFrames[i - 1];

            if (state.IsAtDecisionCenter)
            {
                int preferred = GetPreferredForEnemy0(previousMame);
                int allowedMask = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);
                int rejectedMask = 0;

                if (preferred != 0 && (allowedMask & preferred) != 0)
                {
                    state.Direction = preferred;
                    preferredAcceptedCount++;
                }
                else
                {
                    if (preferred != 0)
                    {
                        rejectedMask |= preferred;
                        preferredRejectedCount++;
                    }

                    if ((allowedMask & state.Direction) != 0)
                    {
                        keepCurrentCount++;
                    }
                    else
                    {
                        rejectedMask |= state.Direction;
                        int chosen = state.Direction;
                        foreach (int candidate in fallbackOrder)
                        {
                            if ((rejectedMask & candidate) != 0)
                                continue;

                            if ((allowedMask & candidate) == 0)
                                continue;

                            chosen = candidate;
                            break;
                        }

                        state.Direction = chosen;
                        fallbackCount++;
                    }
                }
            }

            state.StepOnePixel();

            ArcadeTraceFrame candidateFrame = TraceFrameCloner.Clone(mameFrames[i]);
            candidateFrame.Schema = "ladybug.enemyTraceCandidate.staticMaze.v1";
            state.WriteTo(candidateFrame);
            result.Frames.Add(candidateFrame);

            if (firstDivergencePrediction < 0 && !Enemy0MovementEquals(candidateFrame, mameFrames[i]))
                firstDivergencePrediction = i;
        }

        result.Messages.Add("Candidate static-maze générée.");
        result.Messages.Add("Règle utilisée: aux centres, essayer preferred[0]; si la table ROM 0x0DA2 / validation 0x3911 le refuse, garder la direction courante si elle est autorisée; sinon fallback 01,02,04,08.");
        result.Messages.Add("Cette candidate NE gère pas encore la validation locale des portes 0x4130, ni les demi-tours forcés 0x4189 / 0x4347.");
        result.Messages.Add($"Décisions: preferred acceptées={preferredAcceptedCount}, preferred rejetées={preferredRejectedCount}, keep-current={keepCurrentCount}, fallbacks={fallbackCount}.");

        if (firstDivergencePrediction >= 0)
        {
            ArcadeTraceFrame m = mameFrames[firstDivergencePrediction];
            ArcadeTraceFrame c = result.Frames[firstDivergencePrediction];
            CandidateEnemyState prevState = CandidateEnemyState.FromFrame(mameFrames[firstDivergencePrediction - 1]);
            int prevPreferred = GetPreferredForEnemy0(mameFrames[firstDivergencePrediction - 1]);
            int allowedMask = StaticMazeDirectionValidator.GetAllowedDirections(prevState.X, prevState.Y);
            result.Messages.Add(
                $"Première divergence mouvement prévue au sample {m.Sample}: MAME enemy0=raw {m.Enemies[0].Raw} pos({m.Enemies[0].X},{m.Enemies[0].Y}) dir {m.Enemies[0].Dir}, " +
                $"candidate=raw {c.Enemies[0].Raw} pos({c.Enemies[0].X},{c.Enemies[0].Y}) dir {c.Enemies[0].Dir}.");
            result.Messages.Add(
                $"Contexte décision précédente: pos=({prevState.X:X2},{prevState.Y:X2}), dir={prevState.Direction:X2}, pref={prevPreferred:X2}, allowedMask={StaticMazeDirectionValidator.FormatMask(allowedMask)}, center={prevState.IsAtDecisionCenter}.");
        }
        else
        {
            result.Messages.Add("Aucune divergence mouvement enemy0 détectée par cette candidate static-maze.");
        }

        return result;
    }



    public static CandidateGenerationResult CreateStaticMazeCurrentPrefCandidate(IReadOnlyList<ArcadeTraceFrame> mameFrames)
    {
        var result = new CandidateGenerationResult();
        if (mameFrames.Count == 0)
        {
            result.Messages.Add("Aucune frame MAME: candidate non générée.");
            return result;
        }

        var state = CandidateEnemyState.FromFrame(mameFrames[0]);
        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.staticMazeCurrentPref.v1";
        state.WriteTo(first);
        result.Frames.Add(first);

        int firstDivergencePrediction = -1;
        int preferredAcceptedCount = 0;
        int preferredRejectedCount = 0;
        int keepCurrentCount = 0;
        int fallbackCount = 0;
        int[] fallbackOrder = { 0x01, 0x02, 0x04, 0x08 };

        for (int i = 1; i < mameFrames.Count; i++)
        {
            ArcadeTraceFrame decisionFrame = mameFrames[i];

            if (state.IsAtDecisionCenter)
            {
                int preferred = GetPreferredForEnemy0(decisionFrame);
                int allowedMask = StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y);
                int rejectedMask = 0;

                if (preferred != 0 && (allowedMask & preferred) != 0)
                {
                    state.Direction = preferred;
                    preferredAcceptedCount++;
                }
                else
                {
                    if (preferred != 0)
                    {
                        rejectedMask |= preferred;
                        preferredRejectedCount++;
                    }

                    if ((allowedMask & state.Direction) != 0)
                    {
                        keepCurrentCount++;
                    }
                    else
                    {
                        rejectedMask |= state.Direction;
                        int chosen = state.Direction;
                        foreach (int candidate in fallbackOrder)
                        {
                            if ((rejectedMask & candidate) != 0)
                                continue;
                            if ((allowedMask & candidate) == 0)
                                continue;
                            chosen = candidate;
                            break;
                        }
                        state.Direction = chosen;
                        fallbackCount++;
                    }
                }
            }

            state.StepOnePixel();
            ArcadeTraceFrame candidateFrame = TraceFrameCloner.Clone(mameFrames[i]);
            candidateFrame.Schema = "ladybug.enemyTraceCandidate.staticMazeCurrentPref.v1";
            state.WriteTo(candidateFrame);
            result.Frames.Add(candidateFrame);

            if (firstDivergencePrediction < 0 && !Enemy0MovementEquals(candidateFrame, mameFrames[i]))
                firstDivergencePrediction = i;
        }

        result.Messages.Add("Candidate static-maze + pref sample suivant générée.");
        result.Messages.Add("Hypothèse testée: la décision sample N->N+1 utilise preferred[0] visible dans le sample N+1, pas forcément celui du sample N.");
        result.Messages.Add("Cette candidate garde seulement la validation 0x3911; elle ne gère toujours pas 0x4130 local-door ni 0x4189/0x4347 forced reversal.");
        result.Messages.Add($"Décisions: preferred acceptées={preferredAcceptedCount}, preferred rejetées={preferredRejectedCount}, keep-current={keepCurrentCount}, fallbacks={fallbackCount}.");

        if (firstDivergencePrediction >= 0)
        {
            ArcadeTraceFrame m = mameFrames[firstDivergencePrediction];
            ArcadeTraceFrame c = result.Frames[firstDivergencePrediction];
            CandidateEnemyState prevState = CandidateEnemyState.FromFrame(mameFrames[firstDivergencePrediction - 1]);
            int prefSampleN = GetPreferredForEnemy0(mameFrames[firstDivergencePrediction - 1]);
            int prefSampleN1 = GetPreferredForEnemy0(mameFrames[firstDivergencePrediction]);
            int allowedMask = StaticMazeDirectionValidator.GetAllowedDirections(prevState.X, prevState.Y);
            result.Messages.Add(
                $"Première divergence mouvement prévue au sample {m.Sample}: MAME enemy0=raw {m.Enemies[0].Raw} pos({m.Enemies[0].X},{m.Enemies[0].Y}) dir {m.Enemies[0].Dir}, " +
                $"candidate=raw {c.Enemies[0].Raw} pos({c.Enemies[0].X},{c.Enemies[0].Y}) dir {c.Enemies[0].Dir}.");
            result.Messages.Add(
                $"Contexte décision précédente: pos=({prevState.X:X2},{prevState.Y:X2}), dir={prevState.Direction:X2}, prefSampleN={prefSampleN:X2}, prefSampleN+1={prefSampleN1:X2}, allowedMask={StaticMazeDirectionValidator.FormatMask(allowedMask)}, center={prevState.IsAtDecisionCenter}.");
        }
        else
        {
            result.Messages.Add("Aucune divergence mouvement enemy0 détectée par cette candidate static-maze/current-pref.");
        }

        return result;
    }


    public static CandidateGenerationResult CreateCenterOracleCandidate(IReadOnlyList<ArcadeTraceFrame> mameFrames)
    {
        var result = new CandidateGenerationResult();
        if (mameFrames.Count == 0)
        {
            result.Messages.Add("Aucune frame MAME: candidate non générée.");
            return result;
        }

        var decisionTable = BuildFullDecisionTable(mameFrames, out int conflicts);
        var state = CandidateEnemyState.FromFrame(mameFrames[0]);

        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.centerOracle.v1";
        state.WriteTo(first);
        result.Frames.Add(first);

        int usedOracle = 0;
        int misses = 0;
        int firstDivergencePrediction = -1;

        for (int i = 1; i < mameFrames.Count; i++)
        {
            ArcadeTraceFrame previousMame = mameFrames[i - 1];

            if (state.IsAtDecisionCenter)
            {
                var key = FullDecisionKey.FromFrame(previousMame);
                if (decisionTable.TryGetValue(key, out int nextDir))
                {
                    state.Direction = nextDir;
                    usedOracle++;
                }
                else
                {
                    misses++;
                    int preferred = GetPreferredForEnemy0(previousMame);
                    if (preferred != 0)
                        state.Direction = preferred;
                }
            }

            state.StepOnePixel();

            ArcadeTraceFrame candidate = TraceFrameCloner.Clone(mameFrames[i]);
            candidate.Schema = "ladybug.enemyTraceCandidate.centerOracle.v1";
            state.WriteTo(candidate);
            result.Frames.Add(candidate);

            if (firstDivergencePrediction < 0 && !Enemy0MovementEquals(candidate, mameFrames[i]))
                firstDivergencePrediction = i;
        }

        result.Messages.Add("Candidate oracle-centres générée.");
        result.Messages.Add("Règle utilisée: aux centres seulement, utiliser la direction MAME apprise avec une clé enrichie: pos, dir, preferred[0..3], chase timers, 61B6..61B9. Hors centres: simple mouvement pixel par pixel.");
        result.Messages.Add("Cette candidate n'est pas encore l'IA arcade: elle sert à vérifier que les divergences restantes viennent bien de la prise de décision aux centres, pas du déplacement pixel.");
        result.Messages.Add($"Décisions oracle utilisées={usedOracle}, misses={misses}, conflits de clé enrichie={conflicts}.");

        if (firstDivergencePrediction >= 0)
        {
            ArcadeTraceFrame m = mameFrames[firstDivergencePrediction];
            ArcadeTraceFrame c = result.Frames[firstDivergencePrediction];
            result.Messages.Add(
                $"Première divergence mouvement prévue au sample {m.Sample}: MAME enemy0=raw {m.Enemies[0].Raw} pos({m.Enemies[0].X},{m.Enemies[0].Y}) dir {m.Enemies[0].Dir}, " +
                $"candidate=raw {c.Enemies[0].Raw} pos({c.Enemies[0].X},{c.Enemies[0].Y}) dir {c.Enemies[0].Dir}.");
        }
        else
        {
            result.Messages.Add("Aucune divergence mouvement enemy0 détectée avec l'oracle-centres.");
        }

        return result;
    }

    private static Dictionary<FullDecisionKey, int> BuildFullDecisionTable(IReadOnlyList<ArcadeTraceFrame> frames, out int conflictCount)
    {
        var table = new Dictionary<FullDecisionKey, int>();
        conflictCount = 0;

        for (int i = 0; i < frames.Count - 1; i++)
        {
            if (frames[i].Enemies.Count == 0 || frames[i + 1].Enemies.Count == 0)
                continue;

            var state = CandidateEnemyState.FromFrame(frames[i]);
            if (!state.IsAtDecisionCenter)
                continue;

            var key = FullDecisionKey.FromFrame(frames[i]);
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

    public static CandidateGenerationResult CreateLearnedDecisionCandidate(IReadOnlyList<ArcadeTraceFrame> mameFrames)
    {
        var result = new CandidateGenerationResult();
        if (mameFrames.Count == 0)
        {
            result.Messages.Add("Aucune frame MAME: candidate non générée.");
            return result;
        }

        var state = CandidateEnemyState.FromFrame(mameFrames[0]);

        ArcadeTraceFrame first = TraceFrameCloner.Clone(mameFrames[0]);
        first.Schema = "ladybug.enemyTraceCandidate.guidedReplay.v1";
        state.WriteTo(first);
        result.Frames.Add(first);

        int fallbackCount = 0;
        int firstDivergencePrediction = -1;

        for (int i = 1; i < mameFrames.Count; i++)
        {
            ArcadeTraceFrame previousMame = mameFrames[i - 1];
            ArcadeTraceFrame currentMame = mameFrames[i];

            bool stateStillMatchesMame =
                previousMame.Enemies.Count > 0
                && state.X == Hex.ToByte(previousMame.Enemies[0].X)
                && state.Y == Hex.ToByte(previousMame.Enemies[0].Y)
                && state.Direction == (Hex.ToByte(previousMame.Enemies[0].Dir) & 0x0f);

            if (stateStillMatchesMame && currentMame.Enemies.Count > 0)
            {
                // Replay guidé: on prend seulement la direction observée au tick suivant,
                // puis on laisse quand même le simulateur faire le pas d'un pixel.
                state.Direction = Hex.ToByte(currentMame.Enemies[0].Dir) & 0x0f;
            }
            else
            {
                fallbackCount++;
                int preferred = GetPreferredForEnemy0(previousMame);
                if (state.IsAtDecisionCenter && preferred != 0)
                    state.Direction = preferred;
            }

            state.StepOnePixel();

            ArcadeTraceFrame candidate = TraceFrameCloner.Clone(currentMame);
            candidate.Schema = "ladybug.enemyTraceCandidate.guidedReplay.v1";
            state.WriteTo(candidate);
            result.Frames.Add(candidate);

            if (firstDivergencePrediction < 0 && !Enemy0MovementEquals(candidate, currentMame))
                firstDivergencePrediction = i;
        }

        result.Messages.Add("Candidate replay guidé générée.");
        result.Messages.Add("Règle utilisée: si l'état simulé colle encore à MAME, prendre la direction du prochain sample MAME, puis avancer d'un pixel.");
        result.Messages.Add("Cette candidate n'est PAS une IA: elle vérifie surtout que le format, l'ordre tick/par-pixel et le comparateur sont cohérents.");
        result.Messages.Add($"Fallbacks utilisés pendant le replay: {fallbackCount}.");
        if (firstDivergencePrediction >= 0)
        {
            ArcadeTraceFrame m = mameFrames[firstDivergencePrediction];
            ArcadeTraceFrame c = result.Frames[firstDivergencePrediction];
            result.Messages.Add(
                $"Première divergence mouvement au sample {m.Sample}: MAME enemy0=raw {m.Enemies[0].Raw} pos({m.Enemies[0].X},{m.Enemies[0].Y}) dir {m.Enemies[0].Dir}, " +
                $"candidate=raw {c.Enemies[0].Raw} pos({c.Enemies[0].X},{c.Enemies[0].Y}) dir {c.Enemies[0].Dir}.");
        }
        else
        {
            result.Messages.Add("Aucune divergence mouvement enemy0 avec le replay guidé.");
        }

        return result;
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

    private sealed class DecisionTable
    {
        private readonly Dictionary<DecisionKey, int> _map = new();

        public int Count => _map.Count;
        public int ConflictCount { get; private set; }

        public bool Contains(DecisionKey key) => _map.ContainsKey(key);

        public bool TryGetNextDirection(DecisionKey key, out int direction) => _map.TryGetValue(key, out direction);

        public static DecisionTable BuildFromTrace(IReadOnlyList<ArcadeTraceFrame> frames)
        {
            var table = new DecisionTable();

            for (int i = 0; i < frames.Count - 1; i++)
            {
                if (frames[i].Enemies.Count == 0 || frames[i + 1].Enemies.Count == 0)
                    continue;

                var state = CandidateEnemyState.FromFrame(frames[i]);
                int preferred = GetPreferredForEnemy0(frames[i]);
                int nextDir = Hex.ToByte(frames[i + 1].Enemies[0].Dir) & 0x0f;

                bool directionChanged = nextDir != state.Direction;
                if (!state.IsAtDecisionCenter && !directionChanged)
                    continue;

                var key = DecisionKey.FromState(state, preferred);
                if (table._map.TryGetValue(key, out int existing))
                {
                    if (existing != nextDir)
                        table.ConflictCount++;
                }
                else
                {
                    table._map[key] = nextDir;
                }
            }

            return table;
        }
    }
}
