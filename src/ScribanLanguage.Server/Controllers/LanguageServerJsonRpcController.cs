using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;
using ScribanLanguage.Lsp;
using ScribanLanguage.Servers;
using ScribanLanguage.Services;
using StreamJsonRpc;

namespace ScribanLanguage.Controllers;

public sealed class LanguageServerJsonRpcController(
    CapabilitiesServer capabilitiesServer,
    SemanticTokenServer semanticTokenServer,
    DocumentSyncServer documentSyncServer,
    ILogger<LanguageServerJsonRpcController> rootLogger,
    IServiceLifetime serviceLifetime)
    : IJsonRpcController
{
    private async Task<TResult> Dispatch<TParam, TResult>(TParam p,
        Func<TParam, CancellationToken, ValueTask<TResult>> func, CancellationToken cancellationToken,
        [CallerMemberName] string memberName = "")
    {
        using var logger = rootLogger.Function(memberName);
        await capabilitiesServer.EnsureAllowed(memberName, cancellationToken).ConfigureAwait(false);
        try
        {
            logger.Trace()?.Log("--> {Arg}", JsonConvert.SerializeObject(p));
            var result = await func(p, cancellationToken).ConfigureAwait(false);
            logger.Trace()?.Log("<-- {Result}", JsonConvert.SerializeObject(result));
            return result;
        }
        catch (Exception ex)
        {
            logger.Error()?.Log(ex, "<-- error");
            throw;
        }
    }

    private async Task Dispatch<TParam>(TParam p,
        Func<TParam, CancellationToken, ValueTask> func, CancellationToken cancellationToken,
        [CallerMemberName] string memberName = "")
    {
        using var logger = rootLogger.Function(memberName);
        await capabilitiesServer.EnsureAllowed(memberName, cancellationToken).ConfigureAwait(false);
        try
        {
            logger.Trace()?.Log("--> {Arg}", JsonConvert.SerializeObject(p));
            await func(p, cancellationToken).ConfigureAwait(false);
            logger.Trace()?.Log("<--");
        }
        catch (Exception ex)
        {
            logger.Error()?.Log(ex, "<-- error");
            throw;
        }
    }

    [PublicAPI, JsonRpcMethod(Methods.InitializeName, UseSingleObjectParameterDeserialization = true)]
    public Task<InitializeResult> Initialize(InitializeParams p, CancellationToken cancellationToken)
    {
        return Dispatch(p, capabilitiesServer.Initialize, cancellationToken);
    }

    [PublicAPI, JsonRpcMethod(Methods.InitializedName, UseSingleObjectParameterDeserialization = true)]
    public Task Initialized(InitializedParams p, CancellationToken cancellationToken)
    {
        return Dispatch(p, capabilitiesServer.Initialized, cancellationToken);
    }

    [PublicAPI, JsonRpcMethod("$/setTrace")]
    public Task SetTrace(SetTraceParams p, CancellationToken cancellationToken)
    {
        return Dispatch(p, capabilitiesServer.SetTrace, cancellationToken);
    }

    [PublicAPI, JsonRpcMethod(Methods.ShutdownName, UseSingleObjectParameterDeserialization = true)]
    public async Task<object?> Shutdown(CancellationToken cancellationToken)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("Shutdown");
        return null;
    }

    [PublicAPI, JsonRpcMethod(Methods.ExitName, UseSingleObjectParameterDeserialization = true)]
    public void Exit()
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("Exit");
        serviceLifetime.Exit();
    }

    [PublicAPI,
     JsonRpcMethod(Methods.WorkspaceDidChangeConfigurationName, UseSingleObjectParameterDeserialization = true)]
    public void WorkspaceDidChangeConfiguration(DidChangeConfigurationParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
    }

    [PublicAPI,
     JsonRpcMethod(Methods.WorkspaceDidChangeWatchedFilesName, UseSingleObjectParameterDeserialization = true)]
    public void WorkspaceDidChangeWatchedFiles(DidChangeWatchedFilesParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
    }

    [PublicAPI, JsonRpcMethod(Methods.WorkspaceSymbolName, UseSingleObjectParameterDeserialization = true)]
    public SymbolInformation[] WorkspaceSymbol(WorkspaceSymbolParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        SymbolInformation[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentDidOpenName, UseSingleObjectParameterDeserialization = true)]
    public Task TextDocumentDidOpen(DidOpenTextDocumentParams p, CancellationToken cancellationToken)
    {
        return Dispatch(p, documentSyncServer.Open, cancellationToken);
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
    public Task TextDocumentDidChange(DidChangeTextDocumentParams p, CancellationToken cancellationToken)
    {
        return Dispatch(p, documentSyncServer.Change, cancellationToken);
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentWillSaveName, UseSingleObjectParameterDeserialization = true)]
    public void TextDocumentWillSave(WillSaveTextDocumentParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
    }

    [PublicAPI,
     JsonRpcMethod(Methods.TextDocumentWillSaveWaitUntilName, UseSingleObjectParameterDeserialization = true)]
    public TextEdit[] TextDocumentWillSaveWaitUntil(WillSaveTextDocumentParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        TextEdit[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentDidSaveName, UseSingleObjectParameterDeserialization = true)]
    public Task TextDocumentDidSave(DidSaveTextDocumentParams p, CancellationToken cancellationToken)
    {
        return Dispatch(p, documentSyncServer.Save, cancellationToken);
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
    public Task TextDocumentDidClose(DidCloseTextDocumentParams p, CancellationToken cancellationToken)
    {
        return Dispatch(p, documentSyncServer.Close, cancellationToken);
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentCompletionName, UseSingleObjectParameterDeserialization = true)]
    public CompletionList TextDocumentCompletion(CompletionParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        CompletionList result = new() { Items = [] };
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI,
     JsonRpcMethod(Methods.TextDocumentCompletionResolveName, UseSingleObjectParameterDeserialization = true)]
    public CompletionItem? TextDocumentCompletionResolve(CompletionItem p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        CompletionItem? result = null;
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentHoverName, UseSingleObjectParameterDeserialization = true)]
    public Hover? TextDocumentHover(TextDocumentPositionParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        return null;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentSignatureHelpName, UseSingleObjectParameterDeserialization = true)]
    public SignatureHelp? TextDocumentSignatureHelp(SignatureHelpParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        return null;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentDefinitionName, UseSingleObjectParameterDeserialization = true)]
    public Location[] TextDocumentDefinition(TextDocumentPositionParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        Location[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentTypeDefinitionName, UseSingleObjectParameterDeserialization = true)]
    public Location[] TextDocumentTypeDefinition(TextDocumentPositionParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        Location[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentImplementationName, UseSingleObjectParameterDeserialization = true)]
    public Location[] TextDocumentImplementation(TextDocumentPositionParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        Location[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
    public Location[] TextDocumentReferences(TextDocumentPositionParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        Location[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI,
     JsonRpcMethod(Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
    public DocumentHighlight[] TextDocumentDocumentHighlight(TextDocumentPositionParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        DocumentHighlight[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentDocumentSymbolName, UseSingleObjectParameterDeserialization = true)]
    public SymbolInformation[] TextDocumentDocumentSymbol(DocumentSymbolParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        SymbolInformation[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentCodeActionName, UseSingleObjectParameterDeserialization = true)]
    public CodeAction[]? TextDocumentCodeAction(CodeActionParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        return null;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentCodeLensName, UseSingleObjectParameterDeserialization = true)]
    public CodeLens[]? TextDocumentCodeLens(CodeLensParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        return null;
    }

    [PublicAPI, JsonRpcMethod(Methods.CodeLensResolveName, UseSingleObjectParameterDeserialization = true)]
    public CodeLens? CodeLensResolve(CodeLens p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        CodeLens? result = null;
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentDocumentLinkName, UseSingleObjectParameterDeserialization = true)]
    public DocumentLink[] TextDocumentDocumentLink(DocumentLinkParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        DocumentLink[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.DocumentLinkResolveName, UseSingleObjectParameterDeserialization = true)]
    public DocumentLink? DocumentLinkResolve(DocumentLink p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        DocumentLink? result = null;
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentFormattingName, UseSingleObjectParameterDeserialization = true)]
    public TextEdit[] TextDocumentFormatting(DocumentFormattingParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        TextEdit[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentRangeFormattingName, UseSingleObjectParameterDeserialization = true)]
    public TextEdit[] TextDocumentRangeFormatting(DocumentRangeFormattingParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        TextEdit[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentOnTypeFormattingName, UseSingleObjectParameterDeserialization = true)]
    public TextEdit[] TextDocumentOnTypeFormatting(DocumentOnTypeFormattingParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        TextEdit[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI, JsonRpcMethod(Methods.TextDocumentFoldingRangeName, UseSingleObjectParameterDeserialization = true)]
    public FoldingRange[] TextDocumentFoldingRange(FoldingRangeParams p)
    {
        using var l = rootLogger.Function();
        l.Trace()?.Log("{Arg}", JsonConvert.SerializeObject(p));
        FoldingRange[] result = [];
        l.Trace()?.Log("{Result}", JsonConvert.SerializeObject(result));
        return result;
    }

    [PublicAPI,
     JsonRpcMethod(Methods.TextDocumentSemanticTokensFullName, UseSingleObjectParameterDeserialization = true)]
    public Task<SemanticTokens> TextDocumentSemanticTokensFull(SemanticTokensParams p,
        CancellationToken cancellationToken)
    {
        return Dispatch(p, semanticTokenServer.SemanticTokens, cancellationToken);
    }

    [PublicAPI,
     JsonRpcMethod(Methods.TextDocumentSemanticTokensRangeName, UseSingleObjectParameterDeserialization = true)]
    public Task<SemanticTokens> TextDocumentSemanticTokensRange(SemanticTokensRangeParams p,
        CancellationToken cancellationToken)
    {
        return Dispatch(p, semanticTokenServer.SemanticTokensRange, cancellationToken);
    }
}