import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

import {
  formatProblems,
  isYearStyle,
  lastNumberWithin,
  maxDigits,
  prefixMaxLength,
  runningNumberOf,
  separatorMaxLength,
  type FormatField,
  type NumberFormat,
  type SerialNumberRule,
  type TypedFormat,
} from "../components/number-format.ts";
import type { NumberedDocument } from "../components/numbering-items.ts";

export interface NumberingRules {
  readonly rule: SerialNumberRule;
  readonly currentYear: string;
}

export type ChangeRules = NumberingRules & { readonly nextNumberFixed: boolean };

type Issue = { readonly path: readonly (string | number)[]; readonly message: string };

type FormatValues = z.output<typeof formatValues>;

const documentTypeMaxLength = 128;

const recordVersionMaxLength = 64;

const nextNumberMissing = "Enter the next number.";

const nextNumberNotWhole = "Enter a whole number, such as 1.";

const yearMissing = "Choose how the financial year is shown, or that it is not shown.";

const digitsOutOfRange = `Use 1 to ${maxDigits} digits for the running number.`;

const wholeNumber = /^\d+$/;

// The request carries the next number as a JSON number, which keeps every digit only up to Number.MAX_SAFE_INTEGER.
const largestSentNumber = BigInt(Number.MAX_SAFE_INTEGER);

const issuePaths: Readonly<Record<FormatField, readonly string[]>> = {
  format: [],
  "format.prefix": ["format", "prefix"],
  "format.year": ["format", "year"],
  "format.separator": ["format", "separator"],
  "format.digits": ["format", "digits"],
};

const formatValues = z.object({
  prefix: z.string(),
  year: z.string(),
  separator: z.string(),
  digits: z.string(),
});

const documentTypeSchema = z
  .string({ error: formMessages.pageOutOfDate })
  .min(1, { error: formMessages.pageOutOfDate })
  .max(documentTypeMaxLength, { error: formMessages.pageOutOfDate });

const versionSchema = z
  .string({ error: formMessages.pageOutOfDate })
  .min(1, { error: formMessages.pageOutOfDate })
  .max(recordVersionMaxLength, { error: formMessages.pageOutOfDate });

// Every check reads the values as typed, so one pass finds them all and words each as the API words it; without the rules (in
// the Server Function, which has not read them) only the shape the request takes is checked, and the API checks the rest.
export function setUpNumberSeriesSchema(rules?: NumberingRules) {
  return z
    .object({ documentType: documentTypeSchema, format: formatValues, nextNumber: z.string() })
    .transform((values, context) => {
      const format = formatOf(values.format);
      const issues = [
        ...formatIssues(values.format, rules),
        ...nextNumberIssues(values.nextNumber, format, rules, false),
      ];
      if (format === undefined || issues.length > 0) {
        for (const issue of issues) {
          context.addIssue({ code: "custom", path: [...issue.path], message: issue.message });
        }
        return z.NEVER;
      }
      return { documentType: values.documentType, format, nextNumber: Number(values.nextNumber.trim()) };
    });
}

export function changeNumberSeriesSchema(rules?: ChangeRules) {
  return z
    .object({
      id: z.guid({ error: formMessages.pageOutOfDate }),
      format: formatValues,
      nextNumber: z.string(),
      version: versionSchema,
    })
    .transform((values, context) => {
      const format = formatOf(values.format);
      const optional = rules === undefined || rules.nextNumberFixed;
      const issues = [
        ...formatIssues(values.format, rules),
        ...nextNumberIssues(values.nextNumber, format, rules, optional),
      ];
      if (format === undefined || issues.length > 0) {
        for (const issue of issues) {
          context.addIssue({ code: "custom", path: [...issue.path], message: issue.message });
        }
        return z.NEVER;
      }
      const nextNumber = values.nextNumber.trim();
      return {
        id: values.id,
        format,
        nextNumber: nextNumber === "" ? null : Number(nextNumber),
        version: values.version,
      };
    });
}

export type SetUpNumberSeriesValues = z.input<ReturnType<typeof setUpNumberSeriesSchema>>;

export type ChangeNumberSeriesValues = z.input<ReturnType<typeof changeNumberSeriesSchema>>;

export type NumberFormatValues = SetUpNumberSeriesValues["format"];

export function setUpNumberSeriesValues(documentType: string): SetUpNumberSeriesValues {
  return {
    documentType,
    format: { prefix: "", year: "none", separator: "", digits: "1" },
    nextNumber: "1",
  };
}

// The current year takes a new next number only while it has given none; a year that has not opened yet opens at 1.
export function changeNumberSeriesValues(document: NumberedDocument): ChangeNumberSeriesValues {
  const series = document.series;
  if (series === null) throw new RangeError(`${document.title} have no numbering to change.`);

  const current = series.years.find((year) => year.year === document.currentYear);
  const nextNumber =
    current === undefined
      ? "1"
      : current.inUse || current.nextText === null
        ? ""
        : runningText(current.nextText, current.format);
  return {
    id: series.id,
    format: {
      prefix: series.format.prefix,
      year: series.format.year,
      separator: series.format.separator,
      digits: String(series.format.digits),
    },
    nextNumber,
    version: series.version,
  };
}

export function nextNumberFixed(document: NumberedDocument): boolean {
  return document.series?.years.some((year) => year.year === document.currentYear && year.inUse) ?? false;
}

// The format a set of typed values describes, with the separator left out when the year is not shown, because the field that
// holds it is hidden then; undefined while the year or the digits are not yet a value the request can carry.
export function formatOf(values: FormatValues): NumberFormat | undefined {
  const digits = digitsOf(values.digits);
  if (!isYearStyle(values.year) || digits === undefined) return undefined;
  return {
    prefix: values.prefix,
    year: values.year,
    separator: values.year === "none" ? "" : values.separator,
    digits,
  };
}

export function nextNumberLimit(format: NumberFormat | undefined, rules: NumberingRules | undefined): bigint {
  if (
    format === undefined ||
    rules === undefined ||
    formatProblems(format, rules.rule, rules.currentYear).size > 0
  ) {
    return largestSentNumber;
  }
  const last = lastNumberWithin(format, rules.rule.maxLength);
  return last < largestSentNumber ? last : largestSentNumber;
}

export const numberSeriesProblemMessages: ProblemMessages = {
  "concurrency.conflict":
    "Someone changed this numbering, or a document of this kind was being numbered, while you worked. Show the latest numbering, then try again.",
  "numbering.already-set-up":
    "Someone set this numbering up after the page was loaded. Show the latest numbering, then change it if needed.",
  "numbering.series-not-found":
    "This numbering no longer exists. Show the latest numbering, then set it up again.",
};

export const pageOutOfDateCodes: ReadonlySet<string> = new Set(Object.keys(numberSeriesProblemMessages));

function formatIssues(values: FormatValues, rules: NumberingRules | undefined): Issue[] {
  const { year } = values;
  const format: TypedFormat = {
    prefix: values.prefix,
    year,
    separator: year === "none" ? "" : values.separator,
    digits: digitsOf(values.digits) ?? Number.NaN,
  };
  if (rules !== undefined) {
    return [...formatProblems(format, rules.rule, rules.currentYear)].map(([field, message]) => ({
      path: issuePaths[field],
      message,
    }));
  }

  const issues: Issue[] = [];
  if (format.prefix.length > prefixMaxLength) {
    issues.push({ path: issuePaths["format.prefix"], message: `Use at most ${prefixMaxLength} characters.` });
  }
  if (!isYearStyle(year)) issues.push({ path: issuePaths["format.year"], message: yearMissing });
  if (format.separator.length > separatorMaxLength) {
    issues.push({ path: issuePaths["format.separator"], message: "Use one character, or nothing." });
  }
  if (Number.isNaN(format.digits) || format.digits < 1 || format.digits > maxDigits) {
    issues.push({ path: issuePaths["format.digits"], message: digitsOutOfRange });
  }
  return issues;
}

function nextNumberIssues(
  text: string,
  format: NumberFormat | undefined,
  rules: NumberingRules | undefined,
  optional: boolean,
): Issue[] {
  const typed = text.trim();
  if (typed === "") return optional ? [] : [{ path: ["nextNumber"], message: nextNumberMissing }];
  if (!wholeNumber.test(typed)) return [{ path: ["nextNumber"], message: nextNumberNotWhole }];

  const limit = nextNumberLimit(format, rules);
  const number = BigInt(typed);
  return number < 1n || number > limit
    ? [{ path: ["nextNumber"], message: `Use a number from 1 to ${limit}.` }]
    : [];
}

function digitsOf(text: string): number | undefined {
  const typed = text.trim();
  if (!wholeNumber.test(typed)) return undefined;
  const digits = Number(typed);
  return digits >= 1 && digits <= maxDigits ? digits : undefined;
}

function runningText(text: string, format: NumberFormat): string {
  return runningNumberOf(text, format).toString();
}
