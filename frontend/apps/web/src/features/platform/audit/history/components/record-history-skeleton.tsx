import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

const changePlaceholders = ["first", "second", "third"];

export function RecordHistorySkeleton() {
  return (
    <LoadingStatus label="Loading the latest changes" data-testid="record-history-loading">
      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-36" />
          <Skeleton className="h-4 w-96 max-w-full" />
        </CardHeader>
        <CardContent className="grid gap-5">
          <Skeleton className="h-5 w-56 max-w-full" />
          {changePlaceholders.map((placeholder) => (
            <div key={placeholder} className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-3">
              <Skeleton className="mt-0.5 size-3 rounded-full" />
              <div className="grid gap-2">
                <Skeleton className="h-3 w-20" />
                <Skeleton className="h-4 w-full max-w-md" />
              </div>
            </div>
          ))}
          <Skeleton className="h-4 w-64 max-w-full" />
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
