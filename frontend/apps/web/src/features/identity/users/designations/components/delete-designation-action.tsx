"use client";

import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { Trash2Icon } from "lucide-react";
import { useLayoutEffect, useRef, useTransition } from "react";
import { toast } from "sonner";

import { PersonActionButton, settlePersonAction } from "../../details/components/person-action-controls";
import type { PersonActionRefusal } from "../../details/components/person-action-text";
import { deleteDesignation } from "../server/actions";

import { deleteLabel, designationDeletion } from "./designation-text";

// Focus returns to Delete when the confirmation closes, so a refusal shows beside the button that has it; a deleted
// designation leaves the list with its button, so focus moves to the list's heading as the button goes.
export function DeleteDesignationAction({
  designationId,
  name,
  focusAfterDeleteId,
  onRefusal,
}: {
  designationId: string;
  name: string;
  focusAfterDeleteId: string;
  onRefusal: (refusal: PersonActionRefusal | undefined) => void;
}) {
  const [pending, startDeletion] = useTransition();
  const deleted = useRef(false);
  const texts = designationDeletion(name);

  useLayoutEffect(
    () => () => {
      if (deleted.current) document.getElementById(focusAfterDeleteId)?.focus();
    },
    [focusAfterDeleteId],
  );

  const remove = () => {
    onRefusal(undefined);
    startDeletion(async () => {
      const outcome = await settlePersonAction(() => deleteDesignation(designationId));
      if (!outcome.done) {
        onRefusal(outcome);
        return;
      }
      deleted.current = true;
      if (outcome.message === undefined) toast.success(texts.done);
      else toast.info(texts.gone, { description: outcome.message });
    });
  };

  return (
    <ConfirmDialog
      trigger={
        <PersonActionButton
          pending={pending}
          icon={Trash2Icon}
          variant="ghost"
          size="sm"
          aria-label={deleteLabel(name)}
          data-testid="designation-delete"
        >
          <span className="max-sm:sr-only">Delete</span>
        </PersonActionButton>
      }
      title={texts.title}
      description={texts.description}
      cancelLabel={texts.cancel}
      confirmLabel={texts.confirm}
      tone="destructive"
      onConfirm={remove}
    />
  );
}
