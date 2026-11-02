"use client";

import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { CircleStopIcon, RotateCcwIcon, type LucideIcon } from "lucide-react";
import { unstable_isUnrecognizedActionError, unstable_rethrow } from "next/navigation";
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  useTransition,
  type ComponentProps,
} from "react";
import { toast } from "sonner";

import { rejectedActionState } from "@/shared/forms/client/rejected-action";

import { cancelJobRun, rerunJobRun } from "../server/actions";

import {
  cancelDone,
  cancelOutcome,
  cancelQuestion,
  canCancel,
  canRerun,
  rerunDone,
  rerunQuestion,
  runActionName,
  type RunActionOutcome,
  type RunActionRefusal,
  type RunQuestion,
  type RunSubject,
} from "./run-text";

export interface RunActionRights {
  readonly rerun: boolean;
  readonly cancel: boolean;
}

export interface RunActionSubject extends RunSubject {
  readonly id: string;
}

type RunActionFocus =
  | { readonly focusAfterId: string; readonly onLeave?: never }
  | { readonly onLeave: (runId: string) => void; readonly focusAfterId?: never };

// An action changes what the run can do next, so the button that did it leaves once the action is done, unless the run can
// still be cancelled: a running run of a job this version of the ERP does not have keeps Cancel, and Cancel keeps focus. A
// button leaves before the refreshed page is drawn, so as it goes, focus moves to the element focusAfterId names, or onLeave
// hears which run's button left and a list focuses what it shows of the run once its refreshed rows are drawn.
export function RunActions({
  run,
  rights,
  align,
  focusAfterId,
  onLeave,
}: {
  run: RunActionSubject;
  rights: RunActionRights;
  align: "start" | "end";
} & RunActionFocus) {
  const { id } = run;
  const leave = useCallback(() => {
    if (onLeave !== undefined) onLeave(id);
    else if (focusAfterId !== undefined) document.getElementById(focusAfterId)?.focus();
  }, [focusAfterId, onLeave, id]);
  const offersRerun = rights.rerun && canRerun(run);
  const offersCancel = rights.cancel && canCancel(run);
  if (!offersRerun && !offersCancel) return null;

  return (
    <div
      className={cn("flex flex-wrap items-start gap-2", align === "end" ? "justify-end" : "justify-start")}
      data-run-actions={id}
      data-testid="job-run-actions"
    >
      {offersRerun ? (
        <RunAction
          label="Run again"
          accessibleName={runActionName("Run again", run)}
          icon={RotateCcwIcon}
          question={rerunQuestion(run)}
          send={() => rerunJobRun(id)}
          done={() => rerunDone(run.jobLabel)}
          leave={leave}
          align={align}
          testId="job-run-rerun"
        />
      ) : null}
      {offersCancel ? (
        <RunAction
          label="Cancel"
          accessibleName={runActionName("Cancel", run)}
          icon={CircleStopIcon}
          question={cancelQuestion(run)}
          tone="destructive"
          send={async () => cancelOutcome(await cancelJobRun(id), run.stopRequested)}
          done={(stopRequested) => cancelDone(run.jobLabel, stopRequested)}
          stays={(stopRequested) => stopRequested && canCancel({ ...run, status: "running", stopRequested })}
          leave={leave}
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
// focus to the button after the API has answered; a button the outcome leaves on the page keeps focus.
function RunAction({
  label,
  accessibleName,
  icon,
  question,
  tone = "default",
  send,
  done,
  stays = neverStays,
  leave,
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
  stays?: ((stopRequested: boolean) => boolean) | undefined;
  leave: () => void;
  align: "start" | "end";
  testId: string;
}) {
  const [pending, startAction] = useTransition();
  const [refusal, setRefusal] = useState<RunActionRefusal>();
  const refusalRef = useRef<HTMLParagraphElement>(null);
  const leaving = useRef(false);

  useLayoutEffect(
    () => () => {
      if (leaving.current) leave();
    },
    [leave],
  );

  useEffect(() => {
    if (refusal !== undefined) refusalRef.current?.scrollIntoView({ block: "nearest", inline: "nearest" });
  }, [refusal]);

  const confirm = () => {
    setRefusal(undefined);
    leaving.current = false;
    startAction(async () => {
      const outcome = await settle(send);
      if (!outcome.done) {
        setRefusal(outcome);
        return;
      }
      leaving.current = !stays(outcome.stopRequested);
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
          className="w-full max-w-72 text-sm whitespace-normal text-destructive"
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

function neverStays(): boolean {
  return false;
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
