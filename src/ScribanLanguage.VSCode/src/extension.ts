import * as vscode from "vscode";
import * as vscodelc from "vscode-languageclient/node";
import { registerSemanticTokensProvider } from "./semanticTokensProvider";
import { registerTaskProvider } from "./taskProvider";
import { startLanguageClient } from "./languageClient";

export async function activate(context: vscode.ExtensionContext) {
  context.subscriptions.push(registerTaskProvider());
  const client = await startLanguageClient();
  context.subscriptions.push(client);
  context.subscriptions.push(registerSemanticTokensProvider(client));
  context.subscriptions.push(
    vscode.commands.registerCommand("scriban-language.trace", async () => {
      await client.restart();
      await client.setTrace(vscodelc.Trace.Verbose);
      vscode.window.showInformationMessage(
        "Restarted language server and enabled tracing"
      );
    })
  );
  console.log("Scriban is now active!");
}

export async function deactivate() {}
