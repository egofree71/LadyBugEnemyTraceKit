using System.Collections.Generic;
using Godot;
using LadyBugEnemyTraceLab.TraceLab.Io;
using LadyBugEnemyTraceLab.TraceLab.Model;

namespace LadyBugEnemyTraceLab.TraceLab.Visual;

public partial class EnemyPathView : Control
{
    // Arcade sprite coordinates are logged in the original hardware coordinate system.
    // For a screen/Godot-style view, the vertical axis must be mirrored.
    // This matches the conversion used by the main remake debug overlay:
    // screen/debug Y = 0xDD - arcade Y.
    private const int OriginalDebugYMirror = 0xDD;

    private IReadOnlyList<ArcadeTraceFrame> _frames = new List<ArcadeTraceFrame>();
    private int _enemySlot;

    public void SetTrace(IReadOnlyList<ArcadeTraceFrame> frames, int enemySlot = 0)
    {
        _frames = frames;
        _enemySlot = enemySlot;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.06f, 0.055f, 0.075f));
        DrawGrid();

        if (_frames.Count == 0)
        {
            DrawString(GetThemeDefaultFont(), new Vector2(20, 30), "Aucune trace chargée.", HorizontalAlignment.Left, -1, 16, Colors.White);
            return;
        }

        var points = new List<Vector2>();
        foreach (ArcadeTraceFrame frame in _frames)
        {
            if (frame.Enemies.Count <= _enemySlot)
                continue;

            EnemySlotFrame enemy = frame.Enemies[_enemySlot];
            int x = Hex.ToByte(enemy.X);
            int y = Hex.ToByte(enemy.Y);
            points.Add(MapArcadeToView(x, y));
        }

        for (int i = 1; i < points.Count; i++)
            DrawLine(points[i - 1], points[i], new Color(1f, 0.70f, 0.22f), 2f);

        if (points.Count > 0)
        {
            DrawCircle(points[0], 5, new Color(0.35f, 1f, 0.35f));
            DrawCircle(points[^1], 5, new Color(1f, 0.25f, 0.25f));
        }

        DrawString(
            GetThemeDefaultFont(),
            new Vector2(12, Size.Y - 16),
            $"enemy{_enemySlot} path | samples={_frames.Count} | screen Y=0xDD-arcadeY | green=start red=end",
            HorizontalAlignment.Left,
            -1,
            14,
            Colors.White);
    }

    private void DrawGrid()
    {
        float scale = GetScaleFactor();
        Vector2 origin = GetOrigin(scale);
        for (int x = 0; x <= 0x100; x += 0x10)
        {
            float sx = origin.X + x * scale;
            DrawLine(new Vector2(sx, origin.Y), new Vector2(sx, origin.Y + 0x100 * scale), new Color(0.2f, 0.2f, 0.25f), 1f);
        }

        for (int y = 0; y <= 0x100; y += 0x10)
        {
            float sy = origin.Y + y * scale;
            DrawLine(new Vector2(origin.X, sy), new Vector2(origin.X + 0x100 * scale, sy), new Color(0.2f, 0.2f, 0.25f), 1f);
        }
    }

    private Vector2 MapArcadeToView(int x, int y)
    {
        float scale = GetScaleFactor();
        Vector2 origin = GetOrigin(scale);
        int screenY = OriginalDebugYMirror - y;
        return origin + new Vector2(x * scale, screenY * scale);
    }

    private float GetScaleFactor()
    {
        return Mathf.Min(Size.X - 40f, Size.Y - 45f) / 256f;
    }

    private Vector2 GetOrigin(float scale)
    {
        float mapSize = 256f * scale;
        return new Vector2((Size.X - mapSize) * 0.5f, 15f);
    }
}
