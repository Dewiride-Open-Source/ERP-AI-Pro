import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";

import type { SearchedListColumn, SearchedListLayout, SearchedListRow } from "./searched-list-view.ts";

// The rows are drawn twice, as a table and as cards, and the width of the list's own box shows one of them, at the width
// the data table changes at, so a list keeps its shape wherever a page places it; a list laid out as cards draws only the
// cards. The title column leads the table, because a row header names only the cells after it in its row. Headings and
// cells wrap and a row's cells start at its top, because a cell may hold a sentence, which on one line would push the other
// columns out of sight. A card puts each heading above its value on a narrow box and beside it on a wider one, the heading
// taking at most two fifths of the width, so a long heading never leaves its value without room.
export function SearchedListTable({
  labelledBy,
  columns,
  rows,
  pending = false,
  layout = "responsive",
}: {
  labelledBy: string;
  columns: readonly SearchedListColumn[];
  rows: readonly SearchedListRow[];
  pending?: boolean | undefined;
  layout?: SearchedListLayout | undefined;
}) {
  const title = columns.find((column) => column.title === true) ?? columns[0];
  const details = columns.filter((column) => column !== title);
  const tableColumns = title === undefined ? details : [title, ...details];

  return (
    <div
      data-pending={pending || undefined}
      className="@container/searched-list min-w-0 transition-opacity duration-(--motion-duration-fast) data-pending:opacity-60"
    >
      {layout === "cards" ? null : (
        <Table
          aria-labelledby={labelledBy}
          aria-busy={pending || undefined}
          data-testid="searched-list-table"
          className="hidden @2xl/searched-list:table"
        >
          <TableHeader>
            <TableRow>
              {tableColumns.map((column) => (
                <TableHead key={column.id} scope="col" className="whitespace-normal">
                  {column.header}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row) => (
              <TableRow key={row.id} data-testid="searched-list-row">
                {tableColumns.map((column) =>
                  column === title ? (
                    <TableHead key={column.id} scope="row" className="h-auto p-2 align-top whitespace-normal">
                      {row.cells[column.id]}
                    </TableHead>
                  ) : (
                    <TableCell key={column.id} className="align-top whitespace-normal">
                      {row.cells[column.id]}
                    </TableCell>
                  ),
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      <ul
        role="list"
        aria-labelledby={labelledBy}
        aria-busy={pending || undefined}
        data-testid="searched-list-cards"
        className={layout === "cards" ? "grid gap-2" : "grid gap-2 @2xl/searched-list:hidden"}
      >
        {rows.map((row) => (
          <li
            key={row.id}
            data-testid="searched-list-card"
            className="grid gap-3 rounded-lg border bg-card p-3"
          >
            <p data-testid="searched-list-card-title" className="font-medium break-words">
              {title === undefined ? null : row.cells[title.id]}
            </p>
            {details.length > 0 ? (
              <dl className="grid grid-cols-[minmax(0,1fr)] gap-y-2 text-sm @md/searched-list:grid-cols-[fit-content(40%)_minmax(0,1fr)] @md/searched-list:gap-x-4 @md/searched-list:gap-y-1.5">
                {details.map((column) => (
                  <div
                    key={column.id}
                    className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-y-0.5 @md/searched-list:col-span-2 @md/searched-list:grid-cols-subgrid"
                  >
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
