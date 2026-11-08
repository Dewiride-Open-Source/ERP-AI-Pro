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

const asciiDigit = /^[0-9]$/;

const numberStart = /^[A-Z1-9]/;

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
// it. A number begins with a capital letter or a digit from 1 to 9 because the Invoice Registration Portal refuses an
// e-invoice whose number begins with "0", "/" or "-" (Generate IRN API 1.03, validation 8).
export function formatProblems(
  format: TypedFormat,
  rule: SerialNumberRule,
  financialYear: string,
): ReadonlyMap<FormatField, string> {
  const problems = new Map<FormatField, string>();
  const { prefix, year, separator, digits } = format;
  if (prefix.length > prefixMaxLength || !prefix.split("").every((character) => isAllowed(character, rule))) {
    problems.set(
      "format.prefix",
      `Use at most ${prefixMaxLength} characters, each a capital letter, a digit or ${describe(rule.specialCharacters)}.`,
    );
  }

  if (!isYearStyle(year)) {
    problems.set("format.year", "Choose how the financial year is shown, or that it is not shown.");
  } else if (year !== "none" && !rule.specialCharacters.includes(yearHyphen)) {
    problems.set(
      "format.year",
      `The financial year is written with "${yearHyphen}", which a number may not contain, so leave the year out.`,
    );
  }

  if (
    separator.length > separatorMaxLength ||
    (separator.length === 1 && !rule.specialCharacters.includes(separator))
  ) {
    problems.set("format.separator", `Use ${describe(rule.specialCharacters)}, or nothing.`);
  } else if (year === "none" && separator.length > 0) {
    problems.set(
      "format.separator",
      "The separator follows the financial year, so leave it empty when the year is not shown.",
    );
  }

  if (!Number.isInteger(digits) || digits < 1 || digits > maxDigits) {
    problems.set("format.digits", `Use 1 to ${maxDigits} digits for the running number.`);
  }

  if (problems.size > 0 || !isYearStyle(year)) return problems;

  const first = renderNumber({ ...format, year }, financialYear, 1n);
  if (first.length > rule.maxLength) {
    problems.set(
      "format",
      `The first number, ${first}, has ${first.length} characters; a number may have at most ${rule.maxLength}.`,
    );
  } else if (!numberStart.test(first)) {
    const field = prefix.length > 0 ? "format.prefix" : year === "none" ? "format.digits" : "format.year";
    problems.set(field, `A number must begin with a letter or a digit other than 0, and ${first} does not.`);
  }
  return problems;
}

export function describe(specialCharacters: string): string {
  return specialCharacters.length === 0
    ? "no other character"
    : specialCharacters
        .split("")
        .map((character) => `"${character}"`)
        .join(" or ");
}

function isAllowed(character: string, rule: SerialNumberRule): boolean {
  return (
    upperCaseLetter.test(character) ||
    asciiDigit.test(character) ||
    rule.specialCharacters.includes(character)
  );
}
