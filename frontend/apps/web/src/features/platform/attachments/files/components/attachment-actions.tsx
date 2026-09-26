"use client";

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@dewiride/erp-ui/components/ui/alert-dialog";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { DownloadIcon, Trash2Icon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useRef, useState, useTransition } from "react";

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
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [error, setError] = useState<string>();
  const deleteConfirmed = useRef(false);

  const download = () =>
    startTransition(async () => {
      setError(undefined);
      const result = await createDownloadLink(id);
      if ("url" in result) downloadWithoutLeavingPage(result.url, fileName);
      else setError(result.error);
    });

  const remove = () => {
    deleteConfirmed.current = true;
    startTransition(async () => {
      setError(undefined);
      const result = await deleteAttachment(id);
      if (result.error) setError(result.error);
      else router.refresh();
    });
  };

  const moveFocusAfterDelete = (event: Event) => {
    if (!deleteConfirmed.current) return;
    deleteConfirmed.current = false;
    event.preventDefault();
    document.getElementById(focusAfterDeleteId)?.focus();
  };

  return (
    <div className="flex flex-col items-end gap-1">
      <div className="flex gap-1">
        <Button
          variant="ghost"
          size="sm"
          onClick={download}
          disabled={pending}
          aria-label={`Download ${fileName}`}
          data-testid="attachment-download"
        >
          <DownloadIcon aria-hidden />
          <span className="max-sm:sr-only">Download</span>
        </Button>
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button
              variant="ghost"
              size="sm"
              disabled={pending}
              aria-label={`Delete ${fileName}`}
              data-testid="attachment-delete"
            >
              <Trash2Icon aria-hidden />
              <span className="max-sm:sr-only">Delete</span>
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent onCloseAutoFocus={moveFocusAfterDelete} data-testid="attachment-delete-dialog">
            <AlertDialogHeader>
              <AlertDialogTitle>Delete this attachment?</AlertDialogTitle>
              <AlertDialogDescription>
                <span className="font-medium text-foreground">{fileName}</span> is removed from the list and
                its download links stop working.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel data-testid="attachment-delete-cancel">Keep it</AlertDialogCancel>
              <AlertDialogAction
                variant="destructive"
                onClick={remove}
                data-testid="attachment-delete-confirm"
              >
                Delete
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
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
