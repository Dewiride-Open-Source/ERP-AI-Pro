import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";

import { motionDurations, motionEasings } from "./token-catalogue";

export function MotionScale() {
  return (
    <div className="min-w-0 rounded-xl border bg-card p-4">
      <Table data-testid="motion-tokens">
        <TableHeader>
          <TableRow>
            <TableHead>Token</TableHead>
            <TableHead>Value</TableHead>
            <TableHead>Used by</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {[...motionDurations, ...motionEasings].map((token) => (
            <TableRow key={token.token}>
              <TableCell className="align-top whitespace-normal">
                <code className="font-mono">{token.token}</code>
                <span className="block text-caption text-muted-foreground">{token.utility}</span>
              </TableCell>
              <TableCell className="align-top whitespace-normal">{token.value}</TableCell>
              <TableCell className="align-top whitespace-normal">{token.use}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
