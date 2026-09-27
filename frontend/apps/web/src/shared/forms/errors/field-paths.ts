import type { ValidationErrors } from "../state/form-state.ts";

export type FieldAliases = Readonly<Record<string, string>>;

export type PathIssue = { readonly path: readonly PropertyKey[]; readonly message: string };

const memberName = /^[^.[\]]+/;
const elementIndex = /^\[(0|[1-9]\d*)\]/;
const upperCaseLetter = /^\p{Lu}$/u;

// react-hook-form keeps its own state under these names inside an error object, so a field path containing one cannot
// hold an error (react-hook-form, setError rules).
const reservedFieldNames = new Set(["root", "type", "types", "ref", "message", "form"]);

export function issuePath(path: readonly PropertyKey[]): string {
  return path
    .filter((segment): segment is string | number => typeof segment !== "symbol")
    .map(String)
    .join(".");
}

export function issuesToFieldErrors(issues: readonly PathIssue[]): ValidationErrors {
  return collectValidationErrors(issues.map((issue) => [issuePath(issue.path), issue.message] as const));
}

export function collectValidationErrors(
  messages: Iterable<readonly [path: string | undefined, message: string]>,
): ValidationErrors {
  const fieldErrors = new Map<string, string[]>();
  const formErrors: string[] = [];
  for (const [path, message] of messages) {
    const target = path === undefined || path === "" ? formErrors : messagesOf(fieldErrors, path);
    if (!target.includes(message)) target.push(message);
  }
  return { fieldErrors: Object.fromEntries(fieldErrors), formErrors };
}

export function apiKeyToFormPath(key: string, aliases: FieldAliases = {}): string | undefined {
  const path = memberPath(key);
  if (path === undefined) return undefined;

  const aliased = applyAliases(path, aliases);
  const reserved = aliased.split(".").some((segment) => reservedFieldNames.has(segment));
  return aliased === "" || reserved ? undefined : aliased;
}

// The exact rule of System.Text.Json's JsonNamingPolicy.CamelCase (JsonCamelCaseNamingPolicy.FixCasing), applied per
// UTF-16 code unit with its invariant lower-casing, so a key the API did not convert maps to the member the API writes.
export function camelCaseMemberName(name: string): string {
  const units = name.split("");
  if (!isUpperCase(units[0])) return name;

  for (let index = 0; index < units.length; index++) {
    if (index === 1 && !isUpperCase(units[index])) break;
    const next = units[index + 1];
    if (index > 0 && next !== undefined && !isUpperCase(next)) {
      if (next === " ") units[index] = lowerCase(units[index]);
      break;
    }
    units[index] = lowerCase(units[index]);
  }
  return units.join("");
}

function memberPath(key: string): string | undefined {
  const segments: string[] = [];
  let rest = key;
  while (rest.length > 0) {
    const name = memberName.exec(rest)?.[0];
    if (name === undefined) return undefined;
    segments.push(camelCaseMemberName(name));
    rest = rest.slice(name.length);

    for (let index = elementIndex.exec(rest); index !== null; index = elementIndex.exec(rest)) {
      segments.push(index[1] ?? "");
      rest = rest.slice(index[0].length);
    }

    if (rest.length === 0) break;
    if (!rest.startsWith(".") || rest.length === 1) return undefined;
    rest = rest.slice(1);
  }
  return segments.length > 0 ? segments.join(".") : undefined;
}

function applyAliases(path: string, aliases: FieldAliases): string {
  let match: { readonly from: string; readonly to: string } | undefined;
  for (const [apiKey, formPath] of Object.entries(aliases)) {
    const from = memberPath(apiKey);
    if (from === undefined || (path !== from && !path.startsWith(`${from}.`))) continue;
    if (match === undefined || from.length > match.from.length) match = { from, to: formPath };
  }
  if (match === undefined) return path;
  return match.to === "" ? "" : match.to + path.slice(match.from.length);
}

function messagesOf(fieldErrors: Map<string, string[]>, path: string): string[] {
  const existing = fieldErrors.get(path);
  if (existing !== undefined) return existing;
  const created: string[] = [];
  fieldErrors.set(path, created);
  return created;
}

function isUpperCase(unit: string | undefined): boolean {
  return unit !== undefined && upperCaseLetter.test(unit);
}

function lowerCase(unit: string | undefined): string {
  if (unit === undefined) return "";
  const lower = unit.toLowerCase();
  return lower.length === 1 ? lower : unit;
}
