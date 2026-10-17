import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";
import { CheckIcon, MinusIcon } from "lucide-react";

import type { PermissionCell, PermissionMatrix as Matrix } from "./permission-rows";

export function PermissionMatrix({ matrix, labelledBy }: { matrix: Matrix; labelledBy: string }) {
  return (
    <Table aria-labelledby={labelledBy} data-testid="permissions-matrix">
      <TableHeader>
        <TableRow>
          <TableHead scope="col">Permission</TableHead>
          {matrix.columns.map((column) => (
            <TableHead key={column.role} scope="col">
              {column.label}
            </TableHead>
          ))}
        </TableRow>
      </TableHeader>
      <TableBody>
        {matrix.rows.map((row) => (
          <TableRow key={row.name} data-testid="permissions-row">
            <TableHead scope="row" className="h-auto min-w-60 py-3 align-top whitespace-normal">
              {row.description}
            </TableHead>
            {row.cells.map((cell) => (
              <TableCell key={cell.role}>
                <Holding cell={cell} />
              </TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function Holding({ cell }: { cell: PermissionCell }) {
  return cell.held ? (
    <span className="inline-flex items-center gap-1.5 text-success">
      <CheckIcon className="size-4" aria-hidden />
      Held
    </span>
  ) : (
    <span className="inline-flex items-center gap-1.5 text-muted-foreground">
      <MinusIcon className="size-4" aria-hidden />
      Not held
    </span>
  );
}
