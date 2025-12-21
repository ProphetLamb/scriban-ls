namespace ScribanLanguage.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<TOut> NotNull<TIn, TOut>(this IEnumerable<TIn> seq, Func<TIn, TOut?> selector)
    {
        foreach (var item in seq)
        {
            if (selector(item) is { } result)
            {
                yield return result;
            }
        }
    }

    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> seq) => seq.NotNull<T?, T>(static x => x);

    public static string JoinBy(this IEnumerable<string> seq, string separator) => string.Join(separator, seq);
    public static string JoinBy(this IEnumerable<string> seq, char separator) => string.Join(separator, seq);
}