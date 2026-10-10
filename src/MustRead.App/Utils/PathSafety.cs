namespace MustRead.App.Utils;

/// <summary>
/// Validates values used as single path segments on the share (user, domain,
/// computer names) so they can never escape their inbox folder.
/// </summary>
public static class PathSafety
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    public static bool IsSafeSegment(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.IndexOfAny(InvalidChars) < 0
           && value != "."
           && value != ".."
           && value.Trim() == value;

    public static string EnsureSafeSegment(string value)
    {
        if (!IsSafeSegment(value))
            throw new ArgumentException($"Nome non valido per un percorso: '{value}'.");
        return value;
    }
}
