"use client";

import { ErrorState } from "@dewiride/erp-ui/components/feedback/error-state";

import { mainContentId } from "./main-content";

export function ErrorPage({
  digest,
  onRetry,
  withinShell = false,
}: {
  digest: string | undefined;
  onRetry: () => void;
  withinShell?: boolean;
}) {
  // Inside the shell a retry that succeeds replaces this state, and its Try again button with it, so focus first moves to the
  // shell's main region, which stays; a retry that fails again renders this state anew, which focuses its heading.
  const retry = withinShell
    ? () => {
        document.getElementById(mainContentId)?.focus();
        onRetry();
      }
    : onRetry;

  const state = (
    <ErrorState
      headingLevel={1}
      focusOnMount
      title="We could not show this page"
      description="Something went wrong while preparing it. Try again, and if it keeps happening, share the reference with support."
      reference={digest}
      onRetry={retry}
    />
  );
  return withinShell ? state : <main className="flex min-h-dvh items-center justify-center">{state}</main>;
}
