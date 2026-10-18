"use client";

import { buttonVariants } from "@dewiride/erp-ui/components/ui/button";
import { Empty, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { ChevronLeftIcon, ChevronRightIcon, ChevronsLeftIcon, ChevronsRightIcon } from "lucide-react";

import { useFilterFormState } from "@/shared/lists/filter-state/use-filter-form-state";
import { ListLink, ListNavigationProvider, useListNavigation } from "@/shared/lists/list-navigation";

import type { ChartNode, DepartmentChoices } from "./org-chart";
import { OrgChartFilters } from "./org-chart-filters";
import { emptyChartMessage, pageLabel, peopleOnChartLabel } from "./org-chart-text";
import { OrgChartTree } from "./org-chart-tree";
import { filterSignature, orgChartHref, type OrgChartView } from "./org-chart-view";

export interface ShownChart {
  readonly roots: readonly ChartNode[];
  readonly page: number;
  readonly pageCount: number;
  readonly peopleCount: number;
}

type OrgChartBrowserProps = {
  readonly view: OrgChartView;
  readonly today: string;
  readonly choosesDate: boolean;
  readonly departments: DepartmentChoices;
  readonly departmentName: string | undefined;
  readonly chart: ShownChart;
  readonly linksPeople: boolean;
  readonly headingId: string;
};

const pageLinkClass = cn(
  buttonVariants({ variant: "outline", size: "icon-sm" }),
  "aria-disabled:pointer-events-none aria-disabled:opacity-50",
);

export function OrgChartBrowser(props: OrgChartBrowserProps) {
  return (
    <ListNavigationProvider>
      <OrgChartContent {...props} />
    </ListNavigationProvider>
  );
}

// The tree starts again for every view, so each page opens folded the same way, while the filters and the page links stay
// in place, keeping the focus a person gave them.
function OrgChartContent({
  view,
  today,
  choosesDate,
  departments,
  departmentName,
  chart,
  linksPeople,
  headingId,
}: OrgChartBrowserProps) {
  const { pending } = useListNavigation();
  const filterForm = useFilterFormState(filterSignature(view));

  return (
    <div className="grid min-w-0 gap-4" data-testid="org-chart">
      <OrgChartFilters
        key={filterForm.key}
        view={view}
        today={today}
        choosesDate={choosesDate}
        departments={departments}
        focusFirstField={filterForm.focusFirstField}
        onApply={filterForm.onApply}
        onClear={filterForm.onClear}
      />
      <p role="status" className="text-sm text-muted-foreground" data-testid="org-chart-status">
        {pending ? "Loading…" : peopleOnChartLabel(chart.peopleCount)}
      </p>
      {chart.roots.length === 0 ? (
        <Empty className="border" data-testid="org-chart-empty">
          <EmptyHeader>
            <EmptyTitle>{emptyChartMessage(departmentName, view.department !== undefined)}</EmptyTitle>
          </EmptyHeader>
        </Empty>
      ) : (
        <OrgChartTree
          key={orgChartHref(view)}
          roots={chart.roots}
          linksPeople={linksPeople}
          departmentName={departmentName}
          headingId={headingId}
          pending={pending}
        />
      )}
      {chart.pageCount > 1 ? (
        <OrgChartPages view={view} page={chart.page} pageCount={chart.pageCount} />
      ) : null}
    </div>
  );
}

function OrgChartPages({ view, page, pageCount }: { view: OrgChartView; page: number; pageCount: number }) {
  const href = (target: number) => orgChartHref({ ...view, page: target });

  return (
    <nav
      aria-label="Organisation chart, pages"
      className="flex flex-wrap items-center gap-1 text-sm"
      data-testid="org-chart-pages"
    >
      <ListLink href={href(1)} disabled={page <= 1} aria-label="First page" className={pageLinkClass}>
        <ChevronsLeftIcon aria-hidden />
      </ListLink>
      <ListLink
        href={href(Math.max(1, page - 1))}
        disabled={page <= 1}
        aria-label="Previous page"
        className={pageLinkClass}
      >
        <ChevronLeftIcon aria-hidden />
      </ListLink>
      <p className="px-2 whitespace-nowrap" data-testid="org-chart-page">
        {pageLabel(page, pageCount)}
      </p>
      <ListLink
        href={href(Math.min(pageCount, page + 1))}
        disabled={page >= pageCount}
        aria-label="Next page"
        className={pageLinkClass}
      >
        <ChevronRightIcon aria-hidden />
      </ListLink>
      <ListLink
        href={href(pageCount)}
        disabled={page >= pageCount}
        aria-label="Last page"
        className={pageLinkClass}
      >
        <ChevronsRightIcon aria-hidden />
      </ListLink>
    </nav>
  );
}
