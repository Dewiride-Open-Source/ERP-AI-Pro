import { Card, CardContent, CardHeader } from "@dewiride/erp-ui/components/ui/card";
import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

const identityTiles = ["application", "version", "started", "uptime"];
const startupRows = ["first", "second", "third", "fourth", "fifth"];

export default function SystemInfoLoading() {
  return (
    <div
      className="grid gap-6"
      role="status"
      aria-label="Loading system information"
      data-testid="system-info-loading"
    >
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="grid gap-2">
          <Skeleton className="h-3 w-20" />
          <Skeleton className="h-9 w-64" />
        </div>
        <Skeleton className="h-9 w-24" />
      </header>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-16" />
          <Skeleton className="h-4 w-72 max-w-full" />
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 sm:grid-cols-2">
            {identityTiles.map((tile) => (
              <div key={tile} className="grid gap-2 rounded-lg border bg-muted/40 p-4">
                <Skeleton className="h-3 w-20" />
                <Skeleton className="h-5 w-40 max-w-full" />
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="gap-2">
          <Skeleton className="h-5 w-28" />
          <Skeleton className="h-4 w-80 max-w-full" />
        </CardHeader>
        <CardContent className="grid gap-3">
          {startupRows.map((row) => (
            <Skeleton key={row} className="h-8 w-full" />
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
