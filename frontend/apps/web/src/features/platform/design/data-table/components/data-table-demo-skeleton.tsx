import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

export function DataTableDemoSkeleton() {
  return (
    <div
      className="grid gap-section"
      role="status"
      aria-label="Loading purchase bills"
      data-testid="data-table-demo-loading"
    >
      <header className="grid gap-2">
        <Skeleton className="h-3 w-20" />
        <Skeleton className="h-9 w-48" />
        <Skeleton className="h-4 w-96 max-w-full" />
      </header>
      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-48" />
          <Skeleton className="h-4 w-80 max-w-full" />
        </CardHeader>
        <CardContent>
          <DataTableSkeleton columns={6} rows={10} />
        </CardContent>
      </Card>
    </div>
  );
}
