"use client";

import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { ArchiveIcon, Trash2Icon } from "lucide-react";
import { useId, useState } from "react";
import { toast } from "sonner";

export function ConfirmDemo() {
  const outcomeId = useId();
  const [outcome, setOutcome] = useState("Nothing has been archived or discarded yet.");

  const archive = () => {
    setOutcome("QT-2026-00017 is in the archive.");
    toast.success("Archived QT-2026-00017.");
  };

  const discard = () => {
    setOutcome("The draft invoice was discarded.");
    toast.success("Discarded the draft invoice.");
  };

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap gap-3">
        <ConfirmDialog
          trigger={
            <Button type="button" variant="outline">
              <ArchiveIcon data-icon="inline-start" aria-hidden="true" />
              Archive the quotation
            </Button>
          }
          title="Archive this quotation?"
          description="QT-2026-00017 moves to the archive, where it can be restored."
          confirmLabel="Archive"
          onConfirm={archive}
        />
        <ConfirmDialog
          trigger={
            <Button type="button" variant="outline">
              <Trash2Icon data-icon="inline-start" aria-hidden="true" />
              Discard the draft
            </Button>
          }
          title="Discard this draft?"
          description="The draft invoice and its lines are deleted. This cannot be undone."
          confirmLabel="Discard"
          cancelLabel="Keep the draft"
          tone="destructive"
          onConfirm={discard}
          focusAfterConfirm={() => document.getElementById(outcomeId)}
        />
      </div>
      <p
        id={outcomeId}
        tabIndex={-1}
        data-testid="confirm-demo-outcome"
        className="rounded-sm text-body text-muted-foreground focus-ring"
      >
        {outcome}
      </p>
    </div>
  );
}
