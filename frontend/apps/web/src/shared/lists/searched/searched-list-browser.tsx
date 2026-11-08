"use client";

import type { DataTableNoun, DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import { DataTableStatus } from "@dewiride/erp-ui/components/data-table/data-table-pagination";
import { DataTableTextFilter } from "@dewiride/erp-ui/components/data-table/filters/text-filter";
import { Button, buttonVariants } from "@dewiride/erp-ui/components/ui/button";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from "@dewiride/erp-ui/components/ui/empty";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { ChevronLeftIcon, ChevronRightIcon, ChevronsLeftIcon, ChevronsRightIcon } from "lucide-react";
import Form from "next/form";
import { useEffect, useRef, type FormEvent } from "react";

import { useFilterFormState } from "../filter-state/use-filter-form-state.ts";
import { ListLink, ListNavigationProvider, useListNavigation } from "../list-navigation.tsx";

import { SearchedListTable } from "./searched-list-table.tsx";
import {
  readSearch,
  searchedListHref,
  searchedListParameters,
  searchMaxLength,
  type SearchedListAddress,
  type SearchedListColumn,
  type SearchedListLayout,
  type SearchedListRow,
  type SearchedListView,
} from "./searched-list-view.ts";

export interface SearchedListSearch {
  readonly label: string;
  readonly advice: string;
  readonly example?: string | undefined;
}

export interface SearchedListBrowserProps {
  readonly address: SearchedListAddress;
  readonly view: SearchedListView;
  readonly page: DataTablePage;
  readonly label: string;
  readonly labelledBy: string;
  readonly noun: DataTableNoun;
  readonly search: SearchedListSearch;
  readonly columns: readonly SearchedListColumn[];
  readonly rows: readonly SearchedListRow[];
  readonly layout?: SearchedListLayout | undefined;
  readonly outerNavigation?: boolean | undefined;
}

type SearchedListContentProps = Omit<SearchedListBrowserProps, "outerNavigation">;

const pageLinkClass = cn(
  buttonVariants({ variant: "outline", size: "icon-sm" }),
  "aria-disabled:pointer-events-none aria-disabled:opacity-50",
);

export function SearchedListBrowser({ outerNavigation = false, ...props }: SearchedListBrowserProps) {
  if (outerNavigation) return <SearchedListContent {...props} />;

  return (
    <ListNavigationProvider>
      <SearchedListContent {...props} />
    </ListNavigationProvider>
  );
}

// The search form stays in place when it changed the address itself, keeping the focus a person gave it, and starts again
// for any other change (Clear the search, Back, a link), so its field shows the search of the address.
function SearchedListContent({
  address,
  view,
  page,
  label,
  labelledBy,
  noun,
  search,
  columns,
  rows,
  layout,
}: SearchedListContentProps) {
  const hydrated = useHydrated();
  const { pending } = useListNavigation();
  const searchForm = useFilterFormState(view.search ?? "");

  return (
    <div
      className="grid min-w-0 grid-cols-1 gap-4"
      data-hydrating={hydrated ? undefined : ""}
      data-testid="searched-list"
    >
      <SearchedListSearchForm
        key={searchForm.key}
        address={address}
        view={view}
        label={`Search ${label.toLowerCase()}`}
        search={search}
        focusFirstField={searchForm.focusFirstField}
        onApply={searchForm.onApply}
        onClear={searchForm.onClear}
      />
      {rows.length > 0 ? (
        <SearchedListTable
          labelledBy={labelledBy}
          columns={columns}
          rows={rows}
          pending={pending}
          layout={layout}
        />
      ) : view.search === undefined ? (
        <Empty className="border" data-testid="searched-list-empty">
          <EmptyHeader>
            <EmptyTitle>{`No ${noun.other} are listed.`}</EmptyTitle>
          </EmptyHeader>
        </Empty>
      ) : (
        <Empty className="border" data-testid="searched-list-no-matches">
          <EmptyHeader>
            <EmptyTitle>{`No ${noun.other} match this search.`}</EmptyTitle>
            <EmptyDescription>{search.advice}</EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <ListLink
              href={searchedListHref(address, { search: undefined, page: 1 })}
              disabled={false}
              aria-label="Clear the search"
              className={buttonVariants({ variant: "outline" })}
              onFollow={searchForm.onClear}
            >
              Clear the search
            </ListLink>
          </EmptyContent>
        </Empty>
      )}
      <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 text-sm">
        <DataTableStatus page={page} noun={noun} pending={pending} />
        {page.pageCount > 1 ? (
          <SearchedListPages address={address} view={view} page={page} label={label} />
        ) : null}
      </div>
    </div>
  );
}

function SearchedListSearchForm({
  address,
  view,
  label,
  search,
  focusFirstField,
  onApply,
  onClear,
}: {
  address: SearchedListAddress;
  view: SearchedListView;
  label: string;
  search: SearchedListSearch;
  focusFirstField: boolean;
  onApply: (signature: string) => void;
  onClear: () => void;
}) {
  const { navigate } = useListNavigation();
  const formRef = useRef<HTMLFormElement>(null);

  useEffect(() => {
    if (focusFirstField) formRef.current?.querySelector<HTMLElement>("input:not([type=hidden])")?.focus();
  }, [focusFirstField]);

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const typed = new FormData(event.currentTarget).get(searchedListParameters.search);
    const searched = readSearch(typeof typed === "string" ? typed : "");
    onApply(searched ?? "");
    navigate(searchedListHref(address, { search: searched, page: 1 }));
  }

  return (
    <Form
      ref={formRef}
      action={address.path}
      role="search"
      aria-label={label}
      noValidate
      onSubmit={submit}
      className="flex flex-wrap items-end gap-3"
    >
      {Object.entries(address.keep ?? {}).map(([name, value]) => (
        <input key={name} type="hidden" name={name} value={value} />
      ))}
      <DataTableTextFilter
        name={searchedListParameters.search}
        label={search.label}
        defaultValue={view.search ?? ""}
        placeholder={search.example}
        maxLength={searchMaxLength}
        className="sm:w-72"
      />
      <div className="flex items-center gap-2">
        <Button type="submit" variant="secondary">
          Search
        </Button>
        {view.search === undefined ? null : (
          <ListLink
            href={searchedListHref(address, { search: undefined, page: 1 })}
            disabled={false}
            aria-label="Clear the search"
            className={buttonVariants({ variant: "ghost" })}
            onFollow={onClear}
          >
            Clear the search
          </ListLink>
        )}
      </div>
    </Form>
  );
}

function SearchedListPages({
  address,
  view,
  page,
  label,
}: {
  address: SearchedListAddress;
  view: SearchedListView;
  page: DataTablePage;
  label: string;
}) {
  const href = (target: number) => searchedListHref(address, { ...view, page: target });
  const first = page.page <= 1;
  const last = page.page >= page.pageCount;

  return (
    <nav aria-label={`${label}, pages`} className="flex items-center gap-1" data-testid="searched-list-pages">
      <ListLink href={href(1)} disabled={first} aria-label="First page" className={pageLinkClass}>
        <ChevronsLeftIcon aria-hidden />
      </ListLink>
      <ListLink
        href={href(Math.max(1, page.page - 1))}
        disabled={first}
        aria-label="Previous page"
        className={pageLinkClass}
      >
        <ChevronLeftIcon aria-hidden />
      </ListLink>
      <p className="px-2 whitespace-nowrap" data-testid="searched-list-page">
        Page {page.page} of {page.pageCount}
      </p>
      <ListLink
        href={href(Math.min(page.pageCount, page.page + 1))}
        disabled={last}
        aria-label="Next page"
        className={pageLinkClass}
      >
        <ChevronRightIcon aria-hidden />
      </ListLink>
      <ListLink href={href(page.pageCount)} disabled={last} aria-label="Last page" className={pageLinkClass}>
        <ChevronsRightIcon aria-hidden />
      </ListLink>
    </nav>
  );
}
