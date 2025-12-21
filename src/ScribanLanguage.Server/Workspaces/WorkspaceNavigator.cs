namespace ScribanLanguage.Workspaces;

public sealed class WorkspaceNavigator(Workspace workspace, ILogger<WorkspaceNavigator> rootLogger)
{
    public async ValueTask UpdateDocuments<TContext>(
        Func<Document, TContext, CancellationToken, ValueTask<Document?>> updater,
        TContext context, CancellationToken cancellationToken)
    {
        using var l = rootLogger.Function();
        await workspace.Projects.Read(static async (projects, t, cancellationToken) =>
        {
            return await Task.WhenAll((projects ?? []).Select(project =>
                project.Documents.Update(static async (documents, t, cancellationToken) =>
                {
                    var updaterResult =
                        await Task.WhenAll((documents ?? []).Select(x =>
                            t.updater(x, t.context, cancellationToken).AsTask())).ConfigureAwait(false);
                    return updaterResult.NotNull().ToList();
                }, t, cancellationToken).AsTask())).ConfigureAwait(false);
        }, (l, updater, context), cancellationToken).ConfigureAwait(false);
    }


    public async ValueTask<IReadOnlyList<TResult>> ReadDocuments<TResult, TContext>(
        Func<Document, TContext, CancellationToken, ValueTask<TResult>> selector,
        TContext context, CancellationToken cancellationToken)
    {
        using var l = rootLogger.Function();
        var results = await workspace.Projects.Read(static async (projects, t, cancellationToken) =>
        {
            return await Task.WhenAll((projects ?? []).Select(project =>
                project.Documents.Read(static async (documents, t, cancellationToken) =>
                {
                    var updaterResult =
                        await Task.WhenAll((documents ?? []).Select(x =>
                            t.updater(x, t.context, cancellationToken).AsTask())).ConfigureAwait(false);
                    return updaterResult.NotNull().ToList();
                }, t, cancellationToken).AsTask())).ConfigureAwait(false);
        }, (l, updater: selector, context), cancellationToken).ConfigureAwait(false);
        return results.SelectMany(x => x).ToList();
    }
}