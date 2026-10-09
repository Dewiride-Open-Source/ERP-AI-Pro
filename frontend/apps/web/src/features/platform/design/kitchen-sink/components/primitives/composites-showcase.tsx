"use client";

import { CommandPalette, type CommandPaletteItem } from "@dewiride/erp-ui/components/command/command-palette";
import { ThemeToggle } from "@dewiride/erp-ui/components/theme/theme-toggle";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { FileDropZone, type FileRejection } from "@dewiride/erp-ui/components/upload/file-drop-zone";
import { ClockIcon, FileTextIcon, HouseIcon, ReceiptIcon, UsersIcon } from "lucide-react";
import { useRef, useState } from "react";
import { toast } from "sonner";

import { Specimen, SpecimenGrid } from "../specimen";

import { DataTableSpecimens } from "./data-table-specimen";

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
      <CommandPaletteSpecimen />
      <DataTableSpecimens />
    </SpecimenGrid>
  );
}

function CommandPaletteSpecimen() {
  const [open, setOpen] = useState(false);
  const [chosen, setChosen] = useState<string | undefined>(undefined);
  const opener = useRef<HTMLButtonElement>(null);
  const examples: readonly CommandPaletteItem[] = [
    { id: "home", label: "Home", icon: <HouseIcon aria-hidden /> },
    {
      id: "invoices",
      label: "Invoices",
      description: "Issue and track tax invoices.",
      group: "Finance",
      icon: <FileTextIcon aria-hidden />,
    },
    {
      id: "credit-notes",
      label: "Credit notes",
      description: "Correct an invoice that was issued.",
      group: "Finance",
      keywords: ["refund"],
      icon: <ReceiptIcon aria-hidden />,
    },
    {
      id: "customers",
      label: "Customers",
      description: "Clients with their GSTINs and contacts.",
      group: "Clients",
      icon: <UsersIcon aria-hidden />,
    },
    {
      id: "timesheets",
      label: "Timesheets",
      description: "Hours worked this week.",
      group: "Timesheets",
      icon: <ClockIcon aria-hidden />,
    },
  ];

  return (
    <Specimen
      title="Command palette"
      description="Ctrl+K or ⌘K opens it in the shell. Typing filters the list, the arrow keys move and Enter chooses."
    >
      <div className="flex flex-wrap items-center gap-3">
        <Button ref={opener} variant="outline" onClick={() => setOpen(true)}>
          Open the command palette
        </Button>
        <p
          aria-live="polite"
          data-testid="composites-command-palette-choice"
          className="text-caption text-muted-foreground"
        >
          {chosen === undefined ? "Nothing chosen yet." : `Chosen: ${chosen}`}
        </p>
      </div>
      <CommandPalette
        open={open}
        onOpenChange={setOpen}
        items={examples}
        onSelect={(item) => {
          setChosen(item.label);
          setOpen(false);
        }}
        title="Example commands"
        inputLabel="Search the examples"
        placeholder="Search the examples…"
        listLabel="Examples"
        emptyMessage="No example matches your search."
        countMessage={(count) => (count === 1 ? "1 example" : `${count} examples`)}
        returnFocusTo={opener}
      />
    </Specimen>
  );
}
