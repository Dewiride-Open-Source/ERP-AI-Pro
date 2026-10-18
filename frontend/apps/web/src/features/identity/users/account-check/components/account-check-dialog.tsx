"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@dewiride/erp-ui/components/ui/dialog";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { UserSearchIcon } from "lucide-react";
import Link from "next/link";
import { unstable_isUnrecognizedActionError, unstable_rethrow } from "next/navigation";
import { useCallback, useLayoutEffect, useRef, useState, useTransition } from "react";

import { rejectedActionState } from "@/shared/forms/client/rejected-action";

import {
  PersonActionButton,
  PersonActionRefusalMessage,
  SavingDialogClose,
  SavingDialogCornerClose,
} from "../../details/components/person-action-controls";
import { useSavingDialog, useSavingReport } from "../../details/hooks/use-saving-dialog";
import { checkMicrosoftAccounts } from "../server/actions";

import {
  accountCheckDescription,
  accountCheckSummary,
  accountCheckTitle,
  checkingAccounts,
  deactivatedListLabel,
  deactivationReasonLabel,
  type AccountCheckAnswer,
  type AccountCheckResult,
} from "./account-check";

// The check changes records, so the dialog stays open while it runs and its answer always reaches the person who asked.
export function AccountCheckDialog() {
  const { open, saving, setOpen, setSaving, onOpenChange } = useSavingDialog();
  const close = useCallback(() => setOpen(false), [setOpen]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogTrigger asChild>
        <PersonActionButton
          pending={false}
          icon={UserSearchIcon}
          variant="outline"
          data-testid="account-check"
        >
          {accountCheckTitle}
        </PersonActionButton>
      </DialogTrigger>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg"
      >
        <DialogHeader>
          <DialogTitle>{accountCheckTitle}</DialogTitle>
          <DialogDescription>{accountCheckDescription}</DialogDescription>
        </DialogHeader>
        <AccountCheckSteps onLeave={close} onRunningChange={setSaving} />
        <SavingDialogCornerClose saving={saving} />
      </DialogContent>
    </Dialog>
  );
}

// The status line is on the page from the start, so a screen reader announces the progress and the result as they replace
// each other; the result takes focus because the Check button it replaces is gone. A refusal is removed before the next try,
// so the same words are announced again.
function AccountCheckSteps({
  onLeave,
  onRunningChange,
}: {
  onLeave: () => void;
  onRunningChange: (running: boolean) => void;
}) {
  const [running, startCheck] = useTransition();
  const [answer, setAnswer] = useState<AccountCheckAnswer>();
  const status = useRef<HTMLParagraphElement>(null);
  useSavingReport(running, onRunningChange);

  useLayoutEffect(() => {
    if (answer?.status === "checked") status.current?.focus();
  }, [answer]);

  const check = () => {
    setAnswer(undefined);
    startCheck(async () => {
      let next: AccountCheckAnswer;
      try {
        next = await checkMicrosoftAccounts();
      } catch (error) {
        unstable_rethrow(error);
        const failed = rejectedActionState(error, unstable_isUnrecognizedActionError);
        next = { status: "refused", message: failed.message, reference: failed.reference };
      }
      setAnswer(next);
    });
  };

  const result = answer?.status === "checked" ? answer : undefined;

  return (
    <div className="grid min-w-0 gap-4" data-testid="account-check-steps">
      <p
        ref={status}
        role="status"
        tabIndex={-1}
        className={cn(
          "flex min-h-5 items-center gap-2 rounded-sm text-sm outline-none",
          result === undefined && "text-muted-foreground",
        )}
        data-testid="account-check-status"
      >
        {running ? <Spinner aria-hidden="true" /> : null}
        {statusText(running, result)}
      </p>

      {result === undefined ? null : <DeactivatedPeople result={result} onLeave={onLeave} />}

      {answer?.status === "refused" ? (
        <PersonActionRefusalMessage
          refusal={{ done: false, message: answer.message, reference: answer.reference }}
          testId="account-check-error"
        />
      ) : null}

      <DialogFooter>
        {result === undefined ? (
          <>
            <SavingDialogClose saving={running} variant="outline">
              Cancel
            </SavingDialogClose>
            <PersonActionButton
              pending={running}
              icon={UserSearchIcon}
              onClick={check}
              data-testid="account-check-run"
            >
              Check
            </PersonActionButton>
          </>
        ) : (
          <DialogClose asChild>
            <Button type="button" variant="outline" data-testid="account-check-done">
              Done
            </Button>
          </DialogClose>
        )}
      </DialogFooter>
    </div>
  );
}

function statusText(running: boolean, result: AccountCheckResult | undefined): string {
  if (running) return checkingAccounts;
  return result === undefined ? "" : accountCheckSummary(result);
}

// A name leads to the person's page, so following it closes the dialog first, as leaving the page would.
function DeactivatedPeople({ result, onLeave }: { result: AccountCheckResult; onLeave: () => void }) {
  if (result.deactivated.length === 0) return null;

  return (
    <ul
      aria-label={deactivatedListLabel}
      className="grid min-w-0 gap-2"
      data-testid="account-check-deactivated"
    >
      {result.deactivated.map((person) => (
        <li
          key={person.id}
          className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1 rounded-lg border px-3 py-2.5 text-sm"
        >
          <Link
            href={`/identity/users/${person.id}`}
            onNavigate={onLeave}
            className="rounded-sm font-medium wrap-anywhere text-primary underline-offset-4 focus-ring hover:underline"
          >
            {person.displayName}
          </Link>
          <span className="text-muted-foreground">{deactivationReasonLabel(person.reason)}</span>
        </li>
      ))}
    </ul>
  );
}
