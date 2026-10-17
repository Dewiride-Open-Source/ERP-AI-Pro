"use client";

import type { PersonStatus } from "@dewiride/erp-api-client";
import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { Trash2Icon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { toast } from "sonner";

import { peopleNavigation } from "../../nav";
import { deletePerson } from "../server/actions";

import { PersonActionButton, PersonActionRefusalMessage, settlePersonAction } from "./person-action-controls";
import { deletion, type PersonActionRefusal } from "./person-action-text";

// A deleted record has no page any more, so the person goes back to People, and the deleted record's address leaves the
// history rather than answering "not found" on Back.
export function DeleteAction({
  personId,
  personName,
  status,
}: {
  personId: string;
  personName: string;
  status: PersonStatus | undefined;
}) {
  const router = useRouter();
  const [pending, startDeletion] = useTransition();
  const [refusal, setRefusal] = useState<PersonActionRefusal>();
  const texts = deletion(personName, status);

  const confirm = () => {
    setRefusal(undefined);
    startDeletion(async () => {
      const outcome = await settlePersonAction(() => deletePerson(personId));
      if (!outcome.done) {
        setRefusal(outcome);
        return;
      }
      if (outcome.message === undefined) toast.success(texts.done);
      else toast.info(texts.gone, { description: outcome.message });
      router.replace(peopleNavigation.basePath);
    });
  };

  return (
    <div className="grid justify-items-start gap-1">
      <ConfirmDialog
        trigger={
          <PersonActionButton
            pending={pending}
            icon={Trash2Icon}
            variant="outline"
            size="sm"
            data-testid="person-delete-action"
          >
            Delete
          </PersonActionButton>
        }
        title={texts.title}
        description={texts.description}
        cancelLabel={texts.cancel}
        confirmLabel={texts.confirm}
        tone="destructive"
        onConfirm={confirm}
      />
      {refusal ? <PersonActionRefusalMessage refusal={refusal} testId="person-delete-error" /> : null}
    </div>
  );
}
