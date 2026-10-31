import { commentRanges as scriptCommentRanges, type TextRange } from "./code-text.ts";

export type CommentLine = { readonly line: number; readonly text: string };

export type PolicyFinding = CommentLine & { readonly reason: string };

type LiteralEnd = (source: string, index: number) => number | undefined;

type CommentSyntax =
  | { readonly kind: "lines"; readonly extensions: readonly string[]; readonly patterns: readonly RegExp[] }
  | { readonly kind: "ranges"; readonly extensions: readonly string[]; readonly ranges: (source: string) => TextRange[] };

export const bannedPatterns: readonly { readonly pattern: RegExp; readonly reason: string }[] = [
  { pattern: /\b(TODO|FIXME|HACK|XXX)\b/, reason: "use a roadmap note instead of a task marker" },
  {
    pattern: /\b(moved from|previously|no longer|used to be|replaces the old|updated to|legacy|deprecated)\b/i,
    reason: "comments describe the current code, not its history",
  },
  { pattern: /#region\b/, reason: "regions are not used" },
];

function endOfLine(source: string, index: number): number {
  const end = source.indexOf("\n", index);
  return end === -1 ? source.length : end;
}

function cStyleRanges(literalEnd: LiteralEnd, lineComments: boolean): (source: string) => TextRange[] {
  return (source) => {
    const ranges: TextRange[] = [];
    let index = 0;
    while (index < source.length) {
      if (source[index] === "/" && source[index + 1] === "*") {
        const close = source.indexOf("*/", index + 2);
        const end = close === -1 ? source.length : close + 2;
        ranges.push({ start: index, end });
        index = end;
      } else if (lineComments && source[index] === "/" && source[index + 1] === "/") {
        const end = endOfLine(source, index);
        ranges.push({ start: index, end });
        index = end;
      } else {
        index = literalEnd(source, index) ?? index + 1;
      }
    }
    return ranges;
  };
}

function quotedEnd(source: string, start: number, hole?: (index: number) => number | undefined): number {
  const quote = source[start];
  let index = start + 1;
  while (index < source.length) {
    const char = source[index];
    if (char === "\\") index += 2;
    else if (char === quote) return index + 1;
    else if (char === "\n") return index;
    else index = hole?.(index) ?? index + 1;
  }
  return source.length;
}

function holeEnd(source: string, start: number, literalEnd: LiteralEnd): number {
  let depth = 1;
  let index = start;
  while (index < source.length) {
    const literal = literalEnd(source, index);
    if (literal !== undefined) {
      index = literal;
      continue;
    }
    if (source[index] === "{") depth += 1;
    else if (source[index] === "}" && --depth === 0) return index + 1;
    index += 1;
  }
  return source.length;
}

function verbatimEnd(source: string, quote: number, hole: (index: number) => number | undefined): number {
  let index = quote + 1;
  while (index < source.length) {
    if (source[index] !== '"') index = hole(index) ?? index + 1;
    else if (source[index + 1] === '"') index += 2;
    else return index + 1;
  }
  return source.length;
}

function cSharpLiteralEnd(source: string, start: number): number | undefined {
  const prefix = /^[@$]*/.exec(source.slice(start, start + 4))?.[0] ?? "";
  const quote = start + prefix.length;
  if (source[quote] === "'" && prefix === "") return quotedEnd(source, start);
  if (source[quote] !== '"') return undefined;
  const quotes = /^"*/.exec(source.slice(quote, quote + 64))?.[0].length ?? 1;
  if (quotes >= 3) {
    const close = source.indexOf('"'.repeat(quotes), quote + quotes);
    return close === -1 ? source.length : close + quotes;
  }
  if (quotes === 2) return quote + 2;
  const hole = (index: number): number | undefined => {
    if (!prefix.includes("$") || source[index] !== "{") return undefined;
    return source[index + 1] === "{" ? index + 2 : holeEnd(source, index + 1, cSharpLiteralEnd);
  };
  return prefix.includes("@") ? verbatimEnd(source, quote, hole) : quotedEnd(source, quote, hole);
}

function bicepLiteralEnd(source: string, start: number): number | undefined {
  if (source[start] !== "'") return undefined;
  if (source.startsWith("'''", start)) {
    const close = source.indexOf("'''", start + 3);
    return close === -1 ? source.length : close + 3;
  }
  return quotedEnd(source, start, (index) => (source[index] === "$" && source[index + 1] === "{" ? holeEnd(source, index + 2, bicepLiteralEnd) : undefined));
}

function cssLiteralEnd(source: string, start: number): number | undefined {
  return source[start] === '"' || source[start] === "'" ? quotedEnd(source, start) : undefined;
}

const syntaxes: readonly CommentSyntax[] = [
  { kind: "ranges", extensions: [".cs"], ranges: cStyleRanges(cSharpLiteralEnd, true) },
  { kind: "ranges", extensions: [".ts", ".tsx", ".mjs", ".js"], ranges: scriptCommentRanges },
  { kind: "ranges", extensions: [".bicep"], ranges: cStyleRanges(bicepLiteralEnd, true) },
  { kind: "ranges", extensions: [".css"], ranges: cStyleRanges(cssLiteralEnd, false) },
  { kind: "lines", extensions: [".yml", ".yaml", ".sh", ".editorconfig", ".dockerignore", ".gitignore"], patterns: [/^\s*#/] },
  { kind: "lines", extensions: ["Dockerfile", "Caddyfile", ".service", ".timer"], patterns: [/^\s*#/] },
  { kind: "lines", extensions: [".conf"], patterns: [/^\s*#/, /^\s*\/\//] },
];

function syntaxOf(path: string): CommentSyntax | undefined {
  return syntaxes.find((syntax) => syntax.extensions.some((extension) => path.endsWith(extension)));
}

export function isScanned(path: string): boolean {
  return syntaxOf(path) !== undefined;
}

function linesOfRanges(source: string, ranges: readonly TextRange[]): CommentLine[] {
  const starts = [0];
  for (let index = source.indexOf("\n"); index !== -1; index = source.indexOf("\n", index + 1)) starts.push(index + 1);
  const lines: CommentLine[] = [];
  let line = 0;
  for (const { start, end } of ranges) {
    while (line + 1 < starts.length && (starts[line + 1] ?? 0) <= start) line += 1;
    for (let current = line; current < starts.length && (starts[current] ?? 0) < end; current += 1) {
      const lineStart = Math.max(start, starts[current] ?? 0);
      const lineEnd = Math.min(end, endOfLine(source, starts[current] ?? 0));
      lines.push({ line: current + 1, text: source.slice(lineStart, lineEnd).replace(/\r$/, "") });
    }
  }
  return lines;
}

export function commentLines(path: string, source: string): CommentLine[] {
  const syntax = syntaxOf(path);
  if (syntax === undefined) return [];
  if (syntax.kind === "ranges") return linesOfRanges(source, syntax.ranges(source));
  return source.split(/\r?\n/).flatMap((text, index) => (syntax.patterns.some((pattern) => pattern.test(text)) ? [{ line: index + 1, text }] : []));
}

export function policyFindings(path: string, source: string): PolicyFinding[] {
  return commentLines(path, source).flatMap((comment) =>
    bannedPatterns.filter(({ pattern }) => pattern.test(comment.text)).map(({ reason }) => ({ ...comment, reason })),
  );
}
