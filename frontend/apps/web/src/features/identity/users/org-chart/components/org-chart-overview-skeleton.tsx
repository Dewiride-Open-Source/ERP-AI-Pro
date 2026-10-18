import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

const personPlaceholders = ["first", "second", "third", "fourth", "fifth"];

export function OrgChartOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading the organisation chart"
      pageTitle="Organisation chart"
      className="grid gap-section"
      data-testid="org-chart-loading"
    >
      <header className="grid gap-2">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-9 w-full max-w-72" />
        </div>
        <Skeleton className="h-4 w-full max-w-md" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-full max-w-36" />
          <Skeleton className="h-4 w-full max-w-lg" />
        </CardHeader>
        <CardContent className="grid gap-3">
          <div className="flex flex-wrap items-end gap-3">
            <Skeleton className="h-8 w-full sm:w-80" />
            <Skeleton className="h-8 w-16" />
          </div>
          <Skeleton className="h-4 w-full max-w-48" />
          {personPlaceholders.map((placeholder) => (
            <Skeleton key={placeholder} className="h-16 w-full" />
          ))}
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
