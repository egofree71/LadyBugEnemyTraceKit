using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LadyBugEnemyTraceLab.TraceLab.Model;

public sealed class ArcadeTraceFrame
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("sample")]
    public int Sample { get; set; }

    [JsonPropertyName("mameFrame")]
    public int MameFrame { get; set; }

    [JsonPropertyName("pc")]
    public string Pc { get; set; } = string.Empty;

    [JsonPropertyName("r")]
    public string R { get; set; } = string.Empty;

    [JsonPropertyName("player")]
    public PlayerFrame Player { get; set; } = new();

    [JsonPropertyName("enemies")]
    public List<EnemySlotFrame> Enemies { get; set; } = new();

    [JsonPropertyName("enemyWork")]
    public EnemyWorkFrame EnemyWork { get; set; } = new();

    [JsonPropertyName("timers")]
    public TimerFrame Timers { get; set; } = new();

    [JsonPropertyName("ports")]
    public PortFrame Ports { get; set; } = new();
}

public sealed class PlayerFrame
{
    [JsonPropertyName("raw")]
    public string Raw { get; set; } = "00";

    [JsonPropertyName("x")]
    public string X { get; set; } = "00";

    [JsonPropertyName("y")]
    public string Y { get; set; } = "00";

    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = "00";

    [JsonPropertyName("attr")]
    public string Attr { get; set; } = "00";

    [JsonPropertyName("turnTargetX")]
    public string TurnTargetX { get; set; } = "00";

    [JsonPropertyName("turnTargetY")]
    public string TurnTargetY { get; set; } = "00";

    [JsonPropertyName("currentDir")]
    public string CurrentDir { get; set; } = "00";
}

public sealed class EnemySlotFrame
{
    [JsonPropertyName("slot")]
    public int Slot { get; set; }

    [JsonPropertyName("addr")]
    public string Addr { get; set; } = string.Empty;

    [JsonPropertyName("raw")]
    public string Raw { get; set; } = "00";

    [JsonPropertyName("dir")]
    public string Dir { get; set; } = "00";

    [JsonPropertyName("bit0")]
    public bool Bit0 { get; set; }

    [JsonPropertyName("collisionActive")]
    public bool CollisionActive { get; set; }

    [JsonPropertyName("x")]
    public string X { get; set; } = "00";

    [JsonPropertyName("y")]
    public string Y { get; set; } = "00";

    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = "00";

    [JsonPropertyName("attr")]
    public string Attr { get; set; } = "00";
}

public sealed class EnemyWorkFrame
{
    [JsonPropertyName("tempDir")]
    public string TempDir { get; set; } = "00";

    [JsonPropertyName("tempX")]
    public string TempX { get; set; } = "00";

    [JsonPropertyName("tempY")]
    public string TempY { get; set; } = "00";

    [JsonPropertyName("rejectedMask")]
    public string RejectedMask { get; set; } = "00";

    [JsonPropertyName("fallbackMask")]
    public string FallbackMask { get; set; } = "00";

    [JsonPropertyName("preferred")]
    public List<string> Preferred { get; set; } = new();

    [JsonPropertyName("chaseTimers")]
    public List<string> ChaseTimers { get; set; } = new();

    [JsonPropertyName("chaseRoundRobin")]
    public string ChaseRoundRobin { get; set; } = "00";
}

public sealed class TimerFrame
{
    [JsonPropertyName("61B4")]
    public string Timer61B4 { get; set; } = "00";

    [JsonPropertyName("61B5")]
    public string Timer61B5 { get; set; } = "00";

    [JsonPropertyName("61B6")]
    public string Timer61B6 { get; set; } = "00";

    [JsonPropertyName("61B7")]
    public string Timer61B7 { get; set; } = "00";

    [JsonPropertyName("61B8")]
    public string Timer61B8 { get; set; } = "00";

    [JsonPropertyName("61B9")]
    public string Timer61B9 { get; set; } = "00";

    [JsonPropertyName("freeze61E1")]
    public string Freeze61E1 { get; set; } = "00";

    [JsonPropertyName("collectibleColorCounter6199")]
    public string CollectibleColorCounter6199 { get; set; } = "0000";
}

public sealed class PortFrame
{
    [JsonPropertyName("in0_9000")]
    public string In0 { get; set; } = "00";

    [JsonPropertyName("in1_9001")]
    public string In1 { get; set; } = "00";

    [JsonPropertyName("dsw0_9002")]
    public string Dsw0 { get; set; } = "00";

    [JsonPropertyName("dsw1_9003")]
    public string Dsw1 { get; set; } = "00";
}
