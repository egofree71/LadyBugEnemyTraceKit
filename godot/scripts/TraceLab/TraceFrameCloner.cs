using System.Collections.Generic;
using System.Text.Json;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab;

public static class TraceFrameCloner
{
    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        WriteIndented = false
    };

    public static ArcadeTraceFrame Clone(ArcadeTraceFrame source)
    {
        string json = JsonSerializer.Serialize(source, CloneOptions);
        return JsonSerializer.Deserialize<ArcadeTraceFrame>(json, CloneOptions) ?? new ArcadeTraceFrame();
    }

    public static List<ArcadeTraceFrame> CloneList(IReadOnlyList<ArcadeTraceFrame> source)
    {
        var result = new List<ArcadeTraceFrame>(source.Count);
        foreach (ArcadeTraceFrame frame in source)
            result.Add(Clone(frame));
        return result;
    }
}
