"use client";

import type { DataTableNoun } from "@dewiride/erp-ui/components/data-table/data-table";
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

import { useFilterFormState } from "@/shared/lists/filter-state/use-filter-form-state";
import { ListLink, ListNavigationProvider, useListNavigation } from "@/shared/lists/list-navigation";

import type { ReferenceListPath } from "./reference-list-pages";
import { ReferenceListTable } from "./reference-list-table";
import {
  readSearch,
  referenceListHref,
  referenceListParameters,
  searchMaxLength,
  type ReferenceListColumn,
  type ReferenceListPage,
  type ReferenceListRow,
  type ReferenceListView,
} from "./reference-list-view";

export interface ReferenceListBrowserProps {
  readonly path: ReferenceListPath;
  readonly view: ReferenceListView;
  readonly page: ReferenceListPage;
  readonly label: string;
  readonly labelledBy: string;
  readonly noun: DataTableNoun;
  readonly searchExample?: string | undefined;
  readonly columns: readonly ReferenceListColumn[];
  readonly rows: readonly ReferenceListRow[];
}

const pageLinkClass = cn(
  buttonVariants({ variant: "outline", size: "icon-sm" }),
  "aria-disabled:pointer-events-none aria-disabled:opacity-50",
);

export function ReferenceListBrowser(props: ReferenceListBrowserProps) {
  return (
    <ListNavigationProvider>
      <ReferenceListContent {...props} />
    </ListNavigationProvider>
  );
}

// The search form stays in place when it changed the address itself, keeping the focus a person gave it, and starts again
// for any other change (Clear the search, Back, a link), so its field shows the search of the address.
function ReferenceListContent({
  path,
  view,
  page,
  label,
  labelledBy,
  noun,
  searchExample,
  columns,
  rows,
}: ReferenceListBrowserProps) {
  const hydrated = useHydrated();
  const { pending } = useListNavigation();
  const searchForm = useFilterFormState(view.search ?? "");

  return (
    <div
      className="grid min-w-0 grid-cols-1 gap-4"
      data-hydrating={hydrated ? undefined : ""}
      data-testid="reference-list"
    >
      <ReferenceListSearch
        key={searchForm.key}
        path={path}
        view={view}
        label={`Search ${label.toLowerCase()}`}
        searchExample={searchExample}
        focusFirstField={searchForm.focusFirstField}
        onApply={searchForm.onApply}
        onClear={searchForm.onClear}
      />
      {rows.length > 0 ? (
        <ReferenceListTable labelledBy={labelledBy} columns={columns} rows={rows} pending={pending} />
      ) : view.search === undefined ? (
        <Empty className="border" data-testid="reference-list-empty">
          <EmptyHeader>
            <EmptyTitle>{`No ${noun.other} are listed.`}</EmptyTitle>
          </EmptyHeader>
        </Empty>
      ) : (
        <Empty className="border" data-testid="reference-list-no-matches">
          <EmptyHeader>
            <EmptyTitle>{`No ${noun.other} match this search.`}</EmptyTitle>
            <EmptyDescription>Search by another name or code.</EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <ListLink
              href={referenceListHref(path, { search: undefined, page: 1 })}
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
        {page.pageCount > 1 ? <ReferenceListPages path={path} view={view} page={page} label={label} /> : null}
      </div>
    </div>
  );
}

function ReferenceListSearch({
  path,
  view,
  label,
  searchExample,
  focusFirstField,
  onApply,
  onClear,
}: {
  path: ReferenceListPath;
  view: ReferenceListView;
  label: string;
  searchExample: string | undefined;
  focusFirstField: boolean;
  onApply: (signature: string) => void;
  onClear: () => void;
}) {
  const { navigate } = useListNavigation();
  const formRef = useRef<HTMLFormElement>(null);

  useEffect(() => {
    if (focusFirstField) formRef.current?.querySelector<HTMLElement>("input")?.focus();
  }, [focusFirstField]);

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const typed = new FormData(event.currentTarget).get(referenceListParameters.search);
    const search = readSearch(typeof typed === "string" ? typed : "");
    onApply(search ?? "");
    navigate(referenceListHref(path, { search, page: 1 }));
  }

  return (
    <Form
      ref={formRef}
      action={path}
      role="search"
      aria-label={label}
      noValidate
      onSubmit={submit}
      className="flex flex-wrap items-end gap-3"
    >
      <DataTableTextFilter
        name={referenceListParameters.search}
        label="Name or code"
        defaultValue={view.search ?? ""}
        placeholder={searchExample}
        maxLength={searchMaxLength}
        className="sm:w-72"
      />
      <div className="flex items-center gap-2">
        <Button type="submit" variant="secondary">
          Search
        </Button>
        {view.search === undefined ? null : (
          <ListLink
            href={referenceListHref(path, { search: undefined, page: 1 })}
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

function ReferenceListPages({
  path,
  view,
  page,
  label,
}: {
  path: ReferenceListPath;
  view: ReferenceListView;
  page: ReferenceListPage;
  label: string;
}) {
  const href = (target: number) => referenceListHref(path, { ...view, page: target });
  const first = page.page <= 1;
  const last = page.page >= page.pageCount;

  return (
    <nav
      aria-label={`${label}, pages`}
      className="flex items-center gap-1"
      data-testid="reference-list-pages"
    >
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
      <p className="px-2 whitespace-nowrap" data-testid="reference-list-page">
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
