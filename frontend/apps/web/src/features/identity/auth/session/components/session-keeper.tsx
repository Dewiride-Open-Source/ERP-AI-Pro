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

import { SignOutButton } from "../../account/components/sign-out-button";

import { endCheckDelay, planSession, renewalDue } from "./session-timing";

type Notice =
  | { readonly kind: "none" }
  | { readonly kind: "idle"; readonly endsAt: Date }
  | { readonly kind: "lifetime"; readonly endsAt: Date }
  | { readonly kind: "ended"; readonly returnPath: string };

type Watch = {
  lastRenewedAt: number | undefined;
  busy: boolean;
  ended: boolean;
  lifetimeNoticeSeen: boolean;
  notice: Notice["kind"];
  timers: number[];
};

const quiet: Notice = { kind: "none" };

const unknownRetryDelay = 30 * 1000;

// The session cookie slides only on answers the browser receives itself, so this keeps it alive while the person uses the
// page (a load, a navigation, a click or a key press, at most once a minute), warns before the idle timeout or the lifetime
// ends it, and once the API refuses it offers a new sign-in that returns to this page. Before warning and at the end it
// reads the session again, because another tab of the same browser renews the same cookie.
export function SessionKeeper() {
  const pathname = usePathname();
  const [notice, setNotice] = useState<Notice>(quiet);
  const signInAgain = useRef<HTMLAnchorElement>(null);
  const watch = useRef<Watch>({
    lastRenewedAt: undefined,
    busy: false,
    ended: false,
    lifetimeNoticeSeen: false,
    notice: "none",
    timers: [],
  });

  const actions = useRef({
    show(next: Notice) {
      watch.current.notice = next.kind;
      setNotice(next);
    },

    clearTimers() {
      for (const timer of watch.current.timers) window.clearTimeout(timer);
      watch.current.timers = [];
    },

    at(time: number, run: () => void) {
      watch.current.timers.push(window.setTimeout(run, Math.max(0, time - Date.now())));
    },

    apply(answer: SessionAnswer) {
      const current = watch.current;
      if (current.ended) return;
      if (answer.state === "unknown") {
        actions.current.at(Date.now() + unknownRetryDelay, () => void actions.current.check());
        return;
      }
      actions.current.clearTimers();
      if (answer.state === "ended") {
        current.ended = true;
        actions.current.show({
          kind: "ended",
          returnPath: `${window.location.pathname}${window.location.search}`,
        });
        return;
      }

      const plan = planSession(answer.times);
      const endsAt = new Date(plan.endsAt + answer.times.clockOffset);
      const now = Date.now();
      if (now < plan.warnAt) {
        if (current.notice !== "none") actions.current.show(quiet);
        actions.current.at(plan.warnAt, () => void actions.current.check());
      } else if (plan.extendable) {
        actions.current.show({ kind: "idle", endsAt });
      } else if (!current.lifetimeNoticeSeen) {
        actions.current.show({ kind: "lifetime", endsAt });
      }
      // The API counts the end to the second, so a check that still finds the session waits a second before the next one.
      actions.current.at(Math.max(plan.endsAt, now) + endCheckDelay, () => void actions.current.check());
    },

    async exchange(request: () => Promise<SessionAnswer>) {
      const current = watch.current;
      if (current.busy || current.ended) return;
      current.busy = true;
      try {
        actions.current.apply(await request());
      } finally {
        current.busy = false;
      }
    },

    async check() {
      await actions.current.exchange(readSessionTimes);
    },

    async renew() {
      watch.current.lastRenewedAt = Date.now();
      await actions.current.exchange(renewSessionTimes);
    },

    noteActivity() {
      const current = watch.current;
      if (current.notice === "none" && renewalDue(current.lastRenewedAt, Date.now()))
        void actions.current.renew();
    },
  });

  useEffect(() => {
    const keeper = actions.current;
    const onActivity = () => keeper.noteActivity();
    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") void keeper.check();
    };
    const listening = { capture: true, passive: true } as const;

    keeper.noteActivity();
    document.addEventListener("pointerdown", onActivity, listening);
    document.addEventListener("keydown", onActivity, listening);
    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => {
      keeper.clearTimers();
      document.removeEventListener("pointerdown", onActivity, listening);
      document.removeEventListener("keydown", onActivity, listening);
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, []);

  useEffect(() => {
    actions.current.noteActivity();
  }, [pathname]);

  const staySignedIn = () => {
    actions.current.show(quiet);
    void actions.current.renew();
  };

  const continueWorking = () => {
    watch.current.lifetimeNoticeSeen = true;
    actions.current.show(quiet);
  };

  return (
    <>
      <AlertDialog
        open={notice.kind === "idle"}
        onOpenChange={(open) => {
          if (!open) staySignedIn();
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Are you still there?</AlertDialogTitle>
            <AlertDialogDescription>
              Nothing has happened for a while, so you will be signed out at{" "}
              {notice.kind === "idle" ? formatTimeIst(notice.endsAt) : ""}. Stay signed in to keep working.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <SignOutButton variant="outline" />
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
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Your session ends soon</AlertDialogTitle>
            <AlertDialogDescription>
              You have been signed in for as long as a session can last, so you will be signed out at{" "}
              {notice.kind === "lifetime" ? formatTimeIst(notice.endsAt) : ""}. Save your work, then sign in
              again to carry on.
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
              For your security you have been signed out. Sign in again to come back to this page.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogAction asChild>
              <a ref={signInAgain} href={signInHref(notice.kind === "ended" ? notice.returnPath : undefined)}>
                Sign in again
              </a>
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
