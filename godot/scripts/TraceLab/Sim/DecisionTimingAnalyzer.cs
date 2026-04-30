using System;
using System.Collections.Generic;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

public static class DecisionTimingAnalyzer
{
    public static IEnumerable<string> BuildReport(IReadOnlyList<ArcadeTraceFrame> frames)
    {
        yield return "--- ANALYSE TIMING DES PRÉFÉRENCES ---";
        yield return "On compare, pour chaque centre, la direction MAME du sample suivant avec preferred[0] du sample courant et du sample suivant.";
        yield return "But: vérifier si les préférences loggées en frame_done sont alignées exactement avec la décision enemy0.";

        int centers = 0;
        int prevMatches = 0;
        int nextMatches = 0;
        int bothMatch = 0;
        int neitherMatch = 0;
        int prevOnly = 0;
        int nextOnly = 0;
        var interesting = new List<string>();

        for (int i = 0; i < frames.Count - 1; i++)
        {
            if (frames[i].Enemies.Count == 0 || frames[i + 1].Enemies.Count == 0)
                continue;

            var state = CandidateEnemyState.FromFrame(frames[i]);
            if (!state.IsAtDecisionCenter)
                continue;

            centers++;
            int actual = Hex.ToByte(frames[i + 1].Enemies[0].Dir) & 0x0f;
            int prevPref = GetPref0(frames[i]);
            int nextPref = GetPref0(frames[i + 1]);
            bool prevOk = prevPref == actual;
            bool nextOk = nextPref == actual;

            if (prevOk) prevMatches++;
            if (nextOk) nextMatches++;
            if (prevOk && nextOk) bothMatch++;
            else if (prevOk) prevOnly++;
            else if (nextOk) nextOnly++;
            else neitherMatch++;

            if ((!prevOk || !nextOk) && interesting.Count < 24)
            {
                string line =
                    $"sample {frames[i].Sample:0000}: pos=({state.X:X2},{state.Y:X2}) dir={state.Direction:X2}->MAME {actual:X2}, " +
                    $"pref_current_sample={prevPref:X2}, pref_next_sample={nextPref:X2}, " +
                    $"allowed={StaticMazeDirectionValidator.FormatMask(StaticMazeDirectionValidator.GetAllowedDirections(state.X, state.Y))}, " +
                    $"PCcur={frames[i].Pc}, PCnext={frames[i + 1].Pc}";
                interesting.Add(line);
            }
        }

        yield return $"Centres analysés: {centers}.";
        yield return $"preferred[0] du sample courant matche MAME: {prevMatches}/{centers}.";
        yield return $"preferred[0] du sample suivant matche MAME: {nextMatches}/{centers}.";
        yield return $"Deux côtés matchent: {bothMatch}; courant seulement: {prevOnly}; suivant seulement: {nextOnly}; aucun: {neitherMatch}.";
        yield return "Cas non triviaux:";
        foreach (string line in interesting)
            yield return "  " + line;
        yield return "Interprétation: la trace frame_done est parfaite pour les positions, mais pas toujours pour savoir quelle valeur de 61C4 a été utilisée au moment exact de la décision.";
    }

    private static int GetPref0(ArcadeTraceFrame frame)
    {
        if (frame.EnemyWork.Preferred.Count == 0)
            return 0;
        return Hex.ToByte(frame.EnemyWork.Preferred[0]) & 0x0f;
    }
}
