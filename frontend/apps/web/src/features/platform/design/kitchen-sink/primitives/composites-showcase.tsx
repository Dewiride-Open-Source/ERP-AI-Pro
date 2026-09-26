"use client";

import { ThemeToggle } from "@dewiride/erp-ui/components/theme/theme-toggle";
import { FileDropZone, type FileRejection } from "@dewiride/erp-ui/components/upload/file-drop-zone";
import { toast } from "sonner";

import { Specimen, SpecimenGrid } from "../components/specimen";

const accept = ["image/png"] as const;
const maxSizeBytes = 1024 * 1024;

const rejectionMessages: Record<FileRejection, string> = {
  empty: "The file is empty.",
  "too-large": "The file is larger than 1 MiB.",
  "unsupported-type": "Only PNG images are accepted here.",
};

export function CompositesShowcase() {
  return (
    <SpecimenGrid>
      <Specimen
        title="Theme toggle"
        description="Light, dark or the device setting; arrow keys move between options."
      >
        <ThemeToggle className="w-fit" />
      </Specimen>
      <Specimen
        title="File drop zone"
        description="Checks the type and size before anything is sent. Nothing is uploaded from this page."
      >
        <FileDropZone
          accept={accept}
          maxSizeBytes={maxSizeBytes}
          label="Drop a PNG image here"
          hint="PNG only, up to 1 MiB."
          onFileAccepted={(file) =>
            toast.success("File accepted", { description: `${file.name} passed the checks.` })
          }
          onFileRejected={(file, reason) =>
            toast.error("File refused", { description: `${file.name}: ${rejectionMessages[reason]}` })
          }
        />
      </Specimen>
    </SpecimenGrid>
  );
}
