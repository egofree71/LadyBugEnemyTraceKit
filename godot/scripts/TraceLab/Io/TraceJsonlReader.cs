using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Io;

public static class TraceJsonlReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static TraceLoadResult Load(string godotPath)
    {
        var warnings = new List<string>();
        var frames = new List<ArcadeTraceFrame>();

        using FileAccess? file = FileAccess.Open(godotPath, FileAccess.ModeFlags.Read);
        if (file is null)
            throw new InvalidOperationException($"Impossible d'ouvrir le fichier: {godotPath}");

        var lineNumber = 0;
        while (!file.EofReached())
        {
            string line = file.GetLine();
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                ArcadeTraceFrame? frame = JsonSerializer.Deserialize<ArcadeTraceFrame>(line, Options);
                if (frame is null)
                {
                    warnings.Add($"Ligne {lineNumber}: JSON vide ou non reconnu.");
                    continue;
                }

                NormalizeFrame(frame);
                frames.Add(frame);
            }
            catch (Exception ex)
            {
                warnings.Add($"Ligne {lineNumber}: {ex.Message}");
            }
        }

        return new TraceLoadResult
        {
            Path = godotPath,
            Frames = frames,
            Warnings = warnings
        };
    }

    private static void NormalizeFrame(ArcadeTraceFrame frame)
    {
        frame.Pc = Hex.Normalize(frame.Pc, 4);
        frame.R = Hex.Normalize(frame.R, 2);
        frame.Player.Raw = Hex.Normalize(frame.Player.Raw, 2);
        frame.Player.X = Hex.Normalize(frame.Player.X, 2);
        frame.Player.Y = Hex.Normalize(frame.Player.Y, 2);
        frame.Player.Sprite = Hex.Normalize(frame.Player.Sprite, 2);
        frame.Player.Attr = Hex.Normalize(frame.Player.Attr, 2);
        frame.Player.TurnTargetX = Hex.Normalize(frame.Player.TurnTargetX, 2);
        frame.Player.TurnTargetY = Hex.Normalize(frame.Player.TurnTargetY, 2);
        frame.Player.CurrentDir = Hex.Normalize(frame.Player.CurrentDir, 2);

        foreach (EnemySlotFrame enemy in frame.Enemies)
        {
            enemy.Addr = Hex.Normalize(enemy.Addr, 4);
            enemy.Raw = Hex.Normalize(enemy.Raw, 2);
            enemy.Dir = Hex.Normalize(enemy.Dir, 2);
            enemy.X = Hex.Normalize(enemy.X, 2);
            enemy.Y = Hex.Normalize(enemy.Y, 2);
            enemy.Sprite = Hex.Normalize(enemy.Sprite, 2);
            enemy.Attr = Hex.Normalize(enemy.Attr, 2);
        }

        frame.EnemyWork.TempDir = Hex.Normalize(frame.EnemyWork.TempDir, 2);
        frame.EnemyWork.TempX = Hex.Normalize(frame.EnemyWork.TempX, 2);
        frame.EnemyWork.TempY = Hex.Normalize(frame.EnemyWork.TempY, 2);
        frame.EnemyWork.RejectedMask = Hex.Normalize(frame.EnemyWork.RejectedMask, 2);
        frame.EnemyWork.FallbackMask = Hex.Normalize(frame.EnemyWork.FallbackMask, 2);
        NormalizeList(frame.EnemyWork.Preferred, 2);
        NormalizeList(frame.EnemyWork.ChaseTimers, 2);
        frame.EnemyWork.ChaseRoundRobin = Hex.Normalize(frame.EnemyWork.ChaseRoundRobin, 2);
    }

    private static void NormalizeList(List<string> values, int digits)
    {
        for (int i = 0; i < values.Count; i++)
            values[i] = Hex.Normalize(values[i], digits);
    }
}
