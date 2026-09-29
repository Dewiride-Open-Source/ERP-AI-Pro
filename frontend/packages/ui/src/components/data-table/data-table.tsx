"use client";

import {
  columnVisibilityFeature,
  functionalUpdate,
  rowSelectionFeature,
  rowSortingFeature,
  tableFeatures,
  useTable,
  type Cell,
  type RowData,
  type ColumnDef,
} from "@tanstack/react-table";
import { useMemo, useRef, type ReactNode } from "react";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";
import { cn } from "@dewiride/erp-ui/lib/utils";

import { DataTableCards, type DataTableCard } from "./data-table-cards";
import { DataTableColumnsMenu, type DataTableColumnChoice } from "./data-table-columns-menu";
import { DataTableColumnHeader } from "./data-table-header";
import {
  canToggleColumn,
  hiddenFromVisibility,
  idsFromSelection,
  isDataColumn,
  nextSortDirection,
  resultsStatus,
  selectionState,
  sortingState,
  sortStatus,
  visibilityState,
  type DataTableColumn,
  type DataTableNoun,
  type DataTablePage,
  type DataTableSort,
  type DataTableSortKind,
} from "./data-table-model";
import { DataTablePagination, type DataTableLinkComponent } from "./data-table-pagination";
import { DataTableSelectCheckbox, DataTableSelectionSummary } from "./data-table-selection";
import { DataTableSortSelect, type DataTableSortChoice } from "./data-table-sort-select";
import { useHydrated } from "./use-hydrated";

export type {
  DataTableColumn,
  DataTableNoun,
  DataTablePage,
  DataTableSort,
  DataTableSortDirection,
  DataTableSortKind,
} from "./data-table-model";
export type { DataTableLinkComponent, DataTableLinkProps } from "./data-table-pagination";

export type DataTableRow = RowData;

export interface DataTableSelection<TRow> {
  readonly selectedIds: readonly string[];
  readonly onSelectedIdsChange: (selectedIds: readonly string[]) => void;
  readonly rowLabel: (row: TRow) => string;
}

export interface DataTableSorting {
  readonly sort: DataTableSort;
  readonly onSortChange: (sort: DataTableSort) => void;
}

export interface DataTableColumnVisibility {
  readonly hiddenColumnIds: readonly string[];
  readonly onHiddenColumnIdsChange: (hiddenColumnIds: readonly string[]) => void;
}

export interface DataTablePaging {
  readonly page: DataTablePage;
  readonly pageSizes: readonly number[];
  readonly pageHref: (page: number) => string;
  readonly onPageSizeChange: (pageSize: number) => void;
  readonly linkComponent: DataTableLinkComponent;
}

export interface DataTableProps<TRow> {
  readonly label: string;
  readonly labelledBy?: string | undefined;
  readonly noun: DataTableNoun;
  readonly columns: readonly DataTableColumn<TRow>[];
  readonly rows: readonly TRow[];
  readonly getRowId: (row: TRow) => string;
  readonly paging: DataTablePaging;
  readonly sorting?: DataTableSorting | undefined;
  readonly columnVisibility?: DataTableColumnVisibility | undefined;
  readonly selection?: DataTableSelection<TRow> | undefined;
  readonly filters?: ReactNode;
  readonly empty: ReactNode;
  readonly pending?: boolean | undefined;
}

interface DataTableColumnMeta {
  readonly header: string;
  readonly align: "start" | "end";
  readonly role: "title" | "actions" | undefined;
  readonly sort: DataTableSortKind | undefined;
  readonly className: string | undefined;
}

const features = tableFeatures({
  rowSortingFeature,
  columnVisibilityFeature,
  rowSelectionFeature,
  columnMeta: {} as DataTableColumnMeta,
});

type Features = typeof features;

const noHiddenColumns: readonly string[] = [];

function columnDefinitions<TRow extends RowData>(
  columns: readonly DataTableColumn<TRow>[],
): ColumnDef<Features, TRow>[] {
  return columns.map((column) => ({
    id: column.id,
    header: column.header,
    cell: ({ row }) => column.cell(row.original),
    enableSorting: column.sort !== undefined,
    enableHiding: isDataColumn(column) && column.hideable !== false,
    meta: {
      header: column.header,
      align: column.align ?? "start",
      role: column.role,
      sort: column.sort,
      className: column.className,
    },
  }));
}

function metaOf<TRow extends RowData>(cell: Cell<Features, TRow, unknown>): DataTableColumnMeta | undefined {
  return cell.column.columnDef.meta;
}

function ariaSort(sorted: false | "asc" | "desc"): "ascending" | "descending" | undefined {
  if (sorted === false) return undefined;
  return sorted === "asc" ? "ascending" : "descending";
}

function focusShown(elements: readonly (HTMLElement | null)[]): void {
  elements.find((element) => element !== null && element.getClientRects().length > 0)?.focus();
}

export function DataTable<TRow extends RowData>({
  label,
  labelledBy,
  noun,
  columns,
  rows,
  getRowId,
  paging,
  sorting,
  columnVisibility,
  selection,
  filters,
  empty,
  pending = false,
}: DataTableProps<TRow>) {
  const interactive = useHydrated();
  const tableRef = useRef<HTMLTableElement>(null);
  const cardsRef = useRef<HTMLUListElement>(null);
  const columnDefs = useMemo(() => columnDefinitions(columns), [columns]);
  const data = useMemo(() => [...rows], [rows]);
  const columnIds = useMemo(() => columns.map((column) => column.id), [columns]);
  const hiddenColumnIds = columnVisibility?.hiddenColumnIds ?? noHiddenColumns;
  const sort = sorting?.sort;
  const selectedIds = selection?.selectedIds;
  const state = useMemo(
    () => ({
      sorting: sortingState(sort),
      columnVisibility: visibilityState(hiddenColumnIds),
      rowSelection: selectionState(selectedIds ?? noHiddenColumns),
    }),
    [sort, hiddenColumnIds, selectedIds],
  );

  const table = useTable({
    features,
    columns: columnDefs,
    data,
    getRowId: (row) => getRowId(row),
    manualSorting: true,
    enableMultiSort: false,
    enableSortingRemoval: false,
    enableRowSelection: selection !== undefined,
    enableRowRangeSelection: false,
    state,
    onColumnVisibilityChange: (updater) =>
      columnVisibility?.onHiddenColumnIdsChange(
        hiddenFromVisibility(functionalUpdate(updater, state.columnVisibility), columnIds),
      ),
    onRowSelectionChange: (updater) =>
      selection?.onSelectedIdsChange(idsFromSelection(functionalUpdate(updater, state.rowSelection))),
  });

  function sortBy(columnId: string, kind: DataTableSortKind, current: false | "asc" | "desc"): void {
    sorting?.onSortChange({ columnId, direction: nextSortDirection(current, kind) });
  }

  function clearSelection(): void {
    selection?.onSelectedIdsChange([]);
    focusShown([tableRef.current, cardsRef.current]);
  }

  const rowModel = table.getRowModel().rows;
  const visibleColumns = table.getVisibleLeafColumns();
  const sortColumn = sort === undefined ? undefined : columns.find((column) => column.id === sort.columnId);
  const sortText =
    sort !== undefined && sortColumn?.sort !== undefined
      ? sortStatus(sortColumn.header, sortColumn.sort, sort.direction)
      : undefined;

  const sortChoices: DataTableSortChoice[] = visibleColumns.flatMap((column) => {
    const meta = column.columnDef.meta;
    return meta?.sort === undefined ? [] : [{ columnId: column.id, header: meta.header, kind: meta.sort }];
  });

  const columnChoices: DataTableColumnChoice[] = columns.filter(isDataColumn).flatMap((column) =>
    column.hideable === false
      ? []
      : [
          {
            id: column.id,
            header: column.header,
            visible: !hiddenColumnIds.includes(column.id),
            toggleable: canToggleColumn(column.id, columns, hiddenColumnIds),
          },
        ],
  );

  const allSelected = table.getIsAllPageRowsSelected();
  const someSelected = table.getIsSomePageRowsSelected();

  const cards: DataTableCard[] = rowModel.map((row) => {
    const cells = row.getVisibleCells();
    const titleCell =
      cells.find((cell) => metaOf(cell)?.role === "title") ??
      cells.find((cell) => metaOf(cell)?.role !== "actions");
    const actionsCell = cells.find((cell) => metaOf(cell)?.role === "actions");
    return {
      id: row.id,
      selected: row.getIsSelected(),
      selectLabel: selection === undefined ? undefined : `Select ${selection.rowLabel(row.original)}`,
      title: titleCell === undefined ? null : <table.FlexRender cell={titleCell} />,
      details: cells
        .filter((cell) => cell !== titleCell && cell !== actionsCell)
        .map((cell) => ({
          columnId: cell.column.id,
          header: metaOf(cell)?.header ?? cell.column.id,
          content: <table.FlexRender cell={cell} />,
        })),
      actions: actionsCell === undefined ? null : <table.FlexRender cell={actionsCell} />,
    };
  });

  const status = (
    <p role="status" data-testid="data-table-status" className="text-muted-foreground">
      {pending ? (
        "Loading…"
      ) : (
        <>
          {resultsStatus(paging.page, noun)}
          {sortText !== undefined && rowModel.length > 1 ? (
            <span className="sr-only"> {sortText}</span>
          ) : null}
        </>
      )}
    </p>
  );

  return (
    <div data-slot="data-table" className="@container/data-table grid min-w-0 gap-4">
      {filters !== undefined || sortChoices.length > 0 || columnChoices.length > 0 ? (
        <div className="flex flex-wrap items-end gap-3">
          {filters !== undefined ? (
            <div className="min-w-0 basis-full @4xl/data-table:grow @4xl/data-table:basis-0">{filters}</div>
          ) : null}
          <div className="ms-auto flex flex-wrap items-center gap-2">
            {sorting !== undefined && sortChoices.length > 0 && rowModel.length > 1 ? (
              <DataTableSortSelect
                choices={sortChoices}
                sort={sorting.sort}
                onSortChange={sorting.onSortChange}
                disabled={!interactive}
                className="@2xl/data-table:hidden"
              />
            ) : null}
            {columnVisibility !== undefined && columnChoices.length > 0 ? (
              <DataTableColumnsMenu
                columns={columnChoices}
                disabled={!interactive}
                onVisibleChange={(columnId, visible) => table.getColumn(columnId)?.toggleVisibility(visible)}
              />
            ) : null}
          </div>
        </div>
      ) : null}

      {selection !== undefined ? (
        <DataTableSelectionSummary
          count={selection.selectedIds.length}
          noun={noun}
          onClear={clearSelection}
        />
      ) : null}

      {rowModel.length === 0 ? (
        empty
      ) : (
        <div
          data-pending={pending || undefined}
          className="transition-opacity duration-(--motion-duration-fast) data-pending:opacity-60"
        >
          <Table
            ref={tableRef}
            tabIndex={-1}
            aria-label={labelledBy === undefined ? label : undefined}
            aria-labelledby={labelledBy}
            aria-busy={pending || undefined}
            data-testid="data-table"
            className="hidden outline-none @2xl/data-table:table"
          >
            <TableHeader>
              {table.getHeaderGroups().map((group) => (
                <TableRow key={group.id}>
                  {selection !== undefined ? (
                    <TableHead className="w-8">
                      <DataTableSelectCheckbox
                        checked={allSelected ? true : someSelected ? "indeterminate" : false}
                        label="Select all rows on this page"
                        disabled={!interactive}
                        onCheckedChange={(checked) => table.toggleAllPageRowsSelected(checked)}
                      />
                    </TableHead>
                  ) : null}
                  {group.headers.map((header) => {
                    const meta = header.column.columnDef.meta;
                    const sorted = header.column.getIsSorted();
                    const kind = meta?.sort;
                    return (
                      <TableHead
                        key={header.id}
                        scope="col"
                        aria-sort={ariaSort(sorted)}
                        className={cn(meta?.align === "end" && "text-right")}
                      >
                        {meta?.role === "actions" ? (
                          <span className="sr-only">{meta.header}</span>
                        ) : sorting !== undefined && kind !== undefined ? (
                          <DataTableColumnHeader
                            header={meta?.header ?? header.column.id}
                            sorted={sorted}
                            align={meta?.align ?? "start"}
                            disabled={!interactive}
                            onSort={() => sortBy(header.column.id, kind, sorted)}
                          />
                        ) : (
                          (meta?.header ?? header.column.id)
                        )}
                      </TableHead>
                    );
                  })}
                </TableRow>
              ))}
            </TableHeader>
            <TableBody>
              {rowModel.map((row) => (
                <TableRow
                  key={row.id}
                  data-testid="data-table-row"
                  data-state={row.getIsSelected() ? "selected" : undefined}
                >
                  {selection !== undefined ? (
                    <TableCell>
                      <DataTableSelectCheckbox
                        checked={row.getIsSelected()}
                        label={`Select ${selection.rowLabel(row.original)}`}
                        disabled={!interactive}
                        onCheckedChange={(checked) => row.toggleSelected(checked)}
                      />
                    </TableCell>
                  ) : null}
                  {row.getVisibleCells().map((cell) => {
                    const meta = metaOf(cell);
                    const className = cn(meta?.align === "end" && "text-right", meta?.className);
                    return meta?.role === "title" ? (
                      <TableHead
                        key={cell.id}
                        scope="row"
                        className={cn("h-auto p-2 font-normal", className)}
                      >
                        <table.FlexRender cell={cell} />
                      </TableHead>
                    ) : (
                      <TableCell key={cell.id} className={className}>
                        <table.FlexRender cell={cell} />
                      </TableCell>
                    );
                  })}
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <DataTableCards
            cards={cards}
            label={label}
            labelledBy={labelledBy}
            pending={pending}
            onSelectedChange={
              selection === undefined
                ? undefined
                : (id, selected) => table.getRow(id)?.toggleSelected(selected)
            }
            interactive={interactive}
            listRef={cardsRef}
            className="@2xl/data-table:hidden"
          />
        </div>
      )}

      <DataTablePagination
        label={label}
        page={paging.page}
        pageSizes={paging.pageSizes}
        status={status}
        pageHref={paging.pageHref}
        onPageSizeChange={paging.onPageSizeChange}
        interactive={interactive}
        linkComponent={paging.linkComponent}
      />
    </div>
  );
}
