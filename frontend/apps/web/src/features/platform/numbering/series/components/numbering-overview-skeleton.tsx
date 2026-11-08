import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

import { numberingNavigation } from "../../nav";

const documentPlaceholders = ["first", "second", "third", "fourth"];

export function NumberingOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading the document numbering"
      pageTitle={numberingNavigation.title}
      className="grid gap-section"
      data-testid="numbering-loading"
    >
      <header className="grid gap-2">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-full max-w-20" />
          <Skeleton className="h-9 w-full max-w-72" />
        </div>
        <Skeleton className="h-4 w-full max-w-prose" />
        <Skeleton className="h-4 w-full max-w-md" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-full max-w-48" />
          <Skeleton className="h-4 w-full max-w-md" />
        </CardHeader>
        <CardContent className="grid gap-4 xl:grid-cols-2">
          {documentPlaceholders.map((placeholder) => (
            <div key={placeholder} className="grid gap-3 rounded-lg border p-4">
              <div className="flex items-start justify-between gap-4">
                <div className="grid w-full gap-2">
                  <Skeleton className="h-5 w-full max-w-40" />
                  <Skeleton className="h-4 w-full max-w-72" />
                </div>
                <Skeleton className="h-7 w-20 shrink-0" />
              </div>
              <Skeleton className="h-4 w-full max-w-96" />
            </div>
          ))}
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
