import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

const jobDetailPlaceholders = ["schedule", "next", "last", "tries"];

export function JobsOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading the background jobs"
      pageTitle="Background jobs"
      className="grid gap-section"
      data-testid="jobs-loading"
    >
      <header className="grid gap-2">
        <Skeleton className="h-3 w-20" />
        <Skeleton className="h-9 w-full max-w-64" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-16" />
          <Skeleton className="h-4 w-full max-w-96" />
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 rounded-lg border p-4">
            <div className="grid gap-2">
              <Skeleton className="h-5 w-full max-w-56" />
              <Skeleton className="h-4 w-full max-w-md" />
            </div>
            <div className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
              {jobDetailPlaceholders.map((placeholder) => (
                <div key={placeholder} className="grid gap-1">
                  <Skeleton className="h-3 w-16" />
                  <Skeleton className="h-4 w-full max-w-40" />
                </div>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-16" />
          <Skeleton className="h-4 w-full max-w-md" />
        </CardHeader>
        <CardContent>
          <DataTableSkeleton columns={6} />
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
