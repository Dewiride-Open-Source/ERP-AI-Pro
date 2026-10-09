import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

const bucketRows = ["not-due", "thirty", "sixty", "ninety", "older"];

export function SlowReportSkeleton() {
  return (
    <LoadingStatus
      label="Loading the report"
      pageTitle="Receivables ageing (example)"
      className="grid gap-section"
      data-testid="slow-report-loading"
    >
      <header className="grid gap-2">
        <Skeleton className="h-3 w-20" />
        <Skeleton className="h-9 w-full max-w-80" />
        <Skeleton className="h-4 w-full max-w-96" />
      </header>
      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-48" />
          <Skeleton className="h-4 w-72 max-w-full" />
        </CardHeader>
        <CardContent className="grid gap-3">
          {bucketRows.map((row) => (
            <div key={row} className="flex justify-between gap-4">
              <Skeleton className="h-5 w-48" />
              <Skeleton className="h-5 w-24" />
            </div>
          ))}
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
