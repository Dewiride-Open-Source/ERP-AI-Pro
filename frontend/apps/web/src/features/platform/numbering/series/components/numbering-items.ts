import type {
  NumberedDocumentResponse,
  NumberFormatResponse,
  NumberSeriesResponse,
  NumberSeriesYearResponse,
  SerialNumberRuleResponse,
} from "@dewiride/erp-api-client";

import { isYearStyle, runningNumberOf, type NumberFormat, type SerialNumberRule } from "./number-format.ts";

export interface NumberingYear {
  readonly year: string;
  readonly format: NumberFormat;
  readonly maxLength: number;
  readonly inUse: boolean;
  readonly firstText: string;
  readonly nextText: string | null;
  readonly lastText: string;
  readonly lastIssuedText: string | null;
}

export interface NumberSeries {
  readonly id: string;
  readonly format: NumberFormat;
  readonly version: string;
  readonly years: readonly NumberingYear[];
}

export interface NumberedDocument {
  readonly documentType: string;
  readonly title: string;
  readonly description: string;
  readonly currentYear: string;
  readonly rule: SerialNumberRule | null;
  readonly series: NumberSeries | null;
}

export class UnreadableAnswerError extends TypeError {
  override readonly name = "UnreadableAnswerError";
}

// The API's contract names every member below as present, so a member that arrives without a value means the answer cannot be
// shown faithfully, and the page reports the kinds of document as unreadable rather than show part of one. A year's running
// numbers are left out: they travel as JSON numbers, which keep every digit only up to Number.MAX_SAFE_INTEGER, so the page
// reads them from the numbers' texts.
function given<T>(value: T | null | undefined, member: string): T {
  if (value === null || value === undefined) {
    throw new UnreadableAnswerError(`The API answered a kind of document without its ${member}.`);
  }
  return value;
}

function named(value: string | null | undefined, member: string): string {
  const text = given(value, member);
  if (text === "")
    throw new UnreadableAnswerError(`The API answered a kind of document with an empty ${member}.`);
  return text;
}

// Kiota reads a JSON null where an object belongs as an object without members, so such an object is the API's null.
function present<T extends object>(value: T | null | undefined): T | null {
  return value === null || value === undefined || Object.keys(value).length === 0 ? null : value;
}

function formatOf(format: NumberFormatResponse | null | undefined, member: string): NumberFormat {
  const answered = given(present(format), member);
  const year = given(answered.year, `${member}'s year`);
  if (!isYearStyle(year))
    throw new UnreadableAnswerError(`The API answered a ${member} with the year ${year}.`);
  return {
    prefix: given(answered.prefix, `${member}'s prefix`),
    year,
    separator: given(answered.separator, `${member}'s separator`),
    digits: given(answered.digits, `${member}'s digits`),
  };
}

function ruleOf(rule: SerialNumberRuleResponse | null | undefined): SerialNumberRule | null {
  const answered = present(rule);
  return answered === null
    ? null
    : {
        maxLength: given(answered.maxLength, "rule's most characters"),
        specialCharacters: given(answered.specialCharacters, "rule's special characters"),
      };
}

function yearOf(year: NumberSeriesYearResponse): NumberingYear {
  const read: NumberingYear = {
    year: named(year.year, "year"),
    format: formatOf(year.format, "year's format"),
    maxLength: given(year.maxLength, "year's most characters"),
    inUse: given(year.inUse, "year's use"),
    firstText: named(year.firstText, "year's first number"),
    nextText: year.nextText ?? null,
    lastText: named(year.lastText, "year's last number"),
    lastIssuedText: year.lastIssuedText ?? null,
  };
  for (const text of [read.firstText, read.nextText, read.lastText, read.lastIssuedText]) {
    if (text !== null && !endsInRunningNumber(text, read.format)) {
      throw new UnreadableAnswerError(
        `The API answered ${read.year} with the number ${text}, which its format does not give.`,
      );
    }
  }
  return read;
}

function endsInRunningNumber(text: string, format: NumberFormat): boolean {
  try {
    return runningNumberOf(text, format) >= 1n;
  } catch {
    return false;
  }
}

function seriesOf(series: NumberSeriesResponse | null | undefined): NumberSeries | null {
  const answered = present(series);
  return answered === null
    ? null
    : {
        id: named(answered.id, "numbering's id"),
        format: formatOf(answered.format, "numbering's format"),
        version: named(answered.version, "numbering's version"),
        years: (answered.years ?? []).map(yearOf),
      };
}

export function numberedDocumentOf(document: NumberedDocumentResponse): NumberedDocument {
  return {
    documentType: named(document.documentType, "key"),
    title: named(document.title, "title"),
    description: named(document.description, "description"),
    currentYear: named(document.currentYear, "current financial year"),
    rule: ruleOf(document.rule),
    series: seriesOf(document.series),
  };
}
