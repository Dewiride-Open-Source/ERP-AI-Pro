"use client";

import type { AttachmentResponse } from "@dewiride/erp-api-client";
import { Progress } from "@dewiride/erp-ui/components/ui/progress";
import { FileDropZone, type FileRejection } from "@dewiride/erp-ui/components/upload/file-drop-zone";
import { CheckCircle2Icon, CircleAlertIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

import { ApiError } from "@/shared/api/problem-details";
import { uploadFile } from "@/shared/api/upload";
import { formatBytes } from "@/shared/format/sizes";

import { contentTypeLabel } from "./content-types";

type UploadState =
  | { readonly kind: "idle" }
  | { readonly kind: "uploading"; readonly fileName: string; readonly percent: number }
  | { readonly kind: "uploaded"; readonly fileName: string }
  | { readonly kind: "failed"; readonly fileName: string; readonly message: string };

export function UploadPanel({
  maxSizeBytes,
  allowedContentTypes,
}: {
  maxSizeBytes: number;
  allowedContentTypes: readonly string[];
}) {
  const router = useRouter();
  const [state, setState] = useState<UploadState>({ kind: "idle" });
  const [, startTransition] = useTransition();

  const upload = async (file: File) => {
    setState({ kind: "uploading", fileName: file.name, percent: 0 });
    try {
      await uploadFile<AttachmentResponse>("/platform/attachments", file, ({ loaded, total }) =>
        setState({ kind: "uploading", fileName: file.name, percent: Math.round((loaded / total) * 100) }),
      );
      setState({ kind: "uploaded", fileName: file.name });
      startTransition(() => router.refresh());
    } catch (error) {
      const message =
        error instanceof ApiError ? (error.problem.detail ?? error.message) : "The upload failed.";
      setState({ kind: "failed", fileName: file.name, message });
    }
  };

  const reject = (file: File, reason: FileRejection) =>
    setState({ kind: "failed", fileName: file.name, message: rejectionMessage(reason, maxSizeBytes) });

  const types = [...new Set(allowedContentTypes.map(contentTypeLabel))].join(", ");

  return (
    <div className="grid gap-4">
      <FileDropZone
        accept={allowedContentTypes}
        maxSizeBytes={maxSizeBytes}
        label="Drop a file here or choose one"
        hint={`${types}; up to ${formatBytes(maxSizeBytes)}.`}
        disabled={state.kind === "uploading"}
        onFileAccepted={(file) => void upload(file)}
        onFileRejected={reject}
      />
      <div aria-live="polite" data-testid="upload-status" data-state={state.kind} className="min-h-6">
        <Status state={state} />
      </div>
    </div>
  );
}

function Status({ state }: { state: UploadState }) {
  switch (state.kind) {
    case "idle":
      return null;
    case "uploading":
      return (
        <div className="grid gap-2">
          <p className="text-sm">
            Uploading <span className="font-medium">{state.fileName}</span>… {state.percent}%
          </p>
          <Progress
            value={state.percent}
            aria-label={`Uploading ${state.fileName}`}
            data-testid="upload-progress"
          />
        </div>
      );
    case "uploaded":
      return (
        <p className="flex items-center gap-2 text-sm">
          <CheckCircle2Icon className="size-4 text-primary" aria-hidden />
          <span>
            <span className="font-medium">{state.fileName}</span> was uploaded.
          </span>
        </p>
      );
    case "failed":
      return (
        <p className="flex items-center gap-2 text-sm text-destructive" data-testid="upload-error">
          <CircleAlertIcon className="size-4" aria-hidden />
          <span>
            <span className="font-medium">{state.fileName}</span>: {state.message}
          </span>
        </p>
      );
  }
}

function rejectionMessage(reason: FileRejection, maxSizeBytes: number): string {
  switch (reason) {
    case "empty":
      return "the file is empty.";
    case "too-large":
      return `the file is larger than ${formatBytes(maxSizeBytes)}.`;
    case "unsupported-type":
      return "files of this type cannot be uploaded.";
  }
}
