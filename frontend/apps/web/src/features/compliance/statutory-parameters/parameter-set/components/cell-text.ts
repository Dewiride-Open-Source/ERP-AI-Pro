import { groupIndian } from "@dewiride/erp-ui/lib/indian-number";

import type {
  SearchedListColumn,
  SearchedListLayout,
  SearchedListRow,
  SearchKeys,
} from "@/shared/lists/searched/searched-list-view";

import type { ParameterColumn, ParameterRow } from "../../parameter-sets/components/set-items.ts";
import { longDate, longDayMonth } from "../../parameter-sets/components/set-text.ts";

export const noValue = "None";

export const sourceHeader = "Source";

// A column's name is camelCase letters and digits, so an id with a hyphen can never be one of the set's own columns.
export const sourceColumnId = "entry-source";

const searchedKinds: ReadonlySet<ParameterColumn["kind"]> = new Set(["text", "code", "choice", "stateCode"]);

export function cellValue(row: ParameterRow, column: string): string | null {
  return row.cells.find((cell) => cell.column === column)?.value ?? null;
}

// A value arrives as the canonical text the API keeps (a percentage "2.5", an amount in rupees "50000000", a date
// "2019-10-01"), never as a number, so it is shown without rounding: a percentage with its sign, an amount in rupees grouped
// in lakhs and crores, with paise only when it has them, a date and a day of the year in words, a choice by its title and a
// GST State code after the name of its State.
export function cellText(
  column: ParameterColumn,
  value: string | null,
  stateName: string | null = null,
): string {
  if (value === null) return noValue;
  switch (column.kind) {
    case "percent":
      return `${value}%`;
    case "amount":
      return `₹${groupIndian(value, value.includes(".") ? 2 : 0)}`;
    case "date":
      return longDate(value);
    case "dayMonth":
      return longDayMonth(value);
    case "choice":
      return column.choices.find((choice) => choice.value === value)?.title ?? value;
    case "stateCode":
      return stateName === null ? value : `${stateName} (${value})`;
    default:
      return value;
  }
}

export function rowCellText(column: ParameterColumn, row: ParameterRow): string {
  const cell = row.cells.find((candidate) => candidate.column === column.name);
  return cellText(column, cell?.value ?? null, cell?.stateName ?? null);
}

// A set names the column its entries are titled by; without one, an entry is titled by its key.
export function titleColumn(columns: readonly ParameterColumn[]): ParameterColumn | undefined {
  return columns.find((column) => column.isRowTitle) ?? columns.find((column) => column.isKey);
}

export function entryTitle(columns: readonly ParameterColumn[], row: ParameterRow): string {
  const title = columns.find((column) => column.isRowTitle);
  if (title !== undefined) return rowCellText(title, row);
  const key = columns.find((column) => column.isKey);
  if (key === undefined) return row.key;
  return key.kind === "stateCode" ? rowCellText(key, row) : `${key.title} ${row.key}`;
}

export function entryColumns(columns: readonly ParameterColumn[]): readonly SearchedListColumn[] {
  const title = titleColumn(columns);
  return [
    ...columns.map((column) => ({ id: column.name, header: column.title, title: column === title })),
    { id: sourceColumnId, header: sourceHeader },
  ];
}

export const tableColumnsMax = 6;

// A set whose entries hold more columns than a table shows legibly side by side, its source included, is shown as cards at
// every width, each entry's values under their headings.
export function entryLayout(columns: readonly ParameterColumn[]): SearchedListLayout {
  return entryColumns(columns).length > tableColumnsMax ? "cards" : "responsive";
}

export function entryRow(columns: readonly ParameterColumn[], row: ParameterRow): SearchedListRow {
  return {
    id: row.key,
    cells: {
      ...Object.fromEntries(columns.map((column) => [column.name, rowCellText(column, row)])),
      [sourceColumnId]: row.source,
    },
  };
}

// A search finds an entry by what names it: its key, and the words of its text, its codes, the titles of its choices and its
// States by name and code. Its figures (rates, amounts, days) are values to read, not words to look an entry up by.
export function entrySearchKeys(columns: readonly ParameterColumn[], row: ParameterRow): SearchKeys {
  return {
    code: row.key,
    name: columns
      .filter((column) => searchedKinds.has(column.kind) && cellValue(row, column.name) !== null)
      .map((column) => rowCellText(column, row))
      .join(" "),
  };
}
