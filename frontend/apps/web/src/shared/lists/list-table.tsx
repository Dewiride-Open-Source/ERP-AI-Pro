"use client";

import {
  DataTable,
  type DataTableColumn,
  type DataTableNoun,
  type DataTablePage,
  type DataTableRow,
} from "@dewiride/erp-ui/components/data-table/data-table";
import { buttonVariants } from "@dewiride/erp-ui/components/ui/button";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from "@dewiride/erp-ui/components/ui/empty";
import type { Route } from "next";
import { useSearchParams } from "next/navigation";
import { useState, type ReactNode } from "react";

import { listParameters, type ListDefinition } from "./list-definition.ts";
import { ListFilterForm } from "./list-filter-form.tsx";
import { ListLink, ListNavigationProvider, useListNavigation } from "./list-navigation.tsx";
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

interface FilterFormState {
  readonly signature: string;
  readonly key: number;
  readonly applied: string | undefined;
  readonly clearing: boolean;
  readonly focusFirstField: boolean;
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
  const [form, setForm] = useState<FilterFormState>({
    signature,
    key: 0,
    applied: undefined,
    clearing: false,
    focusFirstField: false,
  });

  // The filter form keeps its controls, and the focus inside them, when the address changes because the form itself
  // applied these filters; any other change (Clear filters, Back, a link) remounts it so its fields show the new filters.
  if (form.signature !== signature) {
    const fromTheForm = form.applied === signature;
    setForm({
      signature,
      key: fromTheForm ? form.key : form.key + 1,
      applied: undefined,
      clearing: false,
      focusFirstField: form.clearing,
    });
  }

  const filtered = Object.keys(query.filters).length > 0;
  const link = (next: ListQuery) => listLink(basePath, next, definition);
  const clear = () => setForm((state) => ({ ...state, clearing: true }));

  return (
    <DataTable
      label={label}
      labelledBy={labelledBy}
      noun={noun}
      columns={columns}
      rows={rows}
      getRowId={getRowId}
      pending={pending}
      paging={{
        page,
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
            key={form.key}
            basePath={basePath}
            definition={definition}
            query={current}
            label={`Filter ${label.toLowerCase()}`}
            focusFirstField={form.focusFirstField}
            onApply={(applied) => setForm((state) => ({ ...state, applied }))}
            onClear={clear}
          >
            {filters}
          </ListFilterForm>
        )
      }
      empty={
        filtered ? (
          <Empty className="border" data-testid="list-no-matches">
            <EmptyHeader>
              <EmptyTitle>{noMatches}</EmptyTitle>
              <EmptyDescription>Change or clear the filters to see more.</EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <ListLink
                href={link(withFilters(current, {}))}
                disabled={false}
                aria-label="Clear filters"
                className={buttonVariants({ variant: "outline" })}
                onFollow={clear}
              >
                Clear filters
              </ListLink>
            </EmptyContent>
          </Empty>
        ) : (
          empty
        )
      }
    />
  );
}

export function ListTable<TRow extends DataTableRow>(props: ListTableProps<TRow>) {
  return (
    <ListNavigationProvider>
      <ListTableContent {...props} />
    </ListNavigationProvider>
  );
}
