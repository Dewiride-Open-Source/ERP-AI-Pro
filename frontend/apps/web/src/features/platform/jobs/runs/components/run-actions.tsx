"use client";

import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { CircleStopIcon, RotateCcwIcon, type LucideIcon } from "lucide-react";
import { unstable_isUnrecognizedActionError, unstable_rethrow } from "next/navigation";
import { useEffect, useLayoutEffect, useRef, useState, useTransition, type ComponentProps } from "react";
import { toast } from "sonner";

import { rejectedActionState } from "@/shared/forms/client/rejected-action";

import { cancelJobRun, rerunJobRun } from "../server/actions";

import {
  cancelDone,
  cancelQuestion,
  canCancel,
  canRerun,
  rerunDone,
  rerunQuestion,
  type RunActionOutcome,
  type RunActionRefusal,
  type RunChoice,
  type RunQuestion,
} from "./run-text";

export interface RunActionRights {
  readonly rerun: boolean;
  readonly cancel: boolean;
}

export interface RunActionSubject extends RunChoice {
  readonly id: string;
  readonly jobLabel: string;
  readonly maxAttempts: number | undefined;
}

// Every action changes what the run can do next, so the button that did it leaves the page once the action is done; as it
// goes, focus moves to the element focusAfterId names instead of falling to the page.
export function RunActions({
  run,
  rights,
  focusAfterId,
  align,
}: {
  run: RunActionSubject;
  rights: RunActionRights;
  focusAfterId: string;
  align: "start" | "end";
}) {
  const offersRerun = rights.rerun && canRerun(run);
  const offersCancel = rights.cancel && canCancel(run);
  if (!offersRerun && !offersCancel) return null;

  return (
    <div
      className={cn("flex flex-wrap items-start gap-2", align === "end" ? "justify-end" : "justify-start")}
      data-testid="job-run-actions"
    >
      {offersRerun ? (
        <RunAction
          label="Run again"
          accessibleName={`Run again: ${run.jobLabel}`}
          icon={RotateCcwIcon}
          question={rerunQuestion(run.jobLabel, run.maxAttempts)}
          send={() => rerunJobRun(run.id)}
          done={() => rerunDone(run.jobLabel)}
          focusAfterId={focusAfterId}
          align={align}
          testId="job-run-rerun"
        />
      ) : null}
      {offersCancel ? (
        <RunAction
          label="Cancel"
          accessibleName={`Cancel: ${run.jobLabel}`}
          icon={CircleStopIcon}
          question={cancelQuestion(run.jobLabel, run.status)}
          tone="destructive"
          send={() => cancelJobRun(run.id)}
          done={(stopRequested) => cancelDone(run.jobLabel, stopRequested)}
          focusAfterId={focusAfterId}
          align={align}
          testId="job-run-cancel"
        />
      ) : null}
    </div>
  );
}

// The confirmation returns focus to the button as it closes, so a refusal shows beside the button that has it; the refusal is
// cleared when the action is asked for again, so a repeated refusal is announced again. A refusal widens a table's actions
// column, which can move the button and the refusal out of view inside a table that scrolls sideways, so each new refusal is
// scrolled into view. After a success, focus moves on only as the button leaves, because the closing confirmation can return
// focus to the button after the API has answered.
function RunAction({
  label,
  accessibleName,
  icon,
  question,
  tone = "default",
  send,
  done,
  focusAfterId,
  align,
  testId,
}: {
  label: string;
  accessibleName: string;
  icon: LucideIcon;
  question: RunQuestion;
  tone?: "default" | "destructive" | undefined;
  send: () => Promise<RunActionOutcome>;
  done: (stopRequested: boolean) => string;
  focusAfterId: string;
  align: "start" | "end";
  testId: string;
}) {
  const [pending, startAction] = useTransition();
  const [refusal, setRefusal] = useState<RunActionRefusal>();
  const refusalRef = useRef<HTMLParagraphElement>(null);
  const succeeded = useRef(false);

  useLayoutEffect(
    () => () => {
      if (succeeded.current) document.getElementById(focusAfterId)?.focus();
    },
    [focusAfterId],
  );

  useEffect(() => {
    if (refusal !== undefined) refusalRef.current?.scrollIntoView({ block: "nearest", inline: "nearest" });
  }, [refusal]);

  const confirm = () => {
    setRefusal(undefined);
    startAction(async () => {
      const outcome = await settle(send);
      if (!outcome.done) {
        setRefusal(outcome);
        return;
      }
      succeeded.current = true;
      toast.success(done(outcome.stopRequested));
    });
  };

  return (
    <div className={cn("grid gap-1", align === "end" ? "justify-items-end" : "justify-items-start")}>
      <ConfirmDialog
        trigger={
          <RunActionButton pending={pending} icon={icon} aria-label={accessibleName} data-testid={testId}>
            {label}
          </RunActionButton>
        }
        title={question.title}
        description={question.description}
        cancelLabel={question.keep}
        confirmLabel={question.confirm}
        tone={tone}
        onConfirm={confirm}
      />
      {refusal === undefined ? null : (
        <p
          ref={refusalRef}
          role="alert"
          className="w-72 max-w-full text-sm whitespace-normal text-destructive"
          data-testid={`${testId}-error`}
        >
          {refusal.message}
          {refusal.reference === undefined ? null : (
            <>
              {" "}
              Reference: <span className="font-mono break-all">{refusal.reference}</span>
            </>
          )}
        </p>
      )}
    </div>
  );
}

async function settle(send: () => Promise<RunActionOutcome>): Promise<RunActionOutcome> {
  try {
    return await send();
  } catch (error) {
    unstable_rethrow(error);
    const failed = rejectedActionState(error, unstable_isUnrecognizedActionError);
    return { done: false, message: failed.message, reference: failed.reference };
  }
}

// The button opens a dialog, so it stays disabled until the page is interactive, when the dialog can open; while the action
// it confirmed runs, it keeps focus and refuses a second one.
function RunActionButton({
  pending,
  icon: Icon,
  className,
  onClick,
  children,
  ...props
}: Omit<ComponentProps<typeof Button>, "type" | "disabled" | "variant" | "size"> & {
  pending: boolean;
  icon: LucideIcon;
}) {
  const hydrated = useHydrated();

  return (
    <Button
      {...props}
      type="button"
      variant="outline"
      size="sm"
      disabled={!hydrated}
      aria-disabled={pending || undefined}
      aria-busy={pending || undefined}
      data-hydrating={hydrated ? undefined : ""}
      className={cn("aria-disabled:opacity-50", className)}
      onClick={(event) => {
        if (pending) {
          event.preventDefault();
          return;
        }
        onClick?.(event);
      }}
    >
      {pending ? (
        <Spinner data-icon="inline-start" aria-hidden="true" />
      ) : (
        <Icon data-icon="inline-start" aria-hidden />
      )}
      {children}
    </Button>
  );
}
