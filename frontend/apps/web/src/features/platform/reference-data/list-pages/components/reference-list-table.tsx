import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";

import type { ReferenceListColumn, ReferenceListRow } from "./reference-list-view";

// The rows are drawn twice, as a table and as cards, and the width of the list's own box shows one of them, at the width
// the data table changes at, so a list keeps its shape wherever a page places it.
export function ReferenceListTable({
  labelledBy,
  columns,
  rows,
  pending = false,
}: {
  labelledBy: string;
  columns: readonly ReferenceListColumn[];
  rows: readonly ReferenceListRow[];
  pending?: boolean | undefined;
}) {
  const title = columns.find((column) => column.title === true) ?? columns[0];
  const details = columns.filter((column) => column !== title);

  return (
    <div
      data-pending={pending || undefined}
      className="@container/reference-list min-w-0 transition-opacity duration-(--motion-duration-fast) data-pending:opacity-60"
    >
      <Table
        aria-labelledby={labelledBy}
        aria-busy={pending || undefined}
        data-testid="reference-list-table"
        className="hidden @2xl/reference-list:table"
      >
        <TableHeader>
          <TableRow>
            {columns.map((column) => (
              <TableHead key={column.id} scope="col">
                {column.header}
              </TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={row.id} data-testid="reference-list-row">
              {columns.map((column) =>
                column === title ? (
                  <TableHead key={column.id} scope="row" className="h-auto p-2 whitespace-normal">
                    {row.cells[column.id]}
                  </TableHead>
                ) : (
                  <TableCell key={column.id}>{row.cells[column.id]}</TableCell>
                ),
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <ul
        role="list"
        aria-labelledby={labelledBy}
        aria-busy={pending || undefined}
        data-testid="reference-list-cards"
        className="grid gap-2 @2xl/reference-list:hidden"
      >
        {rows.map((row) => (
          <li
            key={row.id}
            data-testid="reference-list-card"
            className="grid gap-3 rounded-lg border bg-card p-3"
          >
            <p data-testid="reference-list-card-title" className="font-medium break-words">
              {title === undefined ? null : row.cells[title.id]}
            </p>
            {details.length > 0 ? (
              <dl className="grid grid-cols-[minmax(0,auto)_minmax(0,1fr)] gap-x-4 gap-y-1.5 text-sm">
                {details.map((column) => (
                  <div key={column.id} className="contents">
                    <dt className="text-muted-foreground">{column.header}</dt>
                    <dd className="min-w-0 break-words">{row.cells[column.id]}</dd>
                  </div>
                ))}
              </dl>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  );
}
