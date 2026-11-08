import { groupIndian } from "@dewiride/erp-ui/lib/indian-number";

import type {
  SearchedListColumn,
  SearchedListRow,
  SearchKeys,
} from "@/shared/lists/searched/searched-list-view";

import type { ParameterColumn, ParameterRow } from "../../parameter-sets/components/set-items.ts";
import { longDate } from "../../parameter-sets/components/set-text.ts";

export const noValue = "None";

export const sourceHeader = "Source";

// A column's name is camelCase letters and digits, so an id with a hyphen can never be one of the set's own columns.
export const sourceColumnId = "entry-source";

const searchedKinds: ReadonlySet<ParameterColumn["kind"]> = new Set(["text", "code", "choice"]);

export function cellValue(row: ParameterRow, column: string): string | null {
  return row.cells.find((cell) => cell.column === column)?.value ?? null;
}

// A value arrives as the canonical text the API keeps (a percentage "2.5", an amount in rupees "50000000", a date
// "2019-10-01"), never as a number, so it is shown without rounding: a percentage with its sign, an amount in rupees grouped
// in lakhs and crores, with paise only when it has them, a date in words and a choice by its title.
export function cellText(column: ParameterColumn, value: string | null): string {
  if (value === null) return noValue;
  switch (column.kind) {
    case "percent":
      return `${value}%`;
    case "amount":
      return `₹${groupIndian(value, value.includes(".") ? 2 : 0)}`;
    case "date":
      return longDate(value);
    case "choice":
      return column.choices.find((choice) => choice.value === value)?.title ?? value;
    default:
      return value;
  }
}

// A set names the column its entries are titled by; without one, an entry is titled by its key.
export function titleColumn(columns: readonly ParameterColumn[]): ParameterColumn | undefined {
  return columns.find((column) => column.isRowTitle) ?? columns.find((column) => column.isKey);
}

export function entryTitle(columns: readonly ParameterColumn[], row: ParameterRow): string {
  const title = columns.find((column) => column.isRowTitle);
  if (title !== undefined) return cellText(title, cellValue(row, title.name));
  const key = columns.find((column) => column.isKey);
  return key === undefined ? row.key : `${key.title} ${row.key}`;
}

export function entryColumns(columns: readonly ParameterColumn[]): readonly SearchedListColumn[] {
  const title = titleColumn(columns);
  return [
    ...columns.map((column) => ({ id: column.name, header: column.title, title: column === title })),
    { id: sourceColumnId, header: sourceHeader },
  ];
}

export function entryRow(columns: readonly ParameterColumn[], row: ParameterRow): SearchedListRow {
  return {
    id: row.key,
    cells: {
      ...Object.fromEntries(
        columns.map((column) => [column.name, cellText(column, cellValue(row, column.name))]),
      ),
      [sourceColumnId]: row.source,
    },
  };
}

// A search finds an entry by what names it: its key, and the words of its text, its codes and the titles of its choices. Its
// figures (rates, amounts, days) are values to read, not words to look an entry up by.
export function entrySearchKeys(columns: readonly ParameterColumn[], row: ParameterRow): SearchKeys {
  return {
    code: row.key,
    name: columns
      .filter((column) => searchedKinds.has(column.kind))
      .map((column) => {
        const value = cellValue(row, column.name);
        return value === null ? "" : cellText(column, value);
      })
      .filter((text) => text !== "")
      .join(" "),
  };
}
