"use client";

import { CommandPalette, type CommandPaletteItem } from "@dewiride/erp-ui/components/command/command-palette";
import { ThemeToggle } from "@dewiride/erp-ui/components/theme/theme-toggle";
import {
  Timeline,
  TimelineContent,
  TimelineGroup,
  TimelineItem,
  TimelineTime,
} from "@dewiride/erp-ui/components/timeline/timeline";
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

interface TimelineExampleEvent {
  readonly id: string;
  readonly at: string;
  readonly time: string;
  readonly actor: string;
  readonly action: string;
  readonly subject: string;
  readonly change?: { readonly field: string; readonly value: string; readonly was: string } | undefined;
}

interface TimelineExampleDay {
  readonly date: string;
  readonly heading: string;
  readonly events: readonly TimelineExampleEvent[];
}

const timelineExample: readonly TimelineExampleDay[] = [
  {
    date: "2026-10-16",
    heading: "Friday, 16 October 2026",
    events: [
      {
        id: "designation",
        at: "2026-10-16T09:35:00Z",
        time: "3:05 pm IST",
        actor: "Asha Rao",
        action: "changed the designation of",
        subject: "Vikram Iyer",
        change: { field: "Designation", value: "Senior accountant", was: "Accountant" },
      },
      {
        id: "department",
        at: "2026-10-16T05:50:00Z",
        time: "11:20 am IST",
        actor: "Asha Rao",
        action: "created the department",
        subject: "Payroll operations",
      },
    ],
  },
  {
    date: "2026-10-15",
    heading: "Thursday, 15 October 2026",
    events: [
      {
        id: "file",
        at: "2026-10-15T13:10:00Z",
        time: "6:40 pm IST",
        actor: "Rohan Gupta",
        action: "uploaded the file",
        subject: "gst-returns-q2.pdf",
      },
    ],
  },
];

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
      <TimelineSpecimen />
      <DataTableSpecimens />
    </SpecimenGrid>
  );
}

function TimelineSpecimen() {
  return (
    <Specimen
      title="Timeline"
      description="Events newest first, grouped by day. A screen reader hears a list of days, each a list of events; the markers and the line are decoration."
    >
      <Timeline label="Recent changes (example)">
        {timelineExample.map((day) => (
          <TimelineGroup key={day.date} heading={day.heading} headingLevel={4}>
            {day.events.map((event) => (
              <TimelineItem key={event.id}>
                <TimelineTime dateTime={event.at}>{event.time}</TimelineTime>
                <TimelineContent>
                  <p>
                    <span className="font-medium">{event.actor}</span> {event.action}{" "}
                    <span className="font-medium">{event.subject}</span>.
                  </p>
                  {event.change === undefined ? null : (
                    <dl className="grid grid-cols-[minmax(0,auto)_minmax(0,1fr)] gap-x-4 gap-y-1">
                      <dt className="text-muted-foreground">{event.change.field}</dt>
                      <dd className="min-w-0 break-words">
                        {event.change.value}{" "}
                        <span className="text-muted-foreground">(was {event.change.was})</span>
                      </dd>
                    </dl>
                  )}
                </TimelineContent>
              </TimelineItem>
            ))}
          </TimelineGroup>
        ))}
      </Timeline>
    </Specimen>
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
