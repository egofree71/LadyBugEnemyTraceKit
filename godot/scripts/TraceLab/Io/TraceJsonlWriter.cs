using System.Collections.Generic;
using System.Text.Json;
using Godot;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Io;

public static class TraceJsonlWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false
    };

    public static void Write(string godotPath, IReadOnlyList<ArcadeTraceFrame> frames)
    {
        using FileAccess file = FileAccess.Open(godotPath, FileAccess.ModeFlags.Write)
                                ?? throw new System.InvalidOperationException($"Impossible d'écrire: {godotPath}");

        foreach (ArcadeTraceFrame frame in frames)
        {
            file.StoreLine(JsonSerializer.Serialize(frame, Options));
        }
    }
}
