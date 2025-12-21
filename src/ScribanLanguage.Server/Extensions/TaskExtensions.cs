using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Threading;
using IAsyncDisposable = System.IAsyncDisposable;

namespace ScribanLanguage.Extensions;

public static class TaskExtensions
{
    public static ValueTask AsValueTask<T>(this ValueTask<T> valueTask)
    {
        if (!valueTask.IsCompleted) return new(valueTask.AsTask());
        valueTask.GetAwaiter().GetResult();
        return default;
    }

    public static ValueTask<T> AsValueTask<T>(this Task<T> task)
    {
        return new(task);
    }

    public static ValueTask AsValueTask(this Task task)
    {
        return new(task);
    }
}