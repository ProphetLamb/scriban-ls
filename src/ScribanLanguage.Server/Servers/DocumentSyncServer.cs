using Microsoft.VisualStudio.LanguageServer.Protocol;
using ScribanLanguage.Services;
using ScribanLanguage.Workspaces;

namespace ScribanLanguage.Servers;

public sealed class DocumentSyncServer(
    Workspace workspace,
    WorkspaceNavigator navigator,
    ILogger<DocumentSyncServer> rootLogger)
{
    public async ValueTask Change(DidChangeTextDocumentParams change,
        CancellationToken cancellationToken = default)
    {
        using var l = rootLogger.Function();
        await navigator.UpdateDocuments(static async (d, change, cancellationToken) =>
        {
            if (d.Uri != change.TextDocument.Uri || d.Version > change.TextDocument.Version) return d;
            await d.Content.Update(EditContent, change, cancellationToken).ConfigureAwait(false);
            await d.Template.Read(cancellationToken).ConfigureAwait(false);
            return d with { Version = change.TextDocument.Version };
        }, change, cancellationToken).ConfigureAwait(false);

        static TextContent EditContent(TextContent? content, DidChangeTextDocumentParams change)
        {
            content ??= TextContent.Empty;
            StringBuilder sb = new(content.OriginalString);
            foreach (var e in change.ContentChanges)
            {
                var from = content.GetOffset(e.Range.Start);
                var to = content.GetOffset(e.Range.End);
                var length = to - from;
                sb.Remove(from, length);
                sb.Insert(from, e.Text);
            }

            return TextContent.Parse(sb.ToString());
        }
    }

    private static (IReadOnlyList<Project> Relevant, IReadOnlyList<Project> Projects) RelevantProjects(
        IReadOnlyList<Project>? projects, Uri documentUri, IServiceLifetime lifetime)
    {
        var relevantProjects = (projects ?? []).Where(x => x.RootUri.IsBaseOf(documentUri)).ToList();
        if (relevantProjects.Count != 0)
        {
            return (relevantProjects, projects ?? []);
        }

        // track the document in a project of one
        Project createProject = new(documentUri, lifetime);
        relevantProjects.Add(createProject);
        var modifiedProjects = (projects ?? []).Append(createProject).NotNull().ToList();
        return (relevantProjects, modifiedProjects);
    }


    public async ValueTask Open(DidOpenTextDocumentParams open, CancellationToken cancellationToken = default)
    {
        using var l = rootLogger.Function();
        Document document = new(open.TextDocument.Uri, open.TextDocument.Version,
            open.TextDocument.LanguageId, workspace.Lifetime);
        var content = TextContent.Parse(open.TextDocument.Text);
        await workspace.Projects.Update(static async (projects, t, cancellationToken) =>
        {
            var (relevantProjects, modifiedProjects) = RelevantProjects(projects, t.document.Uri, t.Lifetime);
            await Task.WhenAll(
                relevantProjects
                    .Select(project =>
                        project.Documents.Update(AddOrUpdateDocument, (t.document, t.content), cancellationToken)
                            .AsTask())
            ).ConfigureAwait(false);
            return modifiedProjects;
        }, (document, content, workspace.Lifetime), cancellationToken).ConfigureAwait(false);

        static async ValueTask<IReadOnlyList<Document>> AddOrUpdateDocument(IReadOnlyList<Document>? existing,
            (Document document, TextContent content) t, CancellationToken cancellationToken)
        {
            var (document, content) = t;
            existing ??= [];
            var modify = existing.Where(x => x.Uri == document.Uri).ToList();
            if (modify.Count == 0)
            {
                await document.Content.Update(content, cancellationToken).ConfigureAwait(false);
                return existing.Append(document).ToList();
            }

            await Task.WhenAll(modify.Select(x => x.Content.Update(content, cancellationToken).AsTask()))
                .ConfigureAwait(false);
            return existing.Select(UpdateSameDocument).ToList();

            Document UpdateSameDocument(Document d)
            {
                return d.Uri == document.Uri
                    ? d with
                    {
                        LanguageId = document.LanguageId.NotEmpty() ?? d.LanguageId,
                        Version = Math.Max(document.Version, d.Version),
                    }
                    : d;
            }
        }
    }

    public async ValueTask Save(DidSaveTextDocumentParams save, CancellationToken cancellationToken)
    {
        using var l = rootLogger.Function();
        var content = TextContent.Parse(save.Text);
        await workspace.Projects.Read(static async (projects, t, cancellationToken) =>
        {
            var (relevantProjects, _) = RelevantProjects(projects, t.Uri, t.Lifetime);
            await Task.WhenAll(
                relevantProjects
                    .Select(project => project.Documents.Update(UpdateDocument, (t.Uri, t.content), cancellationToken)
                        .AsTask())
            ).ConfigureAwait(false);
            return projects;
        }, (save.TextDocument.Uri, content, workspace.Lifetime), cancellationToken).ConfigureAwait(false);

        static async ValueTask<IReadOnlyList<Document>> UpdateDocument(IReadOnlyList<Document>? existing,
            (Uri uri, TextContent content) t, CancellationToken cancellationToken)
        {
            var (uri, content) = t;
            existing ??= [];
            var modify = existing.Where(x => x.Uri == uri).ToList();
            await Task.WhenAll(modify.Select(x => x.Content.Update(content, cancellationToken).AsTask()))
                .ConfigureAwait(false);
            return existing;
        }
    }

    public async ValueTask Close(DidCloseTextDocumentParams close, CancellationToken cancellationToken)
    {
        using var l = rootLogger.Function();

        await workspace.Projects.Read(static async (projects, t, cancellationToken) =>
        {
            var (relevantProjects, _) = RelevantProjects(projects, t.Uri, t.Lifetime);
            await Task.WhenAll(
                relevantProjects
                    .Select(project => project.Documents.Update(RemoveDocument, t.Uri, cancellationToken).AsTask())
            ).ConfigureAwait(false);
            return projects;
        }, (close.TextDocument.Uri, workspace.Lifetime), cancellationToken).ConfigureAwait(false);

        static IReadOnlyList<Document> RemoveDocument(IReadOnlyList<Document>? existing, Uri uri)
        {
            return (existing ?? []).Where(x => x.Uri != uri).ToList();
        }
    }
}