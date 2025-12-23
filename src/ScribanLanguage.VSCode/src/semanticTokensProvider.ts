import * as vscode from "vscode";
import * as vscodelc from "vscode-languageclient/node";
export function registerSemanticTokensProvider(
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
