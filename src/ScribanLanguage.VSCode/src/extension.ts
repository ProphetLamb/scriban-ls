import * as vscode from "vscode";
import * as vscodelc from "vscode-languageclient/node";

let client: vscodelc.LanguageClient | undefined;

function redundantSemanticTokenFuckeryKISSmyass(
  client: vscodelc.LanguageClient
) {
  // fucker doesnt read the shitty tokens from server capabilities????
  // why the fuck did i even implement them????
  // and why does it not work if it dont either! client.initializeResult?.capabilities.semanticTokensProvider?.legend is undef anyway
  // lets hope that shit doesnt ever desync and go to shit
  const tokenTypes = [
    vscodelc.SemanticTokenTypes.variable,
    vscodelc.SemanticTokenTypes.comment,
    vscodelc.SemanticTokenTypes.string,
    vscodelc.SemanticTokenTypes.keyword,
    vscodelc.SemanticTokenTypes.operator,
    vscodelc.SemanticTokenTypes.function,
    vscodelc.SemanticTokenTypes.parameter,
    vscodelc.SemanticTokenTypes.property,
  ];
  const tokenModifiers = [
    vscodelc.SemanticTokenModifiers.static,
    vscodelc.SemanticTokenModifiers.declaration,
    vscodelc.SemanticTokenModifiers.documentation,
  ];
  const legend =
    client.initializeResult?.capabilities.semanticTokensProvider?.legend ??
    new vscode.SemanticTokensLegend(tokenTypes, tokenModifiers);

  const provider: vscode.DocumentSemanticTokensProvider &
    vscode.DocumentRangeSemanticTokensProvider = {
    async provideDocumentSemanticTokens(
      document: vscode.TextDocument,
      ct: vscode.CancellationToken
    ): Promise<vscode.SemanticTokens> {
      const result: vscode.SemanticTokens = await client.sendRequest(
        vscodelc.SemanticTokensRequest.method,
        {
          textDocument: {
            uri: document.uri.toString(),
          },
        },
        ct
      );
      return result;
    },
    async provideDocumentRangeSemanticTokens(
      document: vscode.TextDocument,
      range: vscode.Range,
      ct: vscode.CancellationToken
    ): Promise<vscode.SemanticTokens> {
      const result: vscode.SemanticTokens = await client.sendRequest(
        vscodelc.SemanticTokensRangeRequest.method,
        {
          textDocument: {
            uri: document.uri.toString(),
          },
          range: range,
        },
        ct
      );
      return result;
    },
  };

  const selector = { language: "scriban", scheme: "file" };

  return vscode.languages.registerDocumentSemanticTokensProvider(
    selector,
    provider,
    legend
  );
}

export function activate(context: vscode.ExtensionContext) {
  const serverOptions: vscodelc.Executable = {
    command: `${__dirname}/../../ScribanLanguage.Server/bin/Debug/net10.0/ScribanLanguage.Server.exe`,
    args: ["stdio"],
  };
  const clientOptions: vscodelc.LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "scriban" }],
  };
  client = new vscodelc.LanguageClient(
    "scriban-language",
    "Scriban",
    serverOptions,
    clientOptions
  );
  client.registerProposedFeatures();
  client.start();
  console.log(
    'Congratulations, your extension "scriban-language" is now active!'
  );

  context.subscriptions.push(redundantSemanticTokenFuckeryKISSmyass(client));
  context.subscriptions.push(
    vscode.commands.registerCommand("scriban-language.trace", () => {
      vscode.window.showInformationMessage(
        "Hello World from Scriban Language!"
      );
      client?.setTrace(vscodelc.Trace.Verbose);
      client?.restart();
      client?.setTrace(vscodelc.Trace.Verbose);
    })
  );
}

export function deactivate() {
  if (client) {
    const result = client.stop();
    client = undefined;
    return result;
  }
  return null;
}
