using System;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

public sealed class CandidateEnemyState
{
    public int Raw { get; set; }
    public int Direction { get; set; }
    public bool Bit0 { get; set; }
    public bool CollisionActive { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Sprite { get; set; } = "00";
    public string Attr { get; set; } = "00";

    public bool IsAtDecisionCenter => (X & 0x0f) == 0x08 && (Y & 0x0f) == 0x06;

    public static CandidateEnemyState FromFrame(ArcadeTraceFrame frame)
    {
        if (frame.Enemies.Count == 0)
            return new CandidateEnemyState();

        EnemySlotFrame enemy = frame.Enemies[0];
        int raw = Hex.ToByte(enemy.Raw);
        int dir = Hex.ToByte(enemy.Dir) & 0x0f;
        if (dir == 0)
            dir = (raw >> 4) & 0x0f;

        return new CandidateEnemyState
        {
            Raw = raw,
            Direction = dir,
            Bit0 = enemy.Bit0,
            CollisionActive = enemy.CollisionActive,
            X = Hex.ToByte(enemy.X),
            Y = Hex.ToByte(enemy.Y),
            Sprite = enemy.Sprite,
            Attr = enemy.Attr
        };
    }

    public void StepOnePixel()
    {
        switch (Direction)
        {
            case 0x01: X = (X - 1) & 0xff; break;
            case 0x02: Y = (Y - 1) & 0xff; break;
            case 0x04: X = (X + 1) & 0xff; break;
            case 0x08: Y = (Y + 1) & 0xff; break;
        }
    }

    public void WriteTo(ArcadeTraceFrame frame)
    {
        EnsureEnemy0(frame);

        EnemySlotFrame enemy = frame.Enemies[0];
        int lowNibble = Raw & 0x0f;
        Raw = ((Direction & 0x0f) << 4) | lowNibble;

        enemy.Raw = Raw.ToString("X2");
        enemy.Dir = (Direction & 0x0f).ToString("X2");
        enemy.Bit0 = Bit0;
        enemy.CollisionActive = CollisionActive;
        enemy.X = X.ToString("X2");
        enemy.Y = Y.ToString("X2");
        enemy.Sprite = Sprite;
        enemy.Attr = Attr;

        frame.EnemyWork.TempDir = enemy.Dir;
        frame.EnemyWork.TempX = enemy.X;
        frame.EnemyWork.TempY = enemy.Y;
    }

    private static void EnsureEnemy0(ArcadeTraceFrame frame)
    {
        while (frame.Enemies.Count < 1)
        {
            frame.Enemies.Add(new EnemySlotFrame
            {
                Slot = 0,
                Addr = "602B"
            });
        }
    }
}
