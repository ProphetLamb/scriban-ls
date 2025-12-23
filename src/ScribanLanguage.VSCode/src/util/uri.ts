import * as vscode from "vscode";

export async function fileUri(
  filePath: string,
  token?: vscode.CancellationToken
): Promise<vscode.Uri> {
  if (isPathRooted(filePath)) {
    return vscode.Uri.file(filePath);
  }
  const wsFile = (
    await vscode.workspace.findFiles(filePath, undefined, undefined, token)
  ).at(0);
  if (wsFile) {
    return wsFile;
  }
  const wsFolder = vscode.workspace.workspaceFolders?.at(0);
  if (wsFolder) {
    return vscode.Uri.joinPath(wsFolder.uri, filePath);
  }
  return vscode.Uri.file(filePath);
}

function isPathRooted(filePath: string) {
  if (filePath.at(0) === "/") {
    return true;
  }
  if (filePath.slice(1, 3) === ":\\" || filePath.slice(1, 3) === ":/") {
    return true;
  }
  if (filePath.slice(0, 2) === "//" || filePath.slice(0, 2) === "\\") {
    return true;
  }
}