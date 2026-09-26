import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";

import { layers } from "./token-catalogue";

export function LayerScale() {
  return (
    <div className="min-w-0 rounded-xl border bg-card p-4">
      <Table>
        <TableCaption>Higher layers paint above lower ones.</TableCaption>
        <TableHeader>
          <TableRow>
            <TableHead className="whitespace-normal">Layer</TableHead>
            <TableHead className="whitespace-normal">Token and value</TableHead>
            <TableHead className="whitespace-normal">Used by</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {layers.map((layer) => (
            <TableRow key={layer.name}>
              <TableCell className="align-top font-medium">{layer.name}</TableCell>
              <TableCell className="align-top whitespace-normal">
                <code className="font-mono wrap-anywhere">{layer.token}</code> · {layer.value}
                <span className="block text-caption wrap-anywhere text-muted-foreground">
                  {layer.utility}
                </span>
              </TableCell>
              <TableCell className="align-top whitespace-normal">{layer.use}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
