"use client";

import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { DownloadIcon, Trash2Icon } from "lucide-react";
import { useState, useTransition } from "react";
import { toast } from "sonner";

import { useRemoveRow } from "@/shared/lists/list-row-removal";

import { createDownloadLink, deleteAttachment } from "../server/actions";

export function AttachmentActions({
  id,
  fileName,
  focusAfterDeleteId,
}: {
  id: string;
  fileName: string;
  focusAfterDeleteId: string;
}) {
  const removeRow = useRemoveRow();
  const [downloading, startDownload] = useTransition();
  const [error, setError] = useState<string>();

  const download = () =>
    startDownload(async () => {
      setError(undefined);
      const result = await createDownloadLink(id);
      if ("url" in result) downloadWithoutLeavingPage(result.url, fileName);
      else setError(result.error);
    });

  const remove = () => {
    void removeRow(id, () => deleteAttachment(id)).then((outcome) => {
      if (outcome.removed) {
        toast.success(`Deleted ${fileName}.`);
        return;
      }
      toast.error(`${fileName} was not deleted.`, {
        description: outcome.reference
          ? `${outcome.message} Reference: ${outcome.reference}`
          : outcome.message,
      });
    });
  };

  return (
    <div className="flex flex-col items-end gap-1">
      <div className="flex gap-1">
        <Button
          variant="ghost"
          size="sm"
          onClick={download}
          disabled={downloading}
          aria-label={`Download ${fileName}`}
          data-testid="attachment-download"
        >
          <DownloadIcon aria-hidden />
          <span className="max-sm:sr-only">Download</span>
        </Button>
        <ConfirmDialog
          trigger={
            <Button
              variant="ghost"
              size="sm"
              disabled={downloading}
              aria-label={`Delete ${fileName}`}
              data-testid="attachment-delete"
            >
              <Trash2Icon aria-hidden />
              <span className="max-sm:sr-only">Delete</span>
            </Button>
          }
          title="Delete this attachment?"
          description={
            <>
              <span className="font-medium text-foreground">{fileName}</span> is removed from the list and its
              download links stop working.
            </>
          }
          cancelLabel="Keep it"
          confirmLabel="Delete"
          tone="destructive"
          onConfirm={remove}
          focusAfterConfirm={() => document.getElementById(focusAfterDeleteId)}
        />
      </div>
      {error ? (
        <p role="alert" className="text-xs text-destructive" data-testid="attachment-action-error">
          {error}
        </p>
      ) : null}
    </div>
  );
}

function downloadWithoutLeavingPage(url: string, fileName: string) {
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.hidden = true;
  document.body.append(link);
  link.click();
  link.remove();
}
