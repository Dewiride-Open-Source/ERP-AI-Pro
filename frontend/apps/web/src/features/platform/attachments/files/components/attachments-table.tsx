import type { AttachmentResponse } from "@dewiride/erp-api-client";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";

import { formatDateTimeIst } from "@/shared/format/dates";
import { formatBytes } from "@/shared/format/sizes";

import { AttachmentActions } from "./attachment-actions";
import { contentTypeLabel } from "./content-types";

const missing = "—";

export function AttachmentsTable({
  attachments,
  focusAfterDeleteId,
}: {
  attachments: readonly AttachmentResponse[];
  focusAfterDeleteId: string;
}) {
  if (attachments.length === 0) {
    return (
      <p
        role="status"
        data-testid="attachments-empty"
        className="rounded-lg border bg-muted/40 p-4 text-sm text-muted-foreground"
      >
        No attachments yet. Upload a file to see it here.
      </p>
    );
  }

  return (
    <Table data-testid="attachments-table">
      <TableHeader>
        <TableRow>
          <TableHead>File</TableHead>
          <TableHead>Size</TableHead>
          <TableHead className="max-md:hidden">Uploaded</TableHead>
          <TableHead className="max-md:hidden">Virus scan</TableHead>
          <TableHead className="text-right">
            <span className="sr-only">Actions</span>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {attachments.map((attachment) => (
          <TableRow key={attachment.id} data-testid="attachments-row">
            <TableCell className="max-w-64 whitespace-normal">
              <p className="font-medium break-words" data-testid="attachments-file-name">
                {attachment.fileName ?? missing}
              </p>
              <p className="text-xs text-muted-foreground">{contentTypeLabel(attachment.contentType)}</p>
            </TableCell>
            <TableCell className="tabular-nums">
              {attachment.sizeBytes === undefined || attachment.sizeBytes === null
                ? missing
                : formatBytes(attachment.sizeBytes)}
            </TableCell>
            <TableCell className="max-md:hidden">
              {attachment.createdAt ? formatDateTimeIst(attachment.createdAt) : missing}
            </TableCell>
            <TableCell className="max-md:hidden">
              <Badge variant={attachment.scanStatus === "clean" ? "secondary" : "outline"}>
                {attachment.scanStatus === "clean" ? "Clean" : "Not scanned"}
              </Badge>
            </TableCell>
            <TableCell className="text-right">
              {attachment.id ? (
                <AttachmentActions
                  id={attachment.id}
                  fileName={attachment.fileName ?? "this attachment"}
                  focusAfterDeleteId={focusAfterDeleteId}
                />
              ) : null}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
