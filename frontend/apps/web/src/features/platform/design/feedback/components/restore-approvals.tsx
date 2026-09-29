"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { RotateCcwIcon } from "lucide-react";
import { useTransition } from "react";
import { toast } from "sonner";

import { restoreApprovals } from "../server/actions";

export function RestoreApprovals() {
  const [restoring, startRestore] = useTransition();

  const restore = () => {
    if (restoring) return;
    startRestore(async () => {
      await restoreApprovals();
      toast.success("Restored every request.");
    });
  };

  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
      onClick={restore}
      aria-disabled={restoring || undefined}
      aria-busy={restoring || undefined}
      className="aria-disabled:opacity-50"
    >
      {restoring ? (
        <Spinner data-icon="inline-start" aria-hidden="true" />
      ) : (
        <RotateCcwIcon data-icon="inline-start" aria-hidden="true" />
      )}
      Restore the requests
    </Button>
  );
}
