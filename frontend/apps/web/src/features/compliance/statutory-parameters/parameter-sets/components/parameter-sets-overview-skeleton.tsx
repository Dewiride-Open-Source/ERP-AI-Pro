import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

import { parameterSetsNavigation } from "../../nav";

import { setColumns, setsPerPage } from "./set-list";

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

      <DataTableSkeleton columns={setColumns.length} rows={setsPerPage} />
    </LoadingStatus>
  );
}
