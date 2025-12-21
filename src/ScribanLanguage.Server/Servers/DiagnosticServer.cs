using Microsoft.VisualStudio.LanguageServer.Protocol;
using Scriban.Parsing;
using ScribanLanguage.Workspaces;
using StreamJsonRpc;

namespace ScribanLanguage.Servers;

public sealed class DiagnosticServer(Workspace workspace, JsonRpc rpc, ILogger<DiagnosticServer> rootLogger)
{
    public async ValueTask PublishParserDiagnostics(Scriban.Template template, Document document,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var l = rootLogger.Function();
        l.Debug()?.Log("Publishing {Messages} diagnostics for {Uri}", template.Messages.Count, document.Uri);
        var diagnostics = template.Messages.Select(MessageToDiagnostic).ToArray();
        await rpc.NotifyWithParameterObjectAsync(Methods.TextDocumentPublishDiagnosticsName,
            new PublishDiagnosticParams
            {
                Uri = document.Uri,
                Diagnostics = diagnostics
            }).ConfigureAwait(false);

        Diagnostic MessageToDiagnostic(LogMessage msg)
        {
            var start = msg.Span.Start == TextPosition.Eof ? template.Page.Span.End : msg.Span.Start;
            var end = msg.Span.End == TextPosition.Eof ? template.Page.Span.End : msg.Span.End;
            return new()
            {
                Range = new()
                {
                    Start = start.ToPosition(),
                    End = end.ToPosition(),
                },
                Message = msg.Message,
                Severity = msg.Type.ToSeverity()
            };
        }
    }
}