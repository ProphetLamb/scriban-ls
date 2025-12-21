import { load } from "js-yaml";
import { globSync, readFileSync, writeFileSync } from "fs";

const paths = globSync("*.tmLanguage.yaml", { cwd: __dirname }).map(x => `${__dirname}/${x}`);
console.log("converting yaml syntaxes to json ", paths);
for (const path of paths) {
  const doc = load(readFileSync(path, "utf-8"));
  writeFileSync(
    path.replace(".tmLanguage.yaml", ".tmLanguage.json"),
    JSON.stringify(doc, null, 2)
  );
}
