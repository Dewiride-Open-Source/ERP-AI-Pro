"use client";

import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { Undo2Icon } from "lucide-react";
import { useState, useTransition } from "react";
import { toast } from "sonner";

import {
  PersonActionButton,
  PersonActionRefusalMessage,
  settlePersonAction,
} from "../../details/components/person-action-controls";
import type { PersonActionRefusal } from "../../details/components/person-action-text";
import { withdrawPersonMove } from "../server/actions";

import {
  goneMoveMessage,
  withdrawMoveDescription,
  withdrawMoveLabel,
  withdrawnMoveMessage,
} from "./move-text";

export function WithdrawMoveAction({
  personId,
  positionId,
  from,
  focusAfterWithdrawId,
}: {
  personId: string;
  positionId: string;
  from: string;
  focusAfterWithdrawId: string;
}) {
  const [pending, startWithdrawal] = useTransition();
  const [refusal, setRefusal] = useState<PersonActionRefusal>();

  const withdraw = () => {
    setRefusal(undefined);
    startWithdrawal(async () => {
      const outcome = await settlePersonAction(() => withdrawPersonMove(personId, positionId));
      if (!outcome.done) {
        setRefusal(outcome);
        return;
      }
      if (outcome.message === undefined) toast.success(withdrawnMoveMessage(from));
      else toast.info(goneMoveMessage(from), { description: outcome.message });
    });
  };

  return (
    <div className="grid justify-items-start gap-1">
      <ConfirmDialog
        trigger={
          <PersonActionButton
            pending={pending}
            icon={Undo2Icon}
            variant="ghost"
            size="sm"
            aria-label={withdrawMoveLabel(from)}
            data-testid="withdraw-move"
          >
            Withdraw
          </PersonActionButton>
        }
        title="Withdraw this move?"
        description={withdrawMoveDescription(from)}
        cancelLabel="Keep it"
        confirmLabel="Withdraw"
        tone="destructive"
        onConfirm={withdraw}
        focusAfterConfirm={() => document.getElementById(focusAfterWithdrawId)}
      />
      {refusal ? <PersonActionRefusalMessage refusal={refusal} testId="withdraw-move-error" /> : null}
    </div>
  );
}
