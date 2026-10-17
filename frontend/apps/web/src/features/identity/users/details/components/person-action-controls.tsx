"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { DialogClose } from "@dewiride/erp-ui/components/ui/dialog";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { XIcon, type LucideIcon } from "lucide-react";
import { unstable_isUnrecognizedActionError, unstable_rethrow } from "next/navigation";
import type { ComponentProps } from "react";

import { rejectedActionState } from "@/shared/forms/client/rejected-action";

import type { PersonActionOutcome, PersonActionRefusal } from "./person-action-text";

export async function settlePersonAction(
  action: () => Promise<PersonActionOutcome>,
): Promise<PersonActionOutcome> {
  try {
    return await action();
  } catch (error) {
    unstable_rethrow(error);
    const failed = rejectedActionState(error, unstable_isUnrecognizedActionError);
    return { done: false, message: failed.message, reference: failed.reference };
  }
}

// The button opens a dialog, so it stays disabled until the page is interactive, when the dialog can open; while the action
// it confirms runs, it keeps focus and refuses a second one.
export function PersonActionButton({
  pending,
  icon: Icon,
  className,
  onClick,
  children,
  ...props
}: Omit<ComponentProps<typeof Button>, "type" | "disabled"> & { pending: boolean; icon: LucideIcon }) {
  const hydrated = useHydrated();

  return (
    <Button
      {...props}
      type="button"
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

export function SavingDialogClose({
  saving,
  className,
  children,
  ...props
}: Omit<ComponentProps<typeof Button>, "type" | "asChild"> & { saving: boolean }) {
  return (
    <DialogClose asChild>
      <Button
        {...props}
        type="button"
        aria-disabled={saving || undefined}
        className={cn("aria-disabled:opacity-50", className)}
      >
        {children}
      </Button>
    </DialogClose>
  );
}

export function SavingDialogCornerClose({ saving }: { saving: boolean }) {
  return (
    <SavingDialogClose saving={saving} variant="ghost" size="icon-sm" className="absolute top-2 right-2">
      <XIcon aria-hidden />
      <span className="sr-only">Close</span>
    </SavingDialogClose>
  );
}

// The confirmation dialog has closed by the time the API answers, so a refusal is shown beside the button that asked for
// the action, and announced as it appears.
export function PersonActionRefusalMessage({
  refusal,
  testId,
}: {
  refusal: PersonActionRefusal;
  testId: string;
}) {
  return (
    <p role="alert" className="text-sm text-destructive" data-testid={testId}>
      {refusal.message}
      {refusal.reference === undefined ? null : (
        <>
          {" "}
          Reference: <span className="font-mono break-all">{refusal.reference}</span>
        </>
      )}
    </p>
  );
}
