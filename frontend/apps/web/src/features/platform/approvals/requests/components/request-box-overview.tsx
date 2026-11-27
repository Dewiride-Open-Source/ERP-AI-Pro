import type { DataTableFilterOption } from "@dewiride/erp-ui/components/data-table/filters/options-filter";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { CheckCheckIcon, InboxIcon, ListChecksIcon, SendIcon, type LucideIcon } from "lucide-react";
import { redirect } from "next/navigation";

import { ApiError } from "@/shared/api/problem-details";
import { requireSession } from "@/shared/auth/session";
import { listApiParameters } from "@/shared/lists/list-api";
import { listHref, readListQuery, withPage, type SearchParameters } from "@/shared/lists/list-query";

import { listApprovalKinds, type KindsRead } from "../../kinds/server/queries";
import { approvalsNavigation } from "../../nav";
import { requestLists, type RequestBox } from "../lists/requests.list";
import { readApprovalCounts, readApprovalRequests } from "../server/queries";

import { requestBoxPages } from "./approval-pages";
import { requestRows } from "./request-rows";
import { mineCountText, waitingCountText, type RequestCounts } from "./request-text";
import { RequestsList } from "./requests-list";

const boxIcons: Readonly<Record<RequestBox, LucideIcon>> = {
  waiting: InboxIcon,
  mine: SendIcon,
  decided: CheckCheckIcon,
  all: ListChecksIcon,
};

const countedBoxes: ReadonlySet<RequestBox> = new Set(["waiting", "mine"]);

export async function RequestBoxOverview({
  box,
  searchParameters,
}: {
  box: RequestBox;
  searchParameters: SearchParameters;
}) {
  const page = requestBoxPages[box];
  const definition = requestLists[box];
  const { query, canonical } = readListQuery(searchParameters, definition);
  if (!canonical) redirect(listHref(page.path, query, definition));

  const [session, read, kinds, counts] = await Promise.all([
    requireSession(),
    readApprovalRequests(box, listApiParameters(query, definition)),
    listApprovalKinds(),
    countedBoxes.has(box) ? readApprovalCounts() : Promise.resolve(undefined),
  ]);
  if (read.status === "read") {
    const totalPages = read.list.totalPages ?? 0;
    if (query.page > 1 && query.page > totalPages) {
      redirect(listHref(page.path, withPage(query, Math.max(1, totalPages)), definition));
    }
  }
  const viewer = session.status === "signed-in" ? session.person.id : undefined;
  const countText = countsText(box, counts);
  const headingId = `approval-${box}-heading`;
  const Icon = boxIcons[box];

  return (
    <div className="grid min-w-0 gap-section" data-testid="approval-box" data-box={box}>
      <header className="grid gap-2">
        <div>
          <p className="text-eyebrow text-primary uppercase">{approvalsNavigation.title}</p>
          <h1 className="text-title">{page.title}</h1>
        </div>
        <p className="max-w-prose text-body text-muted-foreground">{page.description}</p>
        {countText === undefined ? null : (
          <p className="max-w-prose text-body font-medium" data-testid="approval-counts">
            {countText}
          </p>
        )}
      </header>

      <Card data-testid="approval-requests-card">
        <CardHeader>
          <CardTitle>
            <h2 id={headingId} className="flex items-center gap-2">
              <Icon className="size-4 text-muted-foreground" aria-hidden />
              {page.listHeading}
            </h2>
          </CardTitle>
          <CardDescription>{page.listDescription}</CardDescription>
        </CardHeader>
        <CardContent>
          {read.status === "read" ? (
            <RequestsList
              box={box}
              query={query}
              page={{
                page: read.list.page ?? query.page,
                pageSize: read.list.pageSize ?? query.pageSize,
                totalCount: read.list.totalCount ?? 0,
                pageCount: read.list.totalPages ?? 0,
              }}
              requests={requestRows(read.list.items ?? [], viewer)}
              kindOptions={kindOptions(kinds)}
              labelledBy={headingId}
            />
          ) : (
            <div
              role="status"
              data-testid="approval-requests-unavailable"
              className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
            >
              <p className="font-medium">The requests are not available.</p>
              <p className="mt-1 text-sm text-muted-foreground">{describeFailure(read.reason)}</p>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function countsText(box: RequestBox, counts: RequestCounts | undefined): string | undefined {
  if (counts === undefined) return undefined;
  return box === "waiting" ? waitingCountText(counts) : mineCountText(counts);
}

function kindOptions(kinds: KindsRead): readonly DataTableFilterOption[] {
  return kinds.status === "read" ? kinds.kinds.map((kind) => ({ value: kind.key, label: kind.title })) : [];
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}
