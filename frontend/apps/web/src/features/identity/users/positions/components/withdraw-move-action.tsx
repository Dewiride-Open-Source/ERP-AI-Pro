"use client";

import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { Undo2Icon } from "lucide-react";
import { useLayoutEffect, useRef, useState, useTransition } from "react";
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

// Focus returns to the Withdraw button when the confirmation closes, so a refusal shows beside the button that has it; a
// withdrawn move leaves the list with its button, so focus moves to the list's heading as the button goes.
export function WithdrawMoveAction({
  personId,
  positionId,
  from,
  followsEarlierMove,
  focusAfterWithdrawId,
}: {
  personId: string;
  positionId: string;
  from: string;
  followsEarlierMove: boolean;
  focusAfterWithdrawId: string;
}) {
  const [pending, startWithdrawal] = useTransition();
  const [refusal, setRefusal] = useState<PersonActionRefusal>();
  const withdrawn = useRef(false);

  useLayoutEffect(
    () => () => {
      if (withdrawn.current) document.getElementById(focusAfterWithdrawId)?.focus();
    },
    [focusAfterWithdrawId],
  );

  const withdraw = () => {
    setRefusal(undefined);
    startWithdrawal(async () => {
      const outcome = await settlePersonAction(() => withdrawPersonMove(personId, positionId));
      if (!outcome.done) {
        setRefusal(outcome);
        return;
      }
      withdrawn.current = true;
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
        description={withdrawMoveDescription(from, { followsEarlierMove })}
        cancelLabel="Keep it"
        confirmLabel="Withdraw"
        tone="destructive"
        onConfirm={withdraw}
      />
      {refusal ? <PersonActionRefusalMessage refusal={refusal} testId="withdraw-move-error" /> : null}
    </div>
  );
}
