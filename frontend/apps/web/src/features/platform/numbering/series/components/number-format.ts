import type { NumberYearStyle } from "@dewiride/erp-api-client";

export interface NumberFormat {
  readonly prefix: string;
  readonly year: NumberYearStyle;
  readonly separator: string;
  readonly digits: number;
}

export type TypedFormat = Omit<NumberFormat, "year"> & { readonly year: string };

export interface SerialNumberRule {
  readonly maxLength: number;
  readonly specialCharacters: string;
}

export type FormatField = "format" | "format.prefix" | "format.year" | "format.separator" | "format.digits";

export const yearStyles: readonly NumberYearStyle[] = ["none", "full", "abbreviated"];

export const prefixMaxLength = 32;

export const separatorMaxLength = 1;

export const maxDigits = 18;

const yearHyphen = "-";

const fullYearLength = "2026-27".length;

const abbreviatedYearLength = "26-27".length;

const upperCaseLetter = /^[A-Z]$/;

const asciiDigit = /^\d$/;

const numberStart = /^[A-Z1-9]/;

const characterNames = new Map([
  ["-", "hyphen"],
  ["/", "slash"],
  [".", "full stop"],
  ["_", "underscore"],
]);

export function isYearStyle(value: string): value is NumberYearStyle {
  return (yearStyles as readonly string[]).includes(value);
}

export function yearLabel(financialYear: string, style: NumberYearStyle): string {
  switch (style) {
    case "full":
      return financialYear;
    case "abbreviated":
      return financialYear.slice(2);
    case "none":
      return "";
  }
}

export function fixedLength(format: NumberFormat): number {
  switch (format.year) {
    case "full":
      return format.prefix.length + fullYearLength + format.separator.length;
    case "abbreviated":
      return format.prefix.length + abbreviatedYearLength + format.separator.length;
    case "none":
      return format.prefix.length;
  }
}

export function renderNumber(format: NumberFormat, financialYear: string, running: bigint): string {
  if (running < 1n) throw new RangeError(`A running number starts at 1; received ${running}.`);
  const digits = running.toString().padStart(format.digits, "0");
  return format.year === "none"
    ? format.prefix + digits
    : format.prefix + yearLabel(financialYear, format.year) + format.separator + digits;
}

// NumberFormat.LastNumberWithin: the last running number, of at most maxDigits digits, whose number has at most maxLength
// characters; 0 when not even the first one fits. The API answers it as a JSON number, which keeps every digit only up to
// Number.MAX_SAFE_INTEGER, so the page works it out itself.
export function lastNumberWithin(format: NumberFormat, maxLength: number): bigint {
  const room = maxLength - fixedLength(format);
  if (room < format.digits || room < 1) return 0n;
  return 10n ** BigInt(Math.min(room, maxDigits)) - 1n;
}

export function runningNumberOf(text: string, format: NumberFormat): bigint {
  const running = text.slice(fixedLength(format));
  if (!/^\d+$/.test(running)) throw new RangeError(`${text} does not end in a running number.`);
  return BigInt(running);
}

export function sameFormat(first: NumberFormat, second: NumberFormat): boolean {
  return (
    first.prefix === second.prefix &&
    first.year === second.year &&
    first.separator === second.separator &&
    first.digits === second.digits
  );
}

// NumberFormat.ProblemsUnder, keyed and worded as the API answers them, so a rule the browser finds reads as the API would put
// it; the whole number is checked only once every part is allowed.
export function formatProblems(
  format: TypedFormat,
  rule: SerialNumberRule,
  financialYear: string,
): ReadonlyMap<FormatField, string> {
  const named = specialCharacterNames(rule);
  const problems = new Map<FormatField, string>();
  const parts: readonly (readonly [FormatField, string | undefined])[] = [
    ["format.prefix", prefixProblem(format.prefix, rule, named)],
    ["format.year", yearProblem(format.year, rule)],
    ["format.separator", separatorProblem(format, rule, named)],
    ["format.digits", digitsProblem(format.digits)],
  ];
  for (const [field, problem] of parts) {
    if (problem !== undefined) problems.set(field, problem);
  }
  if (problems.size > 0 || !isYearStyle(format.year)) return problems;

  const whole = wholeNumberProblem({ ...format, year: format.year }, rule, financialYear);
  if (whole !== undefined) problems.set(whole.field, whole.message);
  return problems;
}

export function characterName(character: string): string {
  const name = characterNames.get(character);
  return name === undefined ? `"${character}"` : `${character} (${name})`;
}

export function specialCharacterNames(rule: SerialNumberRule): readonly string[] {
  return rule.specialCharacters.split("").map(characterName);
}

export function listOf(items: readonly string[], conjunction: "and" | "or"): string {
  return items.length < 2
    ? (items[0] ?? "")
    : `${items.slice(0, -1).join(", ")} ${conjunction} ${items.at(-1) ?? ""}`;
}

function prefixProblem(prefix: string, rule: SerialNumberRule, named: readonly string[]): string | undefined {
  if (prefix.length > prefixMaxLength) return `Use at most ${prefixMaxLength} characters.`;
  return prefix.split("").every((character) => isAllowed(character, rule))
    ? undefined
    : `Use only ${listOf(["capital letters", "digits", ...named], "and")}.`;
}

function yearProblem(year: string, rule: SerialNumberRule): string | undefined {
  if (!isYearStyle(year)) return "Choose how the financial year is shown, or that it is not shown.";
  return year === "none" || rule.specialCharacters.includes(yearHyphen)
    ? undefined
    : `The financial year is written with ${characterName(yearHyphen)}, which a number may not contain, so leave the year out.`;
}

function separatorProblem(
  format: TypedFormat,
  rule: SerialNumberRule,
  named: readonly string[],
): string | undefined {
  const { separator, year } = format;
  if (
    separator.length > separatorMaxLength ||
    (separator.length === 1 && !rule.specialCharacters.includes(separator))
  ) {
    return named.length === 0
      ? "Leave it empty, because a number may contain only capital letters and digits."
      : `Use ${listOf(named, "or")}, or nothing.`;
  }
  return year === "none" && separator.length > 0
    ? "The separator follows the financial year, so leave it empty when the year is not shown."
    : undefined;
}

function digitsProblem(digits: number): string | undefined {
  return Number.isInteger(digits) && digits >= 1 && digits <= maxDigits
    ? undefined
    : `Use 1 to ${maxDigits} digits for the running number.`;
}

// A number begins with a capital letter or a digit from 1 to 9 because the Invoice Registration Portal refuses an e-invoice
// whose number begins with "0", "/" or "-" (Generate IRN API 1.03, validation 8).
function wholeNumberProblem(
  format: NumberFormat,
  rule: SerialNumberRule,
  financialYear: string,
): { readonly field: FormatField; readonly message: string } | undefined {
  const first = renderNumber(format, financialYear, 1n);
  if (first.length > rule.maxLength) {
    return {
      field: "format",
      message: `The first number, ${first}, has ${first.length} characters; a number may have at most ${rule.maxLength}.`,
    };
  }
  if (numberStart.test(first)) return undefined;

  return {
    field: firstCharacterField(format),
    message: `A number must begin with a letter or a digit other than 0, and ${first} does not.`,
  };
}

function firstCharacterField(format: NumberFormat): FormatField {
  if (format.prefix.length > 0) return "format.prefix";
  return format.year === "none" ? "format.digits" : "format.year";
}

function isAllowed(character: string, rule: SerialNumberRule): boolean {
  return (
    upperCaseLetter.test(character) ||
    asciiDigit.test(character) ||
    rule.specialCharacters.includes(character)
  );
}
