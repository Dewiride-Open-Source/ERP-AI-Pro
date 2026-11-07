import { readdirSync, readFileSync } from "node:fs";
import { join, posix, relative, resolve, sep } from "node:path";

export type RenderedFile = { readonly template: string; readonly output: string; readonly contents: string };

export const templateSuffix = ".tmpl";

const stems = ["Entities", "entities", "Entity", "entity", "Domain", "domain", "Module", "module", "Title", "Description", "ReadSentence", "CreateSentence", "AreaTitle", "Icon"];
const candidateToken = new RegExp(`__(?:${stems.join("|")})(?:[-_][A-Za-z]+)?__`, "g");

export function renderText(text: string, tokens: ReadonlyMap<string, string>, where: string): string {
  return text.replace(candidateToken, (token) => {
    const value = tokens.get(token);
    if (value === undefined) throw new Error(`${where}: ${token} is no token of the scaffolder`);
    return value;
  });
}

export function renderTree(templateRoot: string, outputRoot: string, tokens: ReadonlyMap<string, string>): RenderedFile[] {
  const root = resolve(outputRoot);
  return filesOf(templateRoot)
    .map((template) => {
      const path = relative(templateRoot, template).split(sep).join(posix.sep);
      if (!path.endsWith(templateSuffix)) throw new Error(`${path}: a template folder holds only ${templateSuffix} files`);
      const output = resolve(root, renderText(path.slice(0, -templateSuffix.length), tokens, path));
      if (output !== root && !output.startsWith(root + sep)) throw new Error(`${path}: renders outside ${outputRoot}`);
      return { template, output, contents: renderText(readFileSync(template, "utf8"), tokens, path) };
    })
    .sort((left, right) => left.output.localeCompare(right.output));
}

function filesOf(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    return entry.isDirectory() ? filesOf(path) : entry.isFile() ? [path] : [];
  });
}
