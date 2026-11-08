import {
  lastNumberWithin,
  maxDigits,
  prefixMaxLength,
  renderNumber,
  runningNumberOf,
  sameFormat,
  type NumberFormat,
  type SerialNumberRule,
} from "./number-format.ts";
import type { NumberedDocument, NumberingYear } from "./numbering-items.ts";

export interface TextSegment {
  readonly text: string;
  readonly isNumber: boolean;
}

export interface YearLine {
  readonly year: string;
  readonly text: readonly TextSegment[];
}

export const numberingDescription =
  "Each kind of document has numbers of its own. Every financial year starts again at 1 and its numbers follow each other without a gap. Once a year has given a number, its format and next number stay as they are, and a change applies from the next year.";

export const notSetUpText = "Not set up yet.";

export const noDocumentsTitle = "No documents are numbered yet.";

export const noDocumentsDescription =
  "Each kind of document the ERP issues, such as tax invoices, appears here once the part of the ERP that issues it is in use.";

export const numberingUnavailable = "The kinds of document are not available.";

export const yearChoiceDescription = "Shown after the text the numbers start with, in full or short.";

export const separatorChoiceDescription = "The character between the year and the running number.";

export const digitsDescription = `The running number is padded with zeros to this many digits, from 1 to ${maxDigits}: 3 gives 001.`;

export const incompletePreview = "Enter the digits of the running number to see the numbers.";

const counted = new Intl.NumberFormat("en-IN");

const characterNames = new Map([
  ["/", "slash"],
  ["-", "hyphen"],
  [".", "full stop"],
  ["_", "underscore"],
]);

const wholeNumber = /^\d+$/;

export function plainText(segments: readonly TextSegment[]): string {
  return segments.map((segment) => segment.text).join("");
}

export function noRuleText(document: Pick<NumberedDocument, "currentYear" | "series">): string {
  return document.series === null
    ? `The ERP holds no rule for these numbers in ${document.currentYear}, so they cannot be numbered yet.`
    : `The ERP holds no rule for these numbers in ${document.currentYear}, so their numbering cannot change.`;
}

export function followingYear(financialYear: string): string {
  const start = Number(financialYear.slice(0, 4)) + 1;
  return `${start}-${String((start + 1) % 100).padStart(2, "0")}`;
}

export function numbersGiven(year: NumberingYear): bigint {
  return year.lastIssuedText === null
    ? 0n
    : runningNumberOf(year.lastIssuedText, year.format) - runningNumberOf(year.firstText, year.format) + 1n;
}

export function yearText(year: NumberingYear): readonly TextSegment[] {
  const given = numbersGiven(year);
  if (year.lastIssuedText === null || given === 0n) {
    return year.nextText === null ? [words("No number given yet.")] : firstWillBe(year.nextText);
  }

  const range =
    given === 1n
      ? [number(year.firstText)]
      : [number(year.firstText), words(" to "), number(year.lastIssuedText)];
  return year.nextText === null
    ? [words(`Every number that fits is used: ${countText(given)}, `), ...range, words(".")]
    : [words(`${countText(given)} given, `), ...range, words("; next "), number(year.nextText), words(".")];
}

// A year opens when its first number is given, at 1 in the numbering's format, so the current year shows that number until
// then; without a rule for the year it cannot open at all.
export function yearLines(document: NumberedDocument): readonly YearLine[] {
  const { series, currentYear, rule } = document;
  if (series === null) return [];

  const lines = series.years.map((year) => ({ year: year.year, text: yearText(year) }));
  if (rule !== null && !series.years.some((year) => year.year === currentYear)) {
    lines.push({ year: currentYear, text: firstWillBe(renderNumber(series.format, currentYear, 1n)) });
  }
  return lines.sort((first, second) => second.year.localeCompare(first.year));
}

// A change made after the current year gave its first number waits for the next year, which has not opened yet.
export function pendingFormatText(document: NumberedDocument): readonly TextSegment[] | undefined {
  const { series, currentYear } = document;
  const current = series?.years.find((year) => year.year === currentYear);
  if (
    series === null ||
    current === undefined ||
    !current.inUse ||
    sameFormat(current.format, series.format)
  ) {
    return undefined;
  }

  const next = followingYear(currentYear);
  if (series.years.some((year) => year.year === next)) return undefined;
  return [
    words(`The new format applies from ${next}, whose first number will be `),
    number(renderNumber(series.format, next, 1n)),
    words("."),
  ];
}

export function setUpLabel(title: string): string {
  return `Set up the numbering of ${title}`;
}

export function changeLabel(title: string): string {
  return `Change the numbering of ${title}`;
}

export function setUpMessage(title: string): string {
  return `Set up the numbering of ${title}.`;
}

export function changedMessage(title: string): string {
  return `Saved the numbering of ${title}.`;
}

export function appliesFrom(currentYear: string, nextNumberFixed: boolean): string {
  return nextNumberFixed ? followingYear(currentYear) : currentYear;
}

export function dialogDescription(document: NumberedDocument, nextNumberFixed: boolean): string {
  const { currentYear, series } = document;
  if (series === null) {
    return `Its numbers start in ${currentYear} at the next number you give, and every later financial year starts again at 1.`;
  }
  return nextNumberFixed
    ? `${currentYear} has given numbers, so they keep their format, and a change applies from ${followingYear(currentYear)}.`
    : `${currentYear} has given no number yet, so a change applies to it at once.`;
}

export function prefixDescription(rule: SerialNumberRule): string {
  const characters = rule.specialCharacters.split("").map((character) => `"${character}"`);
  const allowed = ["capital letters", "digits", ...characters];
  return `Optional. Up to ${prefixMaxLength} characters: ${allowed.slice(0, -1).join(", ")} and ${allowed.at(-1) ?? ""}.`;
}

export function nextNumberDescription(currentYear: string): string {
  return `The running number of the next document in ${currentYear}: 1, or the number after the last one given outside the ERP this year.`;
}

export function fixedNextNumberDescription(currentYear: string, nextText: string | null): string {
  return nextText === null
    ? `${currentYear} has used every number that fits, so its next number cannot change.`
    : `${currentYear} has given numbers, so it goes on from ${nextText}.`;
}

export function characterLabel(character: string): string {
  const name = characterNames.get(character);
  return name === undefined ? `"${character}"` : `${character} (${name})`;
}

// The year starts at the next number typed when that number fits, and at 1 otherwise, which is where a year the change waits
// for starts as well.
export function previewText(
  format: NumberFormat | undefined,
  nextNumber: string,
  rule: SerialNumberRule,
  financialYear: string,
): readonly TextSegment[] {
  if (format === undefined) return [words(incompletePreview)];

  const first = renderNumber(format, financialYear, 1n);
  if (first.length > rule.maxLength) {
    return [
      number(first),
      words(` has ${first.length} characters, more than the ${rule.maxLength} a number may have.`),
    ];
  }

  const last = lastNumberWithin(format, rule.maxLength);
  const typed = nextNumber.trim();
  const start = wholeNumber.test(typed) && BigInt(typed) >= 1n && BigInt(typed) <= last ? BigInt(typed) : 1n;
  return [
    words(`${financialYear} starts at `),
    number(renderNumber(format, financialYear, start)),
    words(`; the last number that fits in ${rule.maxLength} characters is `),
    number(renderNumber(format, financialYear, last)),
    words("."),
  ];
}

function firstWillBe(text: string): readonly TextSegment[] {
  return [words("No number given yet; the first will be "), number(text), words(".")];
}

function countText(count: bigint): string {
  return `${counted.format(count)} ${count === 1n ? "number" : "numbers"}`;
}

function words(text: string): TextSegment {
  return { text, isNumber: false };
}

function number(text: string): TextSegment {
  return { text, isNumber: true };
}
