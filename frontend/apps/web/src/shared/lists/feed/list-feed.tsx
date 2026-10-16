"use client";

import type { DataTableNoun, DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import {
  DataTablePagination,
  DataTableStatus,
} from "@dewiride/erp-ui/components/data-table/data-table-pagination";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import type { Route } from "next";
import type { ReactNode } from "react";

import { useFilterFormState } from "../filter-state/use-filter-form-state.ts";
import type { ListDefinition } from "../list-definition.ts";
import { ListFilterForm, ListNoMatches } from "../list-filter-form.tsx";
import { ListLink, ListNavigationProvider, useListNavigation } from "../list-navigation.tsx";
import {
  filterSignature,
  listLink,
  withFilters,
  withPage,
  withPageSize,
  type ListQuery,
} from "../list-query.ts";

export interface ListFeedProps {
  readonly basePath: Route;
  readonly definition: ListDefinition;
  readonly query: ListQuery;
  readonly page: DataTablePage;
  readonly label: string;
  readonly noun: DataTableNoun;
  readonly itemCount: number;
  readonly empty: ReactNode;
  readonly noMatches: string;
  readonly filters?: ReactNode;
  readonly children: ReactNode;
}

function perPageLabel(noun: DataTableNoun): string {
  return `${noun.other.charAt(0).toUpperCase()}${noun.other.slice(1)} per page`;
}

function ListFeedContent({
  basePath,
  definition,
  query,
  page,
  label,
  noun,
  itemCount,
  empty,
  noMatches,
  filters,
  children,
}: ListFeedProps) {
  const interactive = useHydrated();
  const { navigate, pending } = useListNavigation();
  const filterForm = useFilterFormState(filterSignature(query.filters, definition));
  const filtered = Object.keys(query.filters).length > 0;
  const link = (next: ListQuery) => listLink(basePath, next, definition);

  return (
    <div data-slot="list-feed" data-hydrating={interactive ? undefined : ""} className="grid min-w-0 gap-4">
      {filters === undefined ? null : (
        <ListFilterForm
          key={filterForm.key}
          basePath={basePath}
          definition={definition}
          query={query}
          label={`Filter ${label.toLowerCase()}`}
          focusFirstField={filterForm.focusFirstField}
          onApply={filterForm.onApply}
          onClear={filterForm.onClear}
        >
          {filters}
        </ListFilterForm>
      )}

      {itemCount > 0 ? (
        <div
          aria-busy={pending || undefined}
          data-pending={pending || undefined}
          data-testid="list-feed-items"
          className="min-w-0 transition-opacity duration-(--motion-duration-fast) data-pending:opacity-60"
        >
          {children}
        </div>
      ) : filtered ? (
        <ListNoMatches message={noMatches} href={link(withFilters(query, {}))} onClear={filterForm.onClear} />
      ) : (
        empty
      )}

      <DataTablePagination
        label={label}
        sizeLabel={perPageLabel(noun)}
        page={page}
        pageSizes={definition.pageSizes}
        status={<DataTableStatus page={page} noun={noun} pending={pending} />}
        pageHref={(target) => link(withPage(query, target))}
        onPageSizeChange={(pageSize) => navigate(link(withPageSize(query, pageSize)))}
        interactive={interactive}
        linkComponent={ListLink}
      />
    </div>
  );
}

export function ListFeed(props: ListFeedProps) {
  return (
    <ListNavigationProvider>
      <ListFeedContent {...props} />
    </ListNavigationProvider>
  );
}
