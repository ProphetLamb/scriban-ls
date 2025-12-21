namespace ScribanLanguage.Extensions;

public static class StringExtensions
{
    public static bool IsEmpty([NotNullWhen(false)] this string? s) => string.IsNullOrEmpty(s);
    public static bool IsWhiteSpace([NotNullWhen(false)] this string? s) => string.IsNullOrWhiteSpace(s);

    public static string? NotEmpty(this string? s) => s.IsEmpty() ? s : null;
    public static string? NotWhiteSpace(this string? s) => s.IsWhiteSpace() ? s : null;
}