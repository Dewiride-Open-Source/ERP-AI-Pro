import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

const permissionPlaceholders = ["first", "second", "third", "fourth", "fifth", "sixth"];

export function PermissionsOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading roles and permissions"
      pageTitle="Roles and permissions"
      className="grid gap-section"
      data-testid="permissions-loading"
    >
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-9 w-72 max-w-full" />
          <Skeleton className="h-4 w-96 max-w-full" />
        </div>
        <Skeleton className="h-8 w-28" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-48" />
          <Skeleton className="h-4 w-96 max-w-full" />
        </CardHeader>
        <CardContent className="grid gap-3">
          <Skeleton className="h-8 w-full" />
          {permissionPlaceholders.map((placeholder) => (
            <Skeleton key={placeholder} className="h-12 w-full" />
          ))}
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
