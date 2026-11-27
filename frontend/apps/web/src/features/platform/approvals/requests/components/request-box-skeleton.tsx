import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

import { requestLists, type RequestBox } from "../lists/requests.list";

import { requestBoxPages } from "./approval-pages";

export function RequestBoxOverviewSkeleton({ box }: { box: RequestBox }) {
  const page = requestBoxPages[box];

  return (
    <LoadingStatus
      label={page.loadingLabel}
      pageTitle={page.title}
      className="grid gap-section"
      data-testid="approvals-loading"
    >
      <header className="grid gap-2">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-full max-w-20" />
          <Skeleton className="h-9 w-full max-w-64" />
        </div>
        <Skeleton className="h-4 w-full max-w-md" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-full max-w-56" />
          <Skeleton className="h-4 w-full max-w-96" />
        </CardHeader>
        <CardContent>
          <DataTableSkeleton columns={requestLists[box].columns.length} />
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
