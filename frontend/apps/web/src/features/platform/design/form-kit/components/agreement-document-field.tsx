"use client";

import type { AttachmentResponse } from "@dewiride/erp-api-client";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { FieldDescription, FieldError, FieldLegend, FieldSet } from "@dewiride/erp-ui/components/ui/field";
import { Progress } from "@dewiride/erp-ui/components/ui/progress";
import { FileDropZone, type FileRejection } from "@dewiride/erp-ui/components/upload/file-drop-zone";
import { CheckCircle2Icon, CircleAlertIcon } from "lucide-react";
import { useState, type Ref } from "react";

import { ApiError } from "@/shared/api/problem-details";
import { uploadFile } from "@/shared/api/upload";
import { formatBytes } from "@/shared/format/sizes";

const acceptedTypes = ["application/pdf", "image/png"] as const;

const maxSizeBytes = 10 * 1024 * 1024;

type DocumentUpload =
  | { readonly kind: "none" }
  | { readonly kind: "uploading"; readonly fileName: string; readonly percent: number }
  | { readonly kind: "attached"; readonly fileName: string }
  | { readonly kind: "failed"; readonly fileName: string; readonly message: string };

const rejectionMessages: Readonly<Record<FileRejection, string>> = {
  empty: "the file is empty.",
  "too-large": `the file is larger than ${formatBytes(maxSizeBytes)}.`,
  "unsupported-type": "only PDF files and PNG images can be attached here.",
};

export function AgreementDocumentField({
  id,
  value,
  onValueChange,
  onUploadingChange,
  errors,
  ref,
}: {
  id: string;
  value: string;
  onValueChange: (attachmentId: string) => void;
  onUploadingChange: (uploading: boolean) => void;
  errors: readonly string[];
  ref: Ref<HTMLFieldSetElement>;
}) {
  const [upload, setUpload] = useState<DocumentUpload>({ kind: "none" });
  const shown: DocumentUpload = upload.kind === "attached" && value === "" ? { kind: "none" } : upload;
  const descriptionId = `${id}-description`;
  const errorId = `${id}-error`;
  const invalid = errors.length > 0;

  const send = async (file: File) => {
    setUpload({ kind: "uploading", fileName: file.name, percent: 0 });
    onUploadingChange(true);
    try {
      const attachment = await uploadFile<AttachmentResponse>(
        "/platform/attachments",
        file,
        ({ loaded, total }) =>
          setUpload({ kind: "uploading", fileName: file.name, percent: Math.round((loaded / total) * 100) }),
      );
      if (attachment.id) {
        setUpload({ kind: "attached", fileName: file.name });
        onValueChange(attachment.id);
      } else {
        setUpload({
          kind: "failed",
          fileName: file.name,
          message: "the server did not say where it stored the file.",
        });
      }
    } catch (error) {
      const message =
        error instanceof ApiError ? (error.problem.detail ?? error.message) : "The upload failed.";
      setUpload({ kind: "failed", fileName: file.name, message });
    } finally {
      onUploadingChange(false);
    }
  };

  const remove = () => {
    setUpload({ kind: "none" });
    onValueChange("");
  };

  return (
    <FieldSet
      ref={ref}
      id={id}
      tabIndex={-1}
      aria-describedby={invalid ? `${descriptionId} ${errorId}` : descriptionId}
      data-testid="agreement-document"
      className="gap-3 rounded-lg outline-none"
    >
      <FieldLegend variant="label">Agreement document</FieldLegend>
      <FieldDescription id={descriptionId}>
        Optional. The file is stored encrypted in the attachments list; the supplier itself is never saved.
      </FieldDescription>
      {shown.kind === "attached" ? null : (
        <FileDropZone
          accept={acceptedTypes}
          maxSizeBytes={maxSizeBytes}
          label="Drop the signed agreement here or choose it"
          hint={`PDF or PNG, up to ${formatBytes(maxSizeBytes)}.`}
          disabled={shown.kind === "uploading"}
          onFileAccepted={(file) => void send(file)}
          onFileRejected={(file, reason) =>
            setUpload({ kind: "failed", fileName: file.name, message: rejectionMessages[reason] })
          }
        />
      )}
      <div data-testid="agreement-upload-status" data-state={shown.kind} className="grid gap-2">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div aria-live="polite" className="min-w-0">
            <UploadMessage upload={shown} />
          </div>
          {shown.kind === "attached" ? (
            <Button type="button" variant="outline" size="sm" onClick={remove}>
              Remove the document
            </Button>
          ) : null}
        </div>
        {shown.kind === "uploading" ? (
          <Progress
            value={shown.percent}
            aria-label={`Uploading ${shown.fileName}`}
            data-testid="agreement-upload-progress"
          />
        ) : null}
      </div>
      {invalid ? <FieldError id={errorId} errors={errors.map((message) => ({ message }))} /> : null}
    </FieldSet>
  );
}

function UploadMessage({ upload }: { upload: DocumentUpload }) {
  switch (upload.kind) {
    case "none":
      return null;
    case "uploading":
      return (
        <p className="text-sm">
          Uploading <span className="font-medium">{upload.fileName}</span>…
        </p>
      );
    case "attached":
      return (
        <p className="flex items-center gap-2 text-sm">
          <CheckCircle2Icon className="size-4 shrink-0 text-success" aria-hidden />
          <span className="min-w-0 break-all">
            <span className="font-medium">{upload.fileName}</span> is attached.
          </span>
        </p>
      );
    case "failed":
      return (
        <p className="flex items-center gap-2 text-sm text-destructive" data-testid="agreement-upload-error">
          <CircleAlertIcon className="size-4 shrink-0" aria-hidden />
          <span className="min-w-0 break-all">
            <span className="font-medium">{upload.fileName}</span>: {upload.message}
          </span>
        </p>
      );
  }
}
