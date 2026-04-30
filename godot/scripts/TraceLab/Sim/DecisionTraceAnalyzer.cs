using System.Collections.Generic;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

public static class DecisionTraceAnalyzer
{
    public static List<string> BuildReport(IReadOnlyList<ArcadeTraceFrame> frames, int maxRows = 120)
    {
        var lines = new List<string>();
        if (frames.Count < 2)
        {
            lines.Add("Pas assez de samples pour analyser les décisions.");
            return lines;
        }

        int centerCount = 0;
        int turnCount = 0;
        int emittedRows = 0;

        lines.Add("--- DÉCISIONS ENEMY0 OBSERVÉES DANS MAME ---");
        lines.Add("Format: sample | pos | dir avant -> dir après | preferred[0] | rejected/fallback | centre");

        for (int i = 0; i < frames.Count - 1; i++)
        {
            if (frames[i].Enemies.Count == 0 || frames[i + 1].Enemies.Count == 0)
                continue;

            EnemySlotFrame current = frames[i].Enemies[0];
            EnemySlotFrame next = frames[i + 1].Enemies[0];

            int x = Hex.ToByte(current.X);
            int y = Hex.ToByte(current.Y);
            string beforeDir = current.Dir;
            string afterDir = next.Dir;
            bool atCenter = (x & 0x0f) == 0x08 && (y & 0x0f) == 0x06;
            bool turned = beforeDir != afterDir;

            if (atCenter)
                centerCount++;
            if (turned)
                turnCount++;

            if (!atCenter && !turned)
                continue;

            if (emittedRows < maxRows)
            {
                string preferred = frames[i].EnemyWork.Preferred.Count > 0 ? frames[i].EnemyWork.Preferred[0] : "00";
                string tag = turned ? "TURN" : "KEEP";
                lines.Add(
                    $"{frames[i].Sample:D4} | ({current.X},{current.Y}) | {beforeDir}->{afterDir} | pref={preferred} | " +
                    $"rej={frames[i].EnemyWork.RejectedMask} fb={frames[i].EnemyWork.FallbackMask} | center={atCenter} | {tag}");
                emittedRows++;
            }
        }

        lines.Add($"Centres de décision observés: {centerCount}");
        lines.Add($"Changements de direction observés: {turnCount}");
        if (emittedRows >= maxRows)
            lines.Add($"Rapport tronqué à {maxRows} lignes.");

        return lines;
    }
}
