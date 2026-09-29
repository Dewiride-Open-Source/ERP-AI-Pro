import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

export function AttachmentsOverviewSkeleton() {
  return (
    <div
      className="grid gap-section"
      role="status"
      aria-label="Loading attachments"
      data-testid="attachments-loading"
    >
      <header className="grid gap-2">
        <Skeleton className="h-3 w-20" />
        <Skeleton className="h-9 w-48" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-20" />
          <Skeleton className="h-4 w-64 max-w-full" />
        </CardHeader>
        <CardContent>
          <Skeleton className="h-40 w-full rounded-xl" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-28" />
          <Skeleton className="h-4 w-80 max-w-full" />
        </CardHeader>
        <CardContent>
          <DataTableSkeleton columns={5} />
        </CardContent>
      </Card>
    </div>
  );
}
