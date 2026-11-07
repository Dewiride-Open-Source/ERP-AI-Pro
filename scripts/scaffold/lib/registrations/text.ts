import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { renderText } from "../templates.ts";

export type Span = { readonly start: number; readonly end: number };

export type ListLiteral = { readonly open: number; readonly elements: readonly Span[]; readonly close: number };

type Token = { readonly index: number; readonly char: string; readonly end: number };

const byteOrderMark = "\uFEFF";
const quotes = new Set(['"', "'", "`"]);
const snippetRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "templates", "registrations");

// An edit sees the text with LF line endings and without a byte order mark, and the file keeps both as they were, so a
// registration changes only the lines it adds.
export function preservingShape(text: string, edit: (text: string) => string): string {
  const mark = text.startsWith(byteOrderMark) ? byteOrderMark : "";
  const body = text.slice(mark.length);
  const windows = body.includes("\r\n");
  if (windows && /(?<!\r)\n/.test(body)) throw new Error("its line endings mix CRLF and LF; give it one kind before adding a module");
  const edited = edit(windows ? body.replaceAll("\r\n", "\n") : body);
  return mark + (windows ? edited.replaceAll("\n", "\r\n") : edited);
}

export function snippet(name: string, tokens: ReadonlyMap<string, string>): string {
  return renderText(readFileSync(join(snippetRoot, `${name}.tmpl`), "utf8"), tokens, `registrations/${name}.tmpl`);
}

export function ordinal(left: string, right: string): number {
  if (left === right) return 0;
  return left < right ? -1 : 1;
}

export function sameText(left: string, right: string): boolean {
  return left.toUpperCase() === right.toUpperCase();
}

export function lineStart(text: string, index: number): number {
  return text.lastIndexOf("\n", index - 1) + 1;
}

export function nextLineStart(text: string, index: number): number {
  const end = text.indexOf("\n", index);
  return end === -1 ? text.length : end + 1;
}

export function indentationAt(text: string, index: number): string {
  return /^[ \t]*/.exec(text.slice(lineStart(text, index)))?.[0] ?? "";
}

export function insertAt(text: string, index: number, insertion: string): string {
  return text.slice(0, index) + insertion + text.slice(index);
}

export function stringLiteral(value: string): string {
  return JSON.stringify(value);
}

export function stringValue(literal: string): string {
  return JSON.parse(literal) as string;
}

export function listAfter(text: string, declaration: RegExp, description: string): ListLiteral {
  const match = declaration.exec(text);
  if (match === null) throw new Error(`it holds no ${description}`);
  return listLiteral(text, match.index + match[0].length - 1);
}

// The elements of the list literal that opens at `open`, each from its first character to just after its comma, or its last
// character when it has none; strings and comments are read as a whole, so an element may span lines and hold brackets.
export function listLiteral(text: string, open: number): ListLiteral {
  if (text[open] !== "[") throw new Error("the list it extends does not start with [");
  const elements: Span[] = [];
  let depth = 0;
  let start = -1;
  let end = -1;
  for (const token of tokensFrom(text, open + 1)) {
    if (depth === 0 && "]})".includes(token.char)) {
      if (start !== -1) elements.push({ start, end });
      return { open, elements, close: token.index };
    }

    if (start === -1) start = token.index;
    end = token.end;
    if ("[{(".includes(token.char)) depth += 1;
    else if ("]})".includes(token.char)) depth -= 1;
    else if (token.char === "," && depth === 0) {
      elements.push({ start, end });
      start = -1;
    }
  }

  throw new Error("the list it extends never closes");
}

// Adds one element on a line of its own after `after`, or first when `after` is undefined, with the indentation the list
// uses and the comma the element before it lacks; a list written on one line is laid out one element per line first.
export function insertElement(text: string, list: ListLiteral, after: Span | undefined, element: string): string {
  if (lineStart(text, list.open) === lineStart(text, list.close)) return withElementPerLine(text, list, after, element);
  if (after !== undefined) {
    const rest = text.slice(after.end, nextLineStart(text, after.end)).trim();
    if (rest !== "" && !rest.startsWith("//")) throw new Error("the list it extends does not hold one entry per line");
    const comma = text[after.end - 1] === "," ? "" : ",";
    const withComma = insertAt(text, after.end, comma);
    return insertAt(withComma, nextLineStart(withComma, after.end + comma.length), `${indentationAt(text, after.start)}${element},\n`);
  }

  const first = list.elements[0];
  if (first !== undefined) return insertAt(text, lineStart(text, first.start), `${indentationAt(text, first.start)}${element},\n`);
  return insertAt(text, lineStart(text, list.close), `${indentationAt(text, list.open)}  ${element},\n`);
}

function withElementPerLine(text: string, list: ListLiteral, after: Span | undefined, element: string): string {
  const indentation = indentationAt(text, list.open);
  const elements = list.elements.map(({ start, end }) => text.slice(start, end).replace(/,$/, ""));
  elements.splice(after === undefined ? 0 : list.elements.indexOf(after) + 1, 0, element);
  const lines = elements.map((entry) => `${indentation}  ${entry},\n`).join("");
  return `${text.slice(0, list.open + 1)}\n${lines}${indentation}${text.slice(list.close)}`;
}

// Every character outside comments and whitespace, a string literal standing whole as its opening quote.
function* tokensFrom(text: string, from: number): Generator<Token> {
  let index = from;
  while (index < text.length) {
    const char = text[index] as string;
    const end = literalOrCommentEnd(text, index);
    if (end !== undefined) {
      if (quotes.has(char)) yield { index, char, end };
      index = end;
    } else {
      if (!/\s/.test(char)) yield { index, char, end: index + 1 };
      index += 1;
    }
  }
}

function literalOrCommentEnd(text: string, index: number): number | undefined {
  if (text.startsWith("//", index)) {
    const end = text.indexOf("\n", index);
    return end === -1 ? text.length : end;
  }

  if (text.startsWith("/*", index)) {
    const end = text.indexOf("*/", index + 2);
    return end === -1 ? text.length : end + 2;
  }

  const quote = text[index] as string;
  if (!quotes.has(quote)) return undefined;
  for (let current = index + 1; current < text.length; current += 1) {
    if (text[current] === "\\") current += 1;
    else if (text[current] === quote) return current + 1;
  }

  return text.length;
}
