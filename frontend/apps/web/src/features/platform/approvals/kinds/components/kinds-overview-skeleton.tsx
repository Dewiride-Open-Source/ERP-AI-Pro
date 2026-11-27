import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

import { kindsPage } from "../../requests/components/approval-pages";

const kindPlaceholders = ["first", "second", "third"];

export function KindsOverviewSkeleton() {
  return (
    <LoadingStatus
      label={kindsPage.loadingLabel}
      pageTitle={kindsPage.title}
      className="grid gap-section"
      data-testid="approval-kinds-loading"
    >
      <header className="grid gap-2">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-full max-w-20" />
          <Skeleton className="h-9 w-full max-w-64" />
        </div>
        <Skeleton className="h-4 w-full max-w-md" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-full max-w-64" />
          <Skeleton className="h-4 w-full max-w-xl" />
        </CardHeader>
        <CardContent className="grid gap-4">
          {kindPlaceholders.map((placeholder) => (
            <div key={placeholder} className="grid gap-3 rounded-lg border p-4">
              <div className="grid gap-2">
                <Skeleton className="h-5 w-full max-w-48" />
                <Skeleton className="h-4 w-full max-w-md" />
              </div>
              <Skeleton className="h-4 w-full max-w-72" />
              <Skeleton className="h-14 w-full" />
            </div>
          ))}
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
