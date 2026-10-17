import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

const changePlaceholders = ["first", "second", "third", "fourth"];

export function AuditTrailOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading the audit trail"
      pageTitle="Audit trail"
      className="grid gap-section"
      data-testid="audit-loading"
    >
      <header className="grid gap-2">
        <Skeleton className="h-3 w-20" />
        <Skeleton className="h-9 w-48" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-28" />
          <Skeleton className="h-4 w-96 max-w-full" />
        </CardHeader>
        <CardContent className="grid gap-6">
          <Skeleton className="h-16 w-full rounded-xl" />
          <div className="grid gap-5">
            <Skeleton className="h-5 w-56 max-w-full" />
            {changePlaceholders.map((placeholder) => (
              <div key={placeholder} className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-3">
                <Skeleton className="mt-0.5 size-3 rounded-full" />
                <div className="grid gap-2">
                  <Skeleton className="h-3 w-20" />
                  <Skeleton className="h-4 w-full max-w-md" />
                  <Skeleton className="h-4 w-64 max-w-full" />
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
