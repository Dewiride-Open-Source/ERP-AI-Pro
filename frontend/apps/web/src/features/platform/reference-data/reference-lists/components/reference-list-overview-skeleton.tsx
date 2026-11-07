import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

export function ReferenceListOverviewSkeleton({ title }: { title: string }) {
  return (
    <LoadingStatus
      label={`Loading ${title.toLowerCase()}`}
      pageTitle={title}
      className="grid gap-section"
      data-testid="reference-list-loading"
    >
      <header className="grid gap-2">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-full max-w-28" />
          <Skeleton className="h-9 w-full max-w-72" />
        </div>
        <Skeleton className="h-4 w-full max-w-md" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-full max-w-40" />
          <Skeleton className="h-4 w-full max-w-80" />
        </CardHeader>
        <CardContent>
          <DataTableSkeleton columns={3} />
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
