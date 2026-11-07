import type { FileRejection } from "@dewiride/erp-ui/components/upload/file-drop-zone";
import { compareIsoDates } from "@dewiride/erp-ui/lib/calendar-date";

import { formInvalid, type FormState } from "@/shared/forms/state/form-state";
import { maxOptionValues } from "@/shared/lists/list-definition";
import { listHref, type ListQuery } from "@/shared/lists/list-query";

import {
  maxImportedRates,
  rateScale,
  sourceMaxLength,
} from "../../exchange-rates/forms/exchange-rate.schema.ts";
import { exchangeRatesList } from "../../exchange-rates/lists/exchange-rates.list.ts";

import { unitChoices, type RatesFile, type RatesFileColumn, type RatesFileRow } from "./rates-file.ts";
import type { FileTextRefusal } from "./rates-file-text.ts";

export type FileRefusal = FileRejection | FileTextRefusal;

export interface ColumnHelp {
  readonly column: RatesFileColumn;
  readonly text: string;
}

export const nothingRecorded =
  "No rate of the file was recorded. Correct these lines in the file, then choose it again.";

const counts = new Intl.NumberFormat("en-IN");

export const ratesFileLayout = `The first line names the columns, in any order and any case, and each line after it is one rate. A file holds at most ${counts.format(maxImportedRates)} rates.`;

export const ratesFileColumnHelp: readonly ColumnHelp[] = [
  {
    column: "date",
    text: "The day of the rate, written as 31-03-2026 or 2026-03-31: today or an earlier day.",
  },
  { column: "currency", text: "The code of a foreign currency, such as USD." },
  { column: "unit", text: `How many units of the currency the rate is for: ${unitChoices}.` },
  {
    column: "rate",
    text: `The rupees for those units, with up to ${rateScale} digits after the decimal point.`,
  },
  { column: "source", text: `Optional. Where the rate comes from, up to ${sourceMaxLength} characters.` },
];

const ratePath = /^rates\.(\d+)(?:\.|$)/;

function counted(count: number, one: string, other: string): string {
  return count === 1 ? `1 ${one}` : `${counts.format(count)} ${other}`;
}

export function importButtonText(rates: number): string {
  return `Import ${counted(rates, "rate", "rates")}`;
}

export function fileSummaryText(name: string, file: RatesFile): string {
  const problems = file.problems.length;
  if (problems > 0) {
    return `${name} has ${counted(problems, "problem", "problems")}, so it cannot be imported yet. Correct ${problems === 1 ? "it" : "them"} in the file, then choose it again.`;
  }
  return `${name} holds ${counted(file.rows.length, "rate", "rates")}, ready to import.`;
}

export function fileRefusalText(name: string, refusal: FileRefusal): string {
  switch (refusal) {
    case "empty":
      return `${name} is empty. Choose a CSV file that holds rates.`;
    case "too-large":
      return `${name} is larger than 1 MB, more than ${counts.format(1_000)} rates need. Split it into smaller files.`;
    case "unsupported-type":
    case "not-csv":
      return `${name} is not a CSV file. In Excel, save the sheet as CSV UTF-8 (Comma delimited), then choose that file.`;
    case "not-utf8":
      return `${name} is not saved as UTF-8 text. In Excel, save it as CSV UTF-8 (Comma delimited), then choose it again.`;
    case "unreadable":
      return `${name} could not be read. Choose it again.`;
  }
}

export function importOutcomeText(recorded: number, unchanged: number): string {
  const first = recorded === 0 ? "Recorded no new rates." : `Recorded ${counted(recorded, "rate", "rates")}.`;
  if (unchanged === 0) return first;
  const kept =
    unchanged === 1
      ? "1 rate was already recorded and was left as it was."
      : `${counts.format(unchanged)} rates were already recorded and were left as they were.`;
  return `${first} ${kept}`;
}

// The API names each refused rate by its place in the request, rates[i], and the request holds the file's rates in their
// order, so each problem is named by the line of the file that rate came from.
export function withFileLines(state: FormState, lines: readonly number[]): FormState {
  if (state.status !== "invalid") return state;
  const general = [...state.formErrors];
  const lined: { readonly line: number; readonly text: string }[] = [];
  for (const [path, messages] of Object.entries(state.fieldErrors)) {
    const index = ratePath.exec(path)?.[1];
    const line = index === undefined ? undefined : lines[Number(index)];
    for (const message of messages) {
      if (line === undefined) general.push(message);
      else lined.push({ line, text: `Line ${line}: ${message}` });
    }
  }
  lined.sort((first, second) => first.line - second.line);
  return formInvalid({
    fieldErrors: {},
    formErrors: [nothingRecorded, ...general, ...lined.map(({ text }) => text)],
  });
}

// The rates a file recorded are found again by their currencies and their first and last day; the codes are sorted as the
// list orders a chosen filter's values, so the address is already the list's own.
export function importedRatesHref<TBasePath extends string>(
  basePath: TBasePath,
  rows: readonly RatesFileRow[],
) {
  const currencies = [...new Set(rows.map((row) => row.currency))].sort();
  const days = rows.map((row) => row.rateDate).sort(compareIsoDates);
  const query: ListQuery = {
    page: 1,
    pageSize: exchangeRatesList.defaultPageSize,
    sort: exchangeRatesList.defaultSort,
    filters: {
      ...(currencies.length > 0 && currencies.length <= maxOptionValues ? { currency: currencies } : {}),
      ...(days.length > 0 ? { day: { from: days[0] ?? "", to: days.at(-1) ?? "" } } : {}),
    },
    hiddenColumns: [],
  };
  return listHref(basePath, query, exchangeRatesList);
}
