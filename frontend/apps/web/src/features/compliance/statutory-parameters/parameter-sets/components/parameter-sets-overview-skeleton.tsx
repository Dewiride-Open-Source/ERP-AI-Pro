import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

import { parameterSetsNavigation } from "../../nav";

const placeholderRows = ["first", "second", "third", "fourth", "fifth"];

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

      <div className="grid gap-4">
        <div className="flex flex-wrap items-end gap-3">
          <Skeleton className="h-9 w-full sm:w-72" />
          <Skeleton className="h-9 w-20" />
        </div>
        <div className="grid gap-2">
          {placeholderRows.map((row) => (
            <div key={row} className="grid gap-2 rounded-lg border p-3">
              <Skeleton className="h-5 w-full max-w-80" />
              <Skeleton className="h-4 w-full max-w-md" />
            </div>
          ))}
        </div>
      </div>
    </LoadingStatus>
  );
}
