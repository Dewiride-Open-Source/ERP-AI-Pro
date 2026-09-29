import { Skeleton } from "@dewiride/erp-ui/components/ui/skeleton";

export function DataTableSkeleton({ columns, rows = 5 }: { columns: number; rows?: number }) {
  const cells = Array.from({ length: columns }, (_, index) => `cell-${index}`);

  return (
    <div data-testid="data-table-skeleton" className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Skeleton className="h-8 w-64 max-w-full" />
        <Skeleton className="h-7 w-24" />
      </div>
      <div className="grid gap-2">
        {Array.from({ length: rows }, (_, row) => (
          <div key={`row-${row}`} className="flex gap-4 border-b pb-2 last:border-0">
            {cells.map((cell) => (
              <Skeleton key={cell} className="h-5 flex-1" />
            ))}
          </div>
        ))}
      </div>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Skeleton className="h-4 w-44" />
        <Skeleton className="h-7 w-56 max-w-full" />
      </div>
    </div>
  );
}
