import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export const licencesFolder = resolve(dirname(fileURLToPath(import.meta.url)), "..", "licences");

export type LicenceText = { readonly key: string; readonly title: string; readonly text: string };

const placeholder = /<year>|<owner>|<copyright holders?>|\[yyyy\]|\{yyyy\}|\[year\]|\[name of copyright owner\]|\{name of copyright owner\}|\[fullname\]|<name of author>/i;

const prose = /^copyright\s+(?:notices?|statements?|holders?|owners?|licen[cs]es?|laws?|and|or|to|of|in|on|is|are|by|for|that|protection|the\s+(?:above|following))\b/i;

const copyrightStart = /^(?:copyright\b|\(c\)\s*(?:\d{4}|copyright\b)|©)/i;

const unquoted = (line: string): string => line.trim().replace(/^(?:>\s*)+/, "");

export function isCopyrightLine(line: string): boolean {
  const trimmed = unquoted(line);
  return copyrightStart.test(trimmed) && !prose.test(trimmed) && !placeholder.test(trimmed);
}

export function isPlaceholderCopyright(line: string): boolean {
  const trimmed = unquoted(line);
  return copyrightStart.test(trimmed) && placeholder.test(trimmed);
}

export function normalisedLines(text: string): string[] {
  return text
    .replace(/^﻿/, "")
    .replace(/\r\n?/g, "\n")
    .split("\n")
    .map((line) => line.replace(/\s+$/, ""));
}

export function splitCopyright(text: string): { copyright: string[]; body: string } {
  const copyright: string[] = [];
  const body: string[] = [];
  for (const line of normalisedLines(text)) {
    if (isCopyrightLine(line)) copyright.push(unquoted(line).replace(/\s+/g, " "));
    else if (!isPlaceholderCopyright(line)) body.push(line);
  }
  return { copyright, body: body.join("\n").replace(/\n{3,}/g, "\n\n").trim() };
}

const titleLine = /^\(?(?:the\s+)?[\w .,-]{0,40}\blicen[cs]e\b[\w .,()-]{0,30}\)?$/i;
const apacheAppendix = { start: "appendix how to apply the apache license to your work", end: "limitations under the license" } as const;

function withoutApacheAppendix(words: string): string {
  const start = words.indexOf(apacheAppendix.start);
  const end = start === -1 ? -1 : words.indexOf(apacheAppendix.end, start);
  return end === -1 ? words : `${words.slice(0, start)}${words.slice(end + apacheAppendix.end.length)}`.replace(/\s+/g, " ").trim();
}

export function textKey(body: string): string {
  const lines = body.split("\n");
  const first = lines.findIndex((line) => line.trim() !== "");
  const title = lines[first]?.trim() ?? "";
  const kept = first !== -1 && title.length <= 60 && titleLine.test(title) ? lines.filter((_, index) => index !== first) : lines;
  const words = kept
    .join(" ")
    .toLowerCase()
    .replace(/licen[cs]e/g, "license")
    .replace(/[^a-z0-9]+/g, " ")
    .trim();
  return createHash("sha256").update(withoutApacheAppendix(words)).digest("hex");
}

export class TextTable {
  readonly #texts = new Map<string, LicenceText>();

  add(title: string, text: string): string {
    const key = textKey(text);
    if (!this.#texts.has(key)) this.#texts.set(key, { key, title, text });
    return key;
  }

  get(key: string): LicenceText | undefined {
    return this.#texts.get(key);
  }

  get size(): number {
    return this.#texts.size;
  }
}

type WordProfile = { readonly words: number; readonly set: ReadonlySet<string> };

function wordProfile(text: string): WordProfile {
  const words = splitCopyright(text)
    .body.toLowerCase()
    .replace(/https?:\/\/\S+/g, " ")
    .replace(/licen[cs]e/g, "license")
    .replace(/['’]s\b/g, "")
    .split(/[^a-z0-9]+/)
    .filter((word) => word.length > 0);
  return { words: words.length, set: new Set(words) };
}

export function similarity(left: ReadonlySet<string>, right: ReadonlySet<string>): number {
  let overlap = 0;
  for (const word of left) if (right.has(word)) overlap += 1;
  return (2 * overlap) / (left.size + right.size);
}

export const identifiableLicences = ["0BSD", "Apache-2.0", "BlueOak-1.0.0", "BSD-2-Clause", "BSD-3-Clause", "BSD-4-Clause", "CC0-1.0", "ISC", "MIT", "MIT-0", "MPL-1.1", "MPL-2.0", "Unlicense", "X11", "Zlib"] as const;

export const identification = { threshold: 0.93, margin: 0.03, shortest: 0.85, longest: 1.1 } as const;

let references: Map<string, WordProfile> | undefined;

export function identifyLicence(text: string): string | undefined {
  references ??= new Map(identifiableLicences.map((id) => [id, wordProfile(canonicalText(id))]));
  const candidate = wordProfile(text);
  const scored = [...references]
    .map(([id, reference]) => ({ id, score: similarity(candidate.set, reference.set), ratio: candidate.words / reference.words }))
    .sort((a, b) => b.score - a.score);
  const [best, runnerUp] = scored;
  if (best === undefined || best.score < identification.threshold || best.ratio < identification.shortest || best.ratio > identification.longest) return undefined;
  return runnerUp === undefined || best.score - runnerUp.score >= identification.margin ? best.id : undefined;
}

export function canonicalTextFile(id: string): string {
  return join(licencesFolder, `${id}.txt`);
}

export function canonicalText(id: string): string {
  const file = canonicalTextFile(id);
  if (!existsSync(file)) {
    throw new Error(`no canonical text for ${id}: add scripts/notices/licences/${id}.txt from https://spdx.org/licenses/${id}.json (its licenseText) and list it in scripts/notices/licences/sources.json`);
  }
  return readFileSync(file, "utf8");
}

export function canonicalBody(id: string): string {
  return splitCopyright(canonicalText(id)).body;
}

type SpdxIdentifiers = { readonly licenseListVersion: string; readonly licenses: readonly string[]; readonly exceptions: readonly string[] };

let identifiers: { licences: Map<string, string>; exceptions: Map<string, string> } | undefined;

function spdxIdentifiers(): { licences: Map<string, string>; exceptions: Map<string, string> } {
  if (identifiers === undefined) {
    const list = JSON.parse(readFileSync(join(licencesFolder, "spdx-identifiers.json"), "utf8")) as SpdxIdentifiers;
    identifiers = {
      licences: new Map(list.licenses.map((id) => [id.toLowerCase(), id])),
      exceptions: new Map(list.exceptions.map((id) => [id.toLowerCase(), id])),
    };
  }
  return identifiers;
}

export function spdxExpression(expression: string): string | undefined {
  const tokens = expression.trim().match(/[()]|[^\s()]+/g) ?? [];
  const { licences, exceptions } = spdxIdentifiers();
  const output: string[] = [];
  let position = 0;
  const peek = () => tokens[position];
  const licence = (): boolean => {
    const token = peek();
    if (token === "(") {
      position += 1;
      output.push("(");
      if (!disjunction() || peek() !== ")") return false;
      position += 1;
      output.push(")");
      return true;
    }
    if (token === undefined) return false;
    const plus = token.endsWith("+");
    const id = licences.get((plus ? token.slice(0, -1) : token).toLowerCase()) ?? (token.startsWith("LicenseRef-") ? token : undefined);
    if (id === undefined) return false;
    position += 1;
    output.push(plus ? `${id}+` : id);
    if (peek()?.toUpperCase() === "WITH") {
      position += 1;
      const exception = exceptions.get((peek() ?? "").toLowerCase());
      if (exception === undefined) return false;
      position += 1;
      output.push("WITH", exception);
    }
    return true;
  };
  const sequence = (operator: string, operand: () => boolean): boolean => {
    if (!operand()) return false;
    while (peek()?.toUpperCase() === operator) {
      position += 1;
      output.push(operator);
      if (!operand()) return false;
    }
    return true;
  };
  const conjunction = () => sequence("AND", licence);
  const disjunction = (): boolean => sequence("OR", conjunction);
  return tokens.length > 0 && disjunction() && position === tokens.length ? output.join(" ").replace(/\( /g, "(").replace(/ \)/g, ")") : undefined;
}

export function licenceIds(expression: string): string[] {
  return (expression.match(/[()]|[^\s()]+/g) ?? []).filter((token) => !["(", ")", "AND", "OR", "WITH"].includes(token.toUpperCase()));
}

export function textBase(id: string): string {
  return id.replace(/\+$/, "").replace(/-(?:only|or-later)$/, "");
}
