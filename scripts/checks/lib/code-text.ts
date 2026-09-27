type ScanState = { braceDepth: number; readonly templateBraceDepths: number[] };

const regexAfterPunctuation = new Set("(,=:[!&|?{};+-*%<>~^");
const regexAfterKeyword = /(?:^|[^\w$])(?:return|typeof|instanceof|in|of|new|delete|void|throw|case|do|else|yield|await)$/;

export function withoutComments(source: string): string {
  const state: ScanState = { braceDepth: 0, templateBraceDepths: [] };
  let output = "";
  let index = 0;
  while (index < source.length) {
    const comment = endOfComment(source, index);
    const end = comment ?? endOfToken(source, index, output, state);
    const text = source.slice(index, end);
    output += comment === undefined ? text : text.replace(/[^\r\n]/g, " ");
    index = end;
  }
  return output;
}

function endOfComment(source: string, index: number): number | undefined {
  if (source[index] !== "/") return undefined;
  if (source[index + 1] === "/") {
    const end = source.indexOf("\n", index);
    return end === -1 ? source.length : end;
  }
  if (source[index + 1] === "*") {
    const end = source.indexOf("*/", index + 2);
    return end === -1 ? source.length : end + 2;
  }
  return undefined;
}

function endOfToken(source: string, index: number, preceding: string, state: ScanState): number {
  const char = source[index];
  if (char === '"' || char === "'") return endOfQuoted(source, index);
  if (char === "`") return endOfTemplateChunk(source, index + 1, state);
  if (char === "}" && state.templateBraceDepths.at(-1) === state.braceDepth) {
    state.templateBraceDepths.pop();
    return endOfTemplateChunk(source, index + 1, state);
  }
  if (char === "/" && startsRegex(preceding)) return endOfRegex(source, index);
  if (char === "{") state.braceDepth += 1;
  else if (char === "}") state.braceDepth -= 1;
  return index + 1;
}

function endOfQuoted(source: string, start: number): number {
  const quote = source[start];
  let index = start + 1;
  while (index < source.length) {
    const char = source[index];
    if (char === "\\") index += 2;
    else if (char === quote) return index + 1;
    else if (char === "\n") return index;
    else index += 1;
  }
  return source.length;
}

function endOfTemplateChunk(source: string, start: number, state: ScanState): number {
  let index = start;
  while (index < source.length) {
    const char = source[index];
    if (char === "\\") index += 2;
    else if (char === "`") return index + 1;
    else if (char === "$" && source[index + 1] === "{") {
      state.templateBraceDepths.push(state.braceDepth);
      return index + 2;
    } else index += 1;
  }
  return source.length;
}

function startsRegex(preceding: string): boolean {
  let end = preceding.length;
  while (end > 0 && /\s/.test(preceding[end - 1] ?? "")) end -= 1;
  const last = preceding[end - 1];
  if (last === undefined || regexAfterPunctuation.has(last)) return true;
  return regexAfterKeyword.test(preceding.slice(Math.max(0, end - 12), end));
}

function endOfRegex(source: string, start: number): number {
  let index = start + 1;
  let inClass = false;
  while (index < source.length) {
    const char = source[index];
    if (char === "\\") index += 2;
    else if (char === "\n") return index;
    else if (char === "/" && !inClass) return endOfFlags(source, index + 1);
    else {
      if (char === "[") inClass = true;
      else if (char === "]") inClass = false;
      index += 1;
    }
  }
  return source.length;
}

function endOfFlags(source: string, start: number): number {
  let index = start;
  while (index < source.length && /[a-z]/i.test(source[index] ?? "")) index += 1;
  return index;
}
