namespace GE.BandSite.Testing.Core;

/// <summary>
/// Provides cross-platform text helpers for test assertions.
/// </summary>
public static class TestStringNormalization
{
    /// <summary>
    /// Normalizes line endings to Unix-style (<c>\n</c>) regardless of source platform.
    /// </summary>
    /// <param name="value">Input string to normalize.</param>
    /// <returns>Normalized string (or empty string when <paramref name="value"/> is null).</returns>
    public static string NormalizeLineEndings(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        // First collapse Windows CRLF, then stray CR (old Mac style) into LF.
        return value.Replace("\r\n", "\n").Replace('\r', '\n');
    }
}
