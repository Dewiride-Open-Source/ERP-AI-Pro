"use client";

import type { PersonStatus } from "@dewiride/erp-api-client";
import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { UserCheckIcon, UserXIcon } from "lucide-react";
import { useState, useTransition } from "react";
import { toast } from "sonner";

import { changePersonStatus } from "../server/actions";

import { PersonActionButton, PersonActionRefusalMessage, settlePersonAction } from "./person-action-controls";
import { statusChange, type PersonActionRefusal } from "./person-action-text";

export function StatusAction({
  personId,
  personName,
  status,
  version,
}: {
  personId: string;
  personName: string;
  status: PersonStatus;
  version: string;
}) {
  const [pending, startChange] = useTransition();
  const [refusal, setRefusal] = useState<PersonActionRefusal>();
  const change = statusChange(status, personName);

  const confirm = () => {
    setRefusal(undefined);
    startChange(async () => {
      const outcome = await settlePersonAction(() => changePersonStatus(personId, change.to, version));
      if (outcome.done) toast.success(change.done);
      else setRefusal(outcome);
    });
  };

  return (
    <div className="grid justify-items-start gap-1">
      <ConfirmDialog
        trigger={
          <PersonActionButton
            pending={pending}
            icon={change.to === "deactivated" ? UserXIcon : UserCheckIcon}
            variant="outline"
            size="sm"
            data-testid="person-status-action"
          >
            {change.action}
          </PersonActionButton>
        }
        title={change.title}
        description={change.description}
        cancelLabel={change.cancel}
        confirmLabel={change.confirm}
        tone={change.tone}
        onConfirm={confirm}
      />
      {refusal ? <PersonActionRefusalMessage refusal={refusal} testId="person-status-error" /> : null}
    </div>
  );
}
