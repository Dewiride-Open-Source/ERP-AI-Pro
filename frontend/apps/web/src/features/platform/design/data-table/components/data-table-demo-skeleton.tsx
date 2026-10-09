import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

export function DataTableDemoSkeleton() {
  return (
    <LoadingStatus
      label="Loading purchase bills"
      pageTitle="Data table"
      className="grid gap-section"
      data-testid="data-table-demo-loading"
    >
      <header className="grid gap-2">
        <Skeleton className="h-3 w-20" />
        <Skeleton className="h-9 w-48" />
        <Skeleton className="h-4 w-full max-w-96" />
      </header>
      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-48" />
          <Skeleton className="h-4 w-full max-w-80" />
        </CardHeader>
        <CardContent>
          <DataTableSkeleton columns={6} rows={10} />
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
