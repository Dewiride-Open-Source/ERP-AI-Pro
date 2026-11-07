import { DataTableSkeleton } from "@dewiride/erp-ui/components/data-table/data-table-skeleton";
import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

import { exchangeRatesNavigation } from "../../nav";

export function ExchangeRatesOverviewSkeleton() {
  return (
    <LoadingStatus
      label="Loading exchange rates"
      pageTitle={exchangeRatesNavigation.title}
      className="grid gap-section"
      data-testid="exchange-rates-loading"
    >
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-full max-w-24" />
          <Skeleton className="h-9 w-full max-w-48" />
        </div>
        <div className="flex flex-wrap gap-2">
          <Skeleton className="h-8 w-32" />
          <Skeleton className="h-8 w-32" />
        </div>
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-full max-w-24" />
          <Skeleton className="h-4 w-full max-w-96" />
        </CardHeader>
        <CardContent>
          <DataTableSkeleton columns={5} />
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
