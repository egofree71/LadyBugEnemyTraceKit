using System;
using System.Globalization;

namespace LadyBugEnemyTraceLab.TraceLab.Io;

public static class Hex
{
    public static string Normalize(string? value, int digits)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new string('0', digits);

        string clean = value.Trim();
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            clean = clean[2..];

        clean = clean.ToUpperInvariant();
        if (!int.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsed))
            return clean.PadLeft(digits, '0')[^digits..];

        int mask = digits <= 2 ? 0xff : 0xffff;
        return (parsed & mask).ToString($"X{digits}", CultureInfo.InvariantCulture);
    }

    public static int ToByte(string? value)
    {
        string normalized = Normalize(value, 2);
        return int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsed)
            ? parsed & 0xff
            : 0;
    }
}
