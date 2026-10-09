"use client";

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@dewiride/erp-ui/components/ui/alert-dialog";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { readSessionTimes, renewSessionTimes, type SessionAnswer } from "@/shared/api/session/session-watch";
import { signInHref } from "@/shared/auth/sign-in-addresses";
import { formatTimeIst } from "@/shared/format/dates";
import { mainContentId } from "@/shared/layout/main-content";

import { SignOutButton } from "../../account/components/sign-out-button";

import {
  activityRenews,
  checkDue,
  dismissNotice,
  disposeKeeper,
  initialKeeperState,
  requestExchange,
  settleExchange,
  type ExchangeKind,
  type KeeperNotice,
  type KeeperState,
  type KeeperStep,
} from "./session-keeper-state";

type Shown = {
  readonly notice: KeeperNotice;
  readonly endsAt: number | undefined;
  readonly returnPath: string;
};

type Keeper = {
  readonly request: (kind: ExchangeKind) => void;
  readonly noteActivity: () => void;
  readonly dismiss: (kind: "idle" | "lifetime") => void;
  readonly current: () => KeeperState;
  readonly dispose: () => void;
};

const exchanges: Readonly<Record<ExchangeKind, () => Promise<SessionAnswer>>> = {
  check: readSessionTimes,
  renew: renewSessionTimes,
};

const saidNothing: SessionAnswer = { state: "unknown" };

function startKeeper(show: (notice: KeeperNotice) => void): Keeper {
  let state = initialKeeperState();
  let timer: number | undefined;

  function apply({ state: next, send }: KeeperStep): void {
    const previous = state;
    state = next;
    if (next.nextCheckAt !== previous.nextCheckAt) arm(next.nextCheckAt);
    if (next.notice !== previous.notice) show(next.notice);
    if (send !== undefined) void exchange(send);
  }

  function arm(at: number | undefined): void {
    window.clearTimeout(timer);
    timer =
      at === undefined
        ? undefined
        : window.setTimeout(() => apply(checkDue(state, Date.now())), Math.max(0, at - Date.now()));
  }

  async function exchange(kind: ExchangeKind): Promise<void> {
    const answer = await exchanges[kind]().catch(() => saidNothing);
    apply(settleExchange(state, answer, Date.now()));
  }

  function request(kind: ExchangeKind): void {
    apply(requestExchange(state, kind, Date.now()));
  }

  return {
    request,
    noteActivity: () => {
      if (activityRenews(state, Date.now())) request("renew");
    },
    dismiss: (kind) => apply({ state: dismissNotice(state, kind), send: undefined }),
    current: () => state,
    dispose: () => {
      state = disposeKeeper(state);
      arm(undefined);
    },
  };
}

// The session cookie slides only on answers the browser receives itself, so this keeps it alive while the person uses the
// page (a load, a navigation, a click or a key press, at most once a minute), warns before the idle timeout or the lifetime
// ends it, and once the API refuses it offers a new sign-in that returns to this page. Before warning and at the end it
// reads the session again, because another tab of the same browser renews the same cookie.
export function SessionKeeper() {
  const pathname = usePathname();
  const [shown, setShown] = useState<Shown>({ notice: { kind: "none" }, endsAt: undefined, returnPath: "/" });
  const keeper = useRef<Keeper | null>(null);
  const focusBeforeNotice = useRef<Element | null>(null);
  const signInAgain = useRef<HTMLAnchorElement>(null);
  const lifetimeContent = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // Each mount starts a keeper of its own and disposes it when it unmounts, so an exchange still running then arms no timer
    // and sends nothing more.
    const started = startKeeper((notice) =>
      setShown((previous) => ({
        notice,
        endsAt: notice.kind === "idle" || notice.kind === "lifetime" ? notice.endsAt : previous.endsAt,
        returnPath: `${window.location.pathname}${window.location.search}`,
      })),
    );
    keeper.current = started;
    const onActivity = () => started.noteActivity();
    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") started.request("check");
    };
    const listening = { capture: true, passive: true } as const;

    started.noteActivity();
    document.addEventListener("pointerdown", onActivity, listening);
    document.addEventListener("keydown", onActivity, listening);
    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => {
      started.dispose();
      if (keeper.current === started) keeper.current = null;
      document.removeEventListener("pointerdown", onActivity, listening);
      document.removeEventListener("keydown", onActivity, listening);
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, []);

  useEffect(() => {
    keeper.current?.noteActivity();
  }, [pathname]);

  const staySignedIn = () => {
    keeper.current?.dismiss("idle");
    keeper.current?.request("renew");
  };

  const continueWorking = () => keeper.current?.dismiss("lifetime");

  // Radix runs this before it moves focus into the warning, so the element the person was using is still the active one; a
  // warning that replaces another keeps what the first one found.
  const rememberFocus = () => {
    focusBeforeNotice.current ??= document.activeElement;
  };

  // Without a trigger Radix would return focus to nothing, and the button that held it leaves with the warning, so focus goes
  // back where the person was, or to the page's main content when that is gone. A notice that replaced the warning keeps the
  // focus it took.
  const restoreFocus = (event: Event) => {
    event.preventDefault();
    if (keeper.current?.current().notice.kind !== "none") return;
    const previous = focusBeforeNotice.current;
    focusBeforeNotice.current = null;
    const target =
      (previous instanceof HTMLElement || previous instanceof SVGElement) && previous.isConnected
        ? previous
        : document.getElementById(mainContentId);
    target?.focus({ preventScroll: true });
  };

  const { notice, returnPath } = shown;
  const endsAt = shown.endsAt === undefined ? "" : formatTimeIst(new Date(shown.endsAt));

  return (
    <>
      <AlertDialog
        open={notice.kind === "idle"}
        onOpenChange={(open) => {
          if (!open) staySignedIn();
        }}
      >
        <AlertDialogContent onOpenAutoFocus={rememberFocus} onCloseAutoFocus={restoreFocus}>
          <AlertDialogHeader>
            <AlertDialogTitle>Are you still there?</AlertDialogTitle>
            <AlertDialogDescription>
              Nothing has happened for a while, so you will be signed out at{" "}
              <span className="whitespace-nowrap">{endsAt}</span>. Stay signed in to keep working.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <SignOutButton />
            <AlertDialogCancel variant="default">Stay signed in</AlertDialogCancel>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog
        open={notice.kind === "lifetime"}
        onOpenChange={(open) => {
          if (!open) continueWorking();
        }}
      >
        <AlertDialogContent
          ref={lifetimeContent}
          onOpenAutoFocus={(event) => {
            rememberFocus();
            event.preventDefault();
            lifetimeContent.current?.focus();
          }}
          onCloseAutoFocus={restoreFocus}
        >
          <AlertDialogHeader>
            <AlertDialogTitle>Your session ends soon</AlertDialogTitle>
            <AlertDialogDescription>
              You have been signed in for as long as a session can last, so you will be signed out at{" "}
              <span className="whitespace-nowrap">{endsAt}</span>. Save your work before then; you can sign in
              again afterwards.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel variant="default">Continue working</AlertDialogCancel>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={notice.kind === "ended"}>
        <AlertDialogContent
          onEscapeKeyDown={(event) => event.preventDefault()}
          onOpenAutoFocus={(event) => {
            event.preventDefault();
            signInAgain.current?.focus();
          }}
        >
          <AlertDialogHeader>
            <AlertDialogTitle>Your session has ended</AlertDialogTitle>
            <AlertDialogDescription>
              For your security you have been signed out of the ERP. Sign in again to come back to this page,
              or sign out to also end your Microsoft sign-in on this device.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <SignOutButton />
            <AlertDialogAction asChild>
              <a ref={signInAgain} href={signInHref(returnPath)}>
                Sign in again
              </a>
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
