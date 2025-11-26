using System;
using System.Collections.Generic;

namespace SmartFleet.Dtos;

internal static class ProfileImageSanitizer
{
    private static readonly HashSet<string> InvalidTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "string",
        "null",
        "undefined",
        "none",
        "-"
    };

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (InvalidTokens.Contains(trimmed))
        {
            return null;
        }

        return trimmed;
    }
}
