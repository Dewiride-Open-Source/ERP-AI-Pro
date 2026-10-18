import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

const designationPlaceholders = ["first", "second", "third", "fourth", "fifth", "sixth"];

export function DesignationsOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading designations"
      pageTitle="Designations"
      className="grid gap-section"
      data-testid="designations-loading"
    >
      <header className="grid gap-2">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-9 w-full max-w-56" />
        </div>
        <Skeleton className="h-4 w-full max-w-xl" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-full max-w-40" />
          <Skeleton className="h-4 w-full max-w-28" />
        </CardHeader>
        <CardContent className="grid gap-3">
          <Skeleton className="h-7 w-36" />
          {designationPlaceholders.map((placeholder) => (
            <Skeleton key={placeholder} className="h-12 w-full" />
          ))}
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
