import { statSync } from "node:fs";
import { registerHooks } from "node:module";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const aliasPrefix = "@/";
const sourceRoot = join(process.cwd(), "src");
const candidateSuffixes = ["", ".ts", "/index.ts"];

function isFile(path) {
  return statSync(path, { throwIfNoEntry: false })?.isFile() ?? false;
}

// The web app imports its own src/ through the "@/*" path of its tsconfig.json, which Node does not read, so a pure module
// under test that imports shared code through the alias resolves here the way TypeScript and Next.js resolve it.
registerHooks({
  resolve(specifier, context, nextResolve) {
    if (!specifier.startsWith(aliasPrefix)) return nextResolve(specifier, context);
    const base = join(sourceRoot, specifier.slice(aliasPrefix.length));
    const file = candidateSuffixes.map((suffix) => base + suffix).find(isFile);
    return nextResolve(file === undefined ? specifier : pathToFileURL(file).href, context);
  },
});
