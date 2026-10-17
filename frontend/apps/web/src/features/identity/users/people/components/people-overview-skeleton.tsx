import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

export function PeopleOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading people"
      pageTitle="People"
      className="grid gap-section"
      data-testid="people-loading"
    >
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-9 w-36" />
        </div>
        <div className="flex flex-wrap gap-2">
          <Skeleton className="h-8 w-40" />
          <Skeleton className="h-8 w-48" />
        </div>
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-28" />
          <Skeleton className="h-4 w-96 max-w-full" />
        </CardHeader>
        <CardContent>
          <DataTableSkeleton columns={6} />
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
