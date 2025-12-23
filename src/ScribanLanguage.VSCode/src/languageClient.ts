import * as vscode from "vscode";
import * as vscodelc from "vscode-languageclient/node";
import { getLanguageServerBinaryFilePath } from "./serverBinaries";

export async function startLanguageClient() {
  const serverOptions: vscodelc.Executable = {
    command: await getLanguageServerBinaryFilePath(),
    args: ["lsp", "stdio"],
  };
  const clientOptions: vscodelc.LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "scriban" }],
  };
  const client = new vscodelc.LanguageClient(
    "scriban",
    "Scriban",
    serverOptions,
    clientOptions
  );
  client.registerProposedFeatures();
  client.start();
  return client;
}