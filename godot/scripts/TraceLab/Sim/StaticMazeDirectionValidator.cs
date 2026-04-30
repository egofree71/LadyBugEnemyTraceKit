using System;

namespace LadyBugEnemyTraceLab.TraceLab.Sim;

/// <summary>
/// Partial C# port of the arcade static direction validation routine at 0x3911.
///
/// Important: this is not yet the full enemy validator. The arcade then layers
/// local door/tile checks through 0x4130 and fallback/reversal logic around it.
/// This class only reproduces the compact ROM table lookup used by 0x3911.
/// </summary>
public static class StaticMazeDirectionValidator
{
    // ROM table 0x0DA2..0x0DE3, used by routine 0x3911.
    // Each column stores 6 bytes. Each byte contains two 4-bit allowed-direction masks.
    // Enemy direction bits: 01=left, 02=up, 04=right, 08=down.
    private static readonly int[] DirectionMasks =
    {
        0xAC, 0xAE, 0xEA, 0xEE, 0xAE, 0x06,
        0x6D, 0xC5, 0x3E, 0x95, 0xC7, 0x07,
        0x7D, 0xBD, 0xEF, 0xEB, 0xBF, 0x07,
        0xD5, 0xEF, 0xBF, 0xFE, 0xEF, 0x07,
        0xBD, 0x97, 0xAF, 0x3F, 0x3D, 0x05,
        0xAD, 0xAF, 0x87, 0xAF, 0xAF, 0x07,
        0xED, 0xC7, 0xAF, 0x6F, 0x6D, 0x05,
        0xD5, 0xBF, 0xEF, 0xFB, 0xBF, 0x07,
        0x7D, 0xED, 0xBF, 0xBE, 0xEF, 0x07,
        0x3D, 0x95, 0x6B, 0xC5, 0x97, 0x07,
        0xA9, 0xAB, 0xBA, 0xBB, 0xAB, 0x03,
    };

    public static int GetAllowedDirections(int arcadeX, int arcadeY)
    {
        // Z80 routine 0x3911 effectively uses X >> 4 as the column.
        int column = (arcadeX >> 4) & 0x0F;

        // It uses (Y >> 4) - 3 as the row-ish index, then packs two rows per byte.
        int packedRow = ((arcadeY >> 4) - 3) & 0xFF;
        int byteIndex = column * 6 + (packedRow >> 1);

        if (byteIndex < 0 || byteIndex >= DirectionMasks.Length)
            return 0;

        int packed = DirectionMasks[byteIndex];
        return (packedRow & 0x01) != 0
            ? (packed >> 4) & 0x0F
            : packed & 0x0F;
    }

    public static bool Allows(int arcadeX, int arcadeY, int direction)
    {
        return (GetAllowedDirections(arcadeX, arcadeY) & (direction & 0x0F)) != 0;
    }

    public static string FormatMask(int mask)
    {
        return $"{mask & 0x0F:X1}";
    }
}
