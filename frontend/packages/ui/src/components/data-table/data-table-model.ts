import type { ColumnVisibilityState, RowSelectionState, SortingState } from "@tanstack/react-table";
import type { ReactNode } from "react";

export type DataTableSortDirection = "asc" | "desc";

export type DataTableSortKind = "text" | "number" | "date";

export interface DataTableSort {
  readonly columnId: string;
  readonly direction: DataTableSortDirection;
}

export interface DataTableColumn<TRow> {
  readonly id: string;
  readonly header: string;
  readonly cell: (row: TRow) => ReactNode;
  readonly sort?: DataTableSortKind | undefined;
  readonly hideable?: boolean | undefined;
  readonly align?: "start" | "end" | undefined;
  readonly role?: "title" | "actions" | undefined;
  readonly className?: string | undefined;
}

export interface DataTablePage {
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly pageCount: number;
}

export interface DataTableNoun {
  readonly one: string;
  readonly other: string;
}

const sortDirectionLabels: Readonly<Record<DataTableSortKind, Record<DataTableSortDirection, string>>> = {
  text: { asc: "A to Z", desc: "Z to A" },
  number: { asc: "lowest first", desc: "highest first" },
  date: { asc: "earliest first", desc: "latest first" },
};

const indianCount = new Intl.NumberFormat("en-IN");

export function firstSortDirection(kind: DataTableSortKind): DataTableSortDirection {
  return kind === "text" ? "asc" : "desc";
}

export function nextSortDirection(
  current: DataTableSortDirection | false,
  kind: DataTableSortKind,
): DataTableSortDirection {
  if (current === false) return firstSortDirection(kind);
  return current === "asc" ? "desc" : "asc";
}

export function sortDirectionLabel(kind: DataTableSortKind, direction: DataTableSortDirection): string {
  return sortDirectionLabels[kind][direction];
}

export function sortStatus(
  header: string,
  kind: DataTableSortKind,
  direction: DataTableSortDirection,
): string {
  return `Sorted by ${header}, ${sortDirectionLabel(kind, direction)}.`;
}

export function formatCount(count: number): string {
  return indianCount.format(count);
}

export function pageRange(page: DataTablePage): { first: number; last: number } {
  if (page.totalCount <= 0) return { first: 0, last: 0 };
  const first = (page.page - 1) * page.pageSize + 1;
  return { first, last: Math.min(page.totalCount, page.page * page.pageSize) };
}

export function resultsStatus(page: DataTablePage, noun: DataTableNoun): string {
  if (page.totalCount <= 0) return `No ${noun.other} to show.`;
  if (page.totalCount === 1) return `Showing 1 ${noun.one}.`;
  const total = formatCount(page.totalCount);
  if (page.pageCount <= 1) return `Showing all ${total} ${noun.other}.`;
  const { first, last } = pageRange(page);
  return `Showing ${formatCount(first)}–${formatCount(last)} of ${total} ${noun.other}.`;
}

export function sortingState(sort: DataTableSort | undefined): SortingState {
  return sort === undefined ? [] : [{ id: sort.columnId, desc: sort.direction === "desc" }];
}

export function visibilityState(hiddenColumnIds: readonly string[]): ColumnVisibilityState {
  return Object.fromEntries(hiddenColumnIds.map((id) => [id, false]));
}

export function hiddenFromVisibility(
  visibility: ColumnVisibilityState,
  columnIds: readonly string[],
): readonly string[] {
  return columnIds.filter((id) => visibility[id] === false);
}

export function selectionState(selectedIds: readonly string[]): RowSelectionState {
  return Object.fromEntries(selectedIds.map((id) => [id, true as const]));
}

export function idsFromSelection(selection: RowSelectionState): readonly string[] {
  return Object.keys(selection).filter((id) => selection[id] === true);
}

export function isDataColumn<TRow>(column: DataTableColumn<TRow>): boolean {
  return column.role !== "actions";
}

export function canToggleColumn<TRow>(
  columnId: string,
  columns: readonly DataTableColumn<TRow>[],
  hiddenColumnIds: readonly string[],
): boolean {
  const column = columns.find((candidate) => candidate.id === columnId);
  if (column === undefined || column.hideable === false || !isDataColumn(column)) return false;
  const visibleData = columns.filter(
    (candidate) => isDataColumn(candidate) && !hiddenColumnIds.includes(candidate.id),
  );
  return visibleData.length > 1 || !visibleData.some((candidate) => candidate.id === columnId);
}
