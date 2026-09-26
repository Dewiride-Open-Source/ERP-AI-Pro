"use client";

import { UploadCloudIcon } from "lucide-react";
import { useId, useRef, useState, type DragEvent, type ReactNode } from "react";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { cn } from "@dewiride/erp-ui/lib/utils";

export type FileRejection = "empty" | "too-large" | "unsupported-type";

export type FileDropZoneProps = {
  accept: readonly string[];
  maxSizeBytes: number;
  label: string;
  hint?: ReactNode;
  disabled?: boolean;
  className?: string;
  onFileAccepted: (file: File) => void;
  onFileRejected: (file: File, reason: FileRejection) => void;
};

// The checks mirror the server's so a person hears about a wrong file at once; the server still decides.
export function checkFile(
  file: File,
  accept: readonly string[],
  maxSizeBytes: number,
): FileRejection | undefined {
  if (file.size === 0) return "empty";
  if (file.size > maxSizeBytes) return "too-large";
  if (!accept.includes(file.type.toLowerCase())) return "unsupported-type";
  return undefined;
}

export function FileDropZone({
  accept,
  maxSizeBytes,
  label,
  hint,
  disabled = false,
  className,
  onFileAccepted,
  onFileRejected,
}: FileDropZoneProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const hintId = useId();
  const [dragging, setDragging] = useState(false);

  const take = (files: FileList | null) => {
    const file = files?.item(0);
    if (!file || disabled) return;
    const rejection = checkFile(file, accept, maxSizeBytes);
    if (rejection) onFileRejected(file, rejection);
    else onFileAccepted(file);
  };

  const onDragOver = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    if (!disabled) setDragging(true);
  };

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragging(false);
    take(event.dataTransfer.files);
  };

  return (
    <div
      role="group"
      aria-label={label}
      aria-describedby={hint ? hintId : undefined}
      aria-disabled={disabled || undefined}
      data-testid="file-drop-zone"
      data-dragging={dragging || undefined}
      onDragOver={onDragOver}
      onDragLeave={() => setDragging(false)}
      onDrop={onDrop}
      className={cn(
        "flex flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed bg-muted/30 p-6 text-center transition-colors duration-200 ease-out motion-reduce:transition-none",
        dragging ? "border-primary bg-primary/5" : "border-border",
        disabled && "opacity-60",
        className,
      )}
    >
      <UploadCloudIcon className="size-8 text-muted-foreground" aria-hidden />
      <div className="grid gap-1">
        <p className="font-medium">{label}</p>
        {hint ? (
          <p id={hintId} className="text-sm text-muted-foreground">
            {hint}
          </p>
        ) : null}
      </div>
      <Button
        type="button"
        variant="outline"
        disabled={disabled}
        onClick={() => inputRef.current?.click()}
        data-testid="file-drop-zone-choose"
      >
        Choose a file
      </Button>
      <input
        ref={inputRef}
        type="file"
        className="sr-only"
        tabIndex={-1}
        aria-hidden
        accept={accept.join(",")}
        disabled={disabled}
        data-testid="file-drop-zone-input"
        onChange={(event) => {
          take(event.currentTarget.files);
          event.currentTarget.value = "";
        }}
      />
    </div>
  );
}
