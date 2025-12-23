import * as vscode from "vscode";
import { getLanguageServerBinaryFilePath } from "./serverBinaries";
import { fileUri } from "./util/uri";

interface ScribanTaskDefinition extends vscode.TaskDefinition {
  languageId: string;
  source: string;
  model?: string;
}

function isScibanTaskDefinition(
  def: vscode.TaskDefinition
): def is ScribanTaskDefinition {
  return Object.hasOwn(def, "source") && Object.hasOwn(def, "languageId");
}

function getTaskScope(uri: vscode.Uri) {
  return vscode.workspace.getWorkspaceFolder(uri) ?? vscode.TaskScope.Workspace;
}

async function resolveModelFile(
  source: string,
  quite: boolean,
  token: vscode.CancellationToken
): Promise<string | undefined> {
  const modelFiles = await vscode.workspace.findFiles(
    "**/*.json",
    undefined,
    undefined,
    token
  );
  const modelFilePaths = modelFiles.map((x) => {
    return {
      label: x.fsPath.split(/\/|\\/).pop(),
      detail: x.fsPath,
    } as vscode.QuickPickItem;
  });

  modelFilePaths.splice(0, 0, {
    label: "None",
  });
  const parallelModelFilePath = source?.replace(/\.[^/.]+$/, ".json");
  const parallelModelIndex = modelFilePaths.findIndex(
    (x) => x.detail === parallelModelFilePath
  );

  if (quite && parallelModelIndex >= 0) {
    return modelFilePaths.at(parallelModelIndex)?.detail;
  }

  if (quite) {
    return undefined;
  }

  if (parallelModelIndex > 0) {
    modelFilePaths.splice(
      0,
      0,
      ...modelFilePaths.splice(parallelModelIndex, 1)
    );
  }

  const picked = await vscode.window.showQuickPick(modelFilePaths, {
    title: "Pick model for Scriban template context",
  });
  return picked?.detail;
}

async function createTask(uri: vscode.Uri, token: vscode.CancellationToken) {
  const task = new vscode.Task(
    {
      type: "scriban",
      languageId: "scriban",
      source: uri.fsPath,
      model: (await resolveModelFile(uri.fsPath, true, token)) ?? "",
    } satisfies ScribanTaskDefinition,
    getTaskScope(uri),
    `run ${uri.fsPath.split(/\/|\\/).pop()}`,
    "sciban"
  );
  task.group = vscode.TaskGroup.Test;
  return await resolveTask(task);
}

async function resolveTask(
  unresolved: vscode.Task,
  source?: string,
  model?: string
) {
  const def = unresolved.definition as ScribanTaskDefinition;
  const binary = await getLanguageServerBinaryFilePath();
  const opMode = def.languageId === "scriban" ? "--lexer-mode ScriptOnly" : "";
  const opModel =
    ((model ?? def.model)?.trim() ?? "").length > 0
      ? `--model ${model ?? def.model}`
      : "";
  const cwd =
    typeof unresolved.scope === "object"
      ? (unresolved.scope as vscode.WorkspaceFolder).uri.fsPath
      : undefined;
  const task = new vscode.Task(
    def,
    unresolved.scope as vscode.TaskScope,
    unresolved.name,
    unresolved.source,
    new vscode.ShellExecution(
      `${binary} run ${source ?? def.source} ${opModel} ${opMode}`,
      {
        cwd,
      }
    )
  );
  task.group = unresolved.group;
  task.isBackground = unresolved.isBackground;
  task.problemMatchers = unresolved.problemMatchers;
  task.runOptions = unresolved.runOptions;
  task.isBackground = unresolved.isBackground;
  return task;
}

export class ScibanTaskProvider implements vscode.TaskProvider {
  async provideTasks(token: vscode.CancellationToken): Promise<vscode.Task[]> {
    const files = await vscode.workspace.findFiles(
      "**/*.scriban",
      undefined,
      undefined,
      token
    );
    var tasks = await Promise.all(files.map((x) => createTask(x, token)));
    const active = vscode.window.activeTextEditor?.document;
    if (active && active.languageId === "scriban") {
      const activeIndex = tasks.findIndex(
        (x) =>
          isScibanTaskDefinition(x.definition) &&
          x.definition.source === active.fileName
      );
      if (activeIndex > 0) {
        tasks.splice(0, 0, ...tasks.splice(activeIndex, 1));
      }
      if (activeIndex < 0) {
        tasks.splice(0, 0, await createTask(active.uri, token));
      }
    }

    return tasks;
  }
  async resolveTask(
    _task: vscode.Task,
    token: vscode.CancellationToken
  ): Promise<vscode.Task | null | undefined> {
    const def = _task.definition;
    if (!isScibanTaskDefinition(def)) {
      return undefined;
    }
    const modelFilePath =
      def.model ?? (await resolveModelFile(def.source, false, token));
    const model = modelFilePath
      ? (await fileUri(modelFilePath, token)).fsPath
      : "";
    const source = (await fileUri(def.source, token)).fsPath;
    const task = await resolveTask(_task, source, model);
    return task;
  }
}

export function registerTaskProvider() {
  return vscode.tasks.registerTaskProvider("scriban", new ScibanTaskProvider());
}
