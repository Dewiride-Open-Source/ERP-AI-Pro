import { LoadingStatus } from "@dewiride/erp-ui/components/feedback/loading-status";
import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

const contentLines = ["first", "second", "third", "fourth"];

export function PageLoading() {
  return (
    <LoadingStatus label="Loading the page" className="grid gap-section" data-testid="page-loading">
      <header className="grid gap-2">
        <Skeleton className="h-3 w-20" />
        <Skeleton className="h-9 w-64 max-w-full" />
        <Skeleton className="h-4 w-96 max-w-full" />
      </header>
      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-40" />
          <Skeleton className="h-4 w-72 max-w-full" />
        </CardHeader>
        <CardContent className="grid gap-3">
          {contentLines.map((line) => (
            <Skeleton key={line} className="h-8 w-full" />
          ))}
        </CardContent>
      </Card>
    </LoadingStatus>
  );
}
