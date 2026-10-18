import { buttonVariants } from "@dewiride/erp-ui/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { NetworkIcon } from "lucide-react";
import Link from "next/link";
import { redirect, unstable_rethrow } from "next/navigation";
import type { ReactNode } from "react";

import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { istDateKey } from "@/shared/format/dates";

import { listDepartments } from "../../departments/server/queries";
import { orgChartNavigation } from "../../nav";
import { getOrgChart } from "../server/queries";

import { shownChart } from "./org-chart";
import { OrgChartBrowser } from "./org-chart-browser";
import { chartHeading, otherDateRefusal } from "./org-chart-text";
import { orgChartHref, readOrgChartView, type OrgChartView, type SearchParameters } from "./org-chart-view";

const chartHeadingId = "org-chart-heading";

export async function OrgChartOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const today = istDateKey(new Date());
  const { view, canonical } = readOrgChartView(searchParameters, today);
  if (!canonical) redirect(orgChartHref(view));

  const session = await requireSession();
  const choosesDate = mayShow(session, "identity.users.positions.read");
  if (view.on !== undefined && !choosesDate) {
    return (
      <ChartFrame on={view.on}>
        <OtherDateRefused view={view} />
      </ChartFrame>
    );
  }

  const [chart, departments] = await Promise.allSettled([getOrgChart(view.on), listDepartments()]);
  for (const result of [chart, departments]) {
    if (result.status === "rejected") unstable_rethrow(result.reason);
  }
  if (chart.status === "rejected") {
    return (
      <ChartFrame on={view.on}>
        {isOtherDateRefusal(chart.reason, view) ? (
          <OtherDateRefused view={view} />
        ) : (
          <Unavailable reason={describeFailure(chart.reason)} />
        )}
      </ChartFrame>
    );
  }

  const shown = shownChart(
    view,
    chart.value,
    departments.status === "fulfilled" ? departments.value : undefined,
  );
  if (shown.page.page !== view.page) redirect(orgChartHref({ ...view, page: shown.page.page }));

  return (
    <ChartFrame on={view.on}>
      <OrgChartBrowser
        view={view}
        today={today}
        choosesDate={choosesDate}
        departments={shown.departments}
        departmentName={shown.departmentName}
        chart={{ ...shown.page, peopleCount: shown.peopleCount }}
        linksPeople={mayShow(session, "identity.users.people.read")}
        headingId={chartHeadingId}
      />
    </ChartFrame>
  );
}

function isOtherDateRefusal(reason: unknown, view: OrgChartView): boolean {
  return view.on !== undefined && reason instanceof ApiError && reason.status === 403;
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function ChartFrame({ on, children }: { on: string | undefined; children: ReactNode }) {
  return (
    <div className="grid min-w-0 gap-section">
      <header className="grid gap-2">
        <div>
          <p className="text-eyebrow text-primary uppercase">{orgChartNavigation.area.title}</p>
          <h1 className="text-title">{orgChartNavigation.title}</h1>
        </div>
        <p className="max-w-prose text-body text-muted-foreground">
          Who works in each department and who reports to whom.
        </p>
      </header>

      <Card data-testid="org-chart-card">
        <CardHeader>
          <CardTitle>
            <h2 id={chartHeadingId} className="flex items-center gap-2">
              <NetworkIcon className="size-4 text-muted-foreground" aria-hidden />
              {chartHeading(on)}
            </h2>
          </CardTitle>
          <CardDescription>
            Everyone is listed under the person they report to, sorted by name. Open a person to see who
            reports to them.
          </CardDescription>
        </CardHeader>
        <CardContent>{children}</CardContent>
      </Card>
    </div>
  );
}

function OtherDateRefused({ view }: { view: OrgChartView }) {
  return (
    <div
      role="status"
      data-testid="org-chart-other-date-refused"
      className="grid justify-items-start gap-3 rounded-lg border bg-muted/40 p-4"
    >
      <div>
        <p className="font-medium">{otherDateRefusal.title}</p>
        <p className="mt-1 text-sm text-muted-foreground">{otherDateRefusal.description}</p>
      </div>
      <Link
        href={orgChartHref({ ...view, on: undefined, page: 1 })}
        className={buttonVariants({ variant: "outline" })}
      >
        {otherDateRefusal.action}
      </Link>
    </div>
  );
}

function Unavailable({ reason }: { reason: string }) {
  return (
    <div
      role="status"
      data-testid="org-chart-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">The organisation chart is not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
    </div>
  );
}
