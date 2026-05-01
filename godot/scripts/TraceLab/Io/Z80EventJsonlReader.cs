using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Io;

public static class Z80EventJsonlReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Z80EventLoadResult Load(string godotPath)
    {
        var warnings = new List<string>();
        var events = new List<Z80EventFrame>();

        using FileAccess? file = FileAccess.Open(godotPath, FileAccess.ModeFlags.Read);
        if (file is null)
            throw new InvalidOperationException($"Impossible d'ouvrir le fichier Z80 events: {godotPath}");

        int lineNumber = 0;
        while (!file.EofReached())
        {
            string line = file.GetLine();
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                Z80EventFrame? evt = JsonSerializer.Deserialize<Z80EventFrame>(line, Options);
                if (evt is null)
                {
                    warnings.Add($"Ligne {lineNumber}: JSON vide ou non reconnu.");
                    continue;
                }

                Normalize(evt);
                events.Add(evt);
            }
            catch (Exception ex)
            {
                warnings.Add($"Ligne {lineNumber}: {ex.Message}");
            }
        }

        return new Z80EventLoadResult
        {
            Path = godotPath,
            Events = events,
            Warnings = warnings
        };
    }

    private static void Normalize(Z80EventFrame evt)
    {
        evt.Pc = Hex.Normalize(evt.Pc, 4);
        evt.TmpDir = Hex.Normalize(evt.TmpDir, 2);
        evt.TmpX = Hex.Normalize(evt.TmpX, 2);
        evt.TmpY = Hex.Normalize(evt.TmpY, 2);
        evt.RejectedMask = Hex.Normalize(evt.RejectedMask, 2);
        evt.FallbackMask = Hex.Normalize(evt.FallbackMask, 2);
        evt.Enemy0Raw = Hex.Normalize(evt.Enemy0Raw, 2);
        evt.Enemy0X = Hex.Normalize(evt.Enemy0X, 2);
        evt.Enemy0Y = Hex.Normalize(evt.Enemy0Y, 2);
        evt.Enemy1Raw = Hex.Normalize(evt.Enemy1Raw, 2);
        evt.Enemy1X = Hex.Normalize(evt.Enemy1X, 2);
        evt.Enemy1Y = Hex.Normalize(evt.Enemy1Y, 2);
        evt.Chase0 = Hex.Normalize(evt.Chase0, 2);
        evt.Chase1 = Hex.Normalize(evt.Chase1, 2);
        evt.Chase2 = Hex.Normalize(evt.Chase2, 2);
        evt.Chase3 = Hex.Normalize(evt.Chase3, 2);
        evt.RoundRobin = Hex.Normalize(evt.RoundRobin, 2);

        for (int i = 0; i < evt.Preferred.Count; i++)
            evt.Preferred[i] = Hex.Normalize(evt.Preferred[i], 2);
    }
}
