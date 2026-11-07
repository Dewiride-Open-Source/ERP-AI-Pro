import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

import { parameterSetsNavigation } from "../../nav";

const placeholderCards = ["first", "second", "third", "fourth"];

export function ParameterSetsOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading statutory parameters"
      pageTitle={parameterSetsNavigation.title}
      className="grid gap-section"
      data-testid="parameter-sets-loading"
    >
      <header className="grid gap-2">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-full max-w-24" />
          <Skeleton className="h-9 w-full max-w-72" />
        </div>
        <Skeleton className="h-4 w-full max-w-prose" />
      </header>

      <div className="grid gap-4 xl:grid-cols-2">
        {placeholderCards.map((card) => (
          <Card key={card}>
            <CardHeader className="gap-2">
              <Skeleton className="h-5 w-full max-w-80" />
              <Skeleton className="h-5 w-full max-w-28" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-full max-w-md" />
            </CardHeader>
            <CardContent className="grid gap-3">
              <Skeleton className="h-4 w-full max-w-60" />
              <Skeleton className="h-4 w-full max-w-40" />
            </CardContent>
          </Card>
        ))}
      </div>
    </LoadingStatus>
  );
}
