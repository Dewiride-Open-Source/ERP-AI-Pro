"use client";

import {
  DataTable,
  type DataTableColumn,
  type DataTableNoun,
  type DataTablePage,
  type DataTableRow,
} from "@dewiride/erp-ui/components/data-table/data-table";
import type { Route } from "next";
import { useSearchParams } from "next/navigation";
import { useOptimistic, useState, useTransition, type ReactNode } from "react";

import { useFilterFormState } from "./filter-state/use-filter-form-state.ts";
import { listParameters, type ListDefinition } from "./list-definition.ts";
import { ListFilterForm, ListNoMatches } from "./list-filter-form.tsx";
import { ListLink, ListNavigationProvider, useListNavigation } from "./list-navigation.tsx";
import { RowRemovalContext, settleRemoval, type RemoveRow } from "./list-row-removal.tsx";
import {
  filterSignature,
  listLink,
  readHiddenColumns,
  withFilters,
  withHiddenColumns,
  withPage,
  withPageSize,
  withSort,
  type ListQuery,
} from "./list-query.ts";
import { withoutRow, type ShownRows } from "./optimistic-rows.ts";

export interface ListTableSelection<TRow> {
  readonly rowLabel: (row: TRow) => string;
}

export interface ListTableProps<TRow extends DataTableRow> {
  readonly basePath: Route;
  readonly definition: ListDefinition;
  readonly query: ListQuery;
  readonly page: DataTablePage;
  readonly label: string;
  readonly labelledBy?: string | undefined;
  readonly noun: DataTableNoun;
  readonly columns: readonly DataTableColumn<TRow>[];
  readonly rows: readonly TRow[];
  readonly getRowId: (row: TRow) => string;
  readonly empty: ReactNode;
  readonly noMatches: string;
  readonly filters?: ReactNode;
  readonly selection?: ListTableSelection<TRow> | undefined;
}

interface Selection {
  readonly signature: string;
  readonly ids: readonly string[];
}

const noIds: readonly string[] = [];

function ListTableContent<TRow extends DataTableRow>({
  basePath,
  definition,
  query,
  page,
  label,
  labelledBy,
  noun,
  columns,
  rows,
  getRowId,
  empty,
  noMatches,
  filters,
  selection,
}: ListTableProps<TRow>) {
  const { navigate, pending } = useListNavigation();
  const searchParams = useSearchParams();
  const hiddenColumns = readHiddenColumns(
    searchParams.get(listParameters.hiddenColumns) ?? undefined,
    definition,
  );
  const current = withHiddenColumns(query, hiddenColumns);
  const signature = filterSignature(query.filters, definition);
  const [selected, setSelected] = useState<Selection>({ signature, ids: noIds });
  if (selected.signature !== signature) setSelected({ signature, ids: noIds });
  const selectedIds = selected.signature === signature ? selected.ids : noIds;
  const filterForm = useFilterFormState(signature);

  const [shown, hideRow] = useOptimistic<ShownRows<TRow>, string>({ page, rows }, (current, id) =>
    withoutRow(current, id, getRowId),
  );
  const [, startRemoval] = useTransition();
  const removeRow: RemoveRow = (id, remove) =>
    new Promise((resolve) => {
      startRemoval(async () => {
        hideRow(id);
        const outcome = await settleRemoval(remove);
        if (outcome.removed) {
          setSelected((current) => ({
            ...current,
            ids: current.ids.filter((selectedId) => selectedId !== id),
          }));
        }
        resolve(outcome);
      });
    });

  const filtered = Object.keys(query.filters).length > 0;
  const link = (next: ListQuery) => listLink(basePath, next, definition);

  return (
    <RowRemovalContext value={removeRow}>
      <DataTable
        label={label}
        labelledBy={labelledBy}
        noun={noun}
        columns={columns}
        rows={shown.rows}
        getRowId={getRowId}
        pending={pending}
        paging={{
          page: shown.page,
          pageSizes: definition.pageSizes,
          pageHref: (target) => link(withPage(current, target)),
          onPageSizeChange: (pageSize) => navigate(link(withPageSize(current, pageSize))),
          linkComponent: ListLink,
        }}
        sorting={{
          sort: { columnId: query.sort.field, direction: query.sort.direction },
          onSortChange: (sort) =>
            navigate(link(withSort(current, { field: sort.columnId, direction: sort.direction }))),
        }}
        columnVisibility={{
          hiddenColumnIds: hiddenColumns,
          onHiddenColumnIdsChange: (hidden) =>
            window.history.replaceState(null, "", link(withHiddenColumns(current, hidden))),
        }}
        selection={
          selection === undefined
            ? undefined
            : {
                selectedIds,
                onSelectedIdsChange: (ids) => setSelected({ signature, ids }),
                rowLabel: selection.rowLabel,
              }
        }
        filters={
          filters === undefined ? undefined : (
            <ListFilterForm
              key={filterForm.key}
              basePath={basePath}
              definition={definition}
              query={current}
              label={`Filter ${label.toLowerCase()}`}
              focusFirstField={filterForm.focusFirstField}
              onApply={filterForm.onApply}
              onClear={filterForm.onClear}
            >
              {filters}
            </ListFilterForm>
          )
        }
        empty={
          filtered ? (
            <ListNoMatches
              message={noMatches}
              href={link(withFilters(current, {}))}
              onClear={filterForm.onClear}
            />
          ) : (
            empty
          )
        }
      />
    </RowRemovalContext>
  );
}

export function ListTable<TRow extends DataTableRow>(props: ListTableProps<TRow>) {
  return (
    <ListNavigationProvider>
      <ListTableContent {...props} />
    </ListNavigationProvider>
  );
}
