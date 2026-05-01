using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LadyBugEnemyTraceLab.TraceLab.Model;

public sealed class Z80EventFrame
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("frameSample")]
    public int FrameSample { get; set; }

    [JsonPropertyName("mameFrame")]
    public int MameFrame { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("pc")]
    public string Pc { get; set; } = "0000";

    [JsonPropertyName("tmpDir")]
    public string TmpDir { get; set; } = "00";

    [JsonPropertyName("tmpX")]
    public string TmpX { get; set; } = "00";

    [JsonPropertyName("tmpY")]
    public string TmpY { get; set; } = "00";

    [JsonPropertyName("rejectedMask")]
    public string RejectedMask { get; set; } = "00";

    [JsonPropertyName("fallbackMask")]
    public string FallbackMask { get; set; } = "00";

    [JsonPropertyName("preferred")]
    public List<string> Preferred { get; set; } = new();

    [JsonPropertyName("enemy0Raw")]
    public string Enemy0Raw { get; set; } = "00";

    [JsonPropertyName("enemy0X")]
    public string Enemy0X { get; set; } = "00";

    [JsonPropertyName("enemy0Y")]
    public string Enemy0Y { get; set; } = "00";

    [JsonPropertyName("enemy1Raw")]
    public string Enemy1Raw { get; set; } = "00";

    [JsonPropertyName("enemy1X")]
    public string Enemy1X { get; set; } = "00";

    [JsonPropertyName("enemy1Y")]
    public string Enemy1Y { get; set; } = "00";

    [JsonPropertyName("chase0")]
    public string Chase0 { get; set; } = "00";

    [JsonPropertyName("chase1")]
    public string Chase1 { get; set; } = "00";

    [JsonPropertyName("chase2")]
    public string Chase2 { get; set; } = "00";

    [JsonPropertyName("chase3")]
    public string Chase3 { get; set; } = "00";

    [JsonPropertyName("rr")]
    public string RoundRobin { get; set; } = "00";

    [JsonPropertyName("raw")]
    public string Raw { get; set; } = string.Empty;
}

public sealed class Z80EventLoadResult
{
    public string Path { get; init; } = string.Empty;
    public List<Z80EventFrame> Events { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
