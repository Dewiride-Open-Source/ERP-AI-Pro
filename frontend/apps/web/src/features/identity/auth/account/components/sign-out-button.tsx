"use client";

import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { LogOutIcon } from "lucide-react";
import { useRef, useState, type SubmitEvent } from "react";

import { requestTokenFieldName } from "@/shared/api/antiforgery";
import { browserRequestToken, renewBrowserAntiforgeryTokens } from "@/shared/api/browser-antiforgery";
import { signOutPath } from "@/shared/auth/sign-in-addresses";

export function SignOutButton({
  variant = "ghost",
  compact = false,
}: {
  variant?: "ghost" | "outline";
  compact?: boolean;
}) {
  const hydrated = useHydrated();
  const [pending, setPending] = useState(false);
  const tokenField = useRef<HTMLInputElement>(null);

  // The page posts the form itself, so the browser follows the redirects through Microsoft's sign-out back to this site. It
  // posts even when the session has already ended, because the browser's Microsoft sign-in outlives the session and only
  // that sign-out ends it. The request token is read as the form is sent, because a sign-in in another tab replaces it, and
  // fetched first when it is missing; an ended session gets none, and the API signs it out without one.
  const signOut = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (pending) return;
    const form = event.currentTarget;
    setPending(true);
    void (async () => {
      if (browserRequestToken() === undefined) await renewBrowserAntiforgeryTokens(() => undefined);
      if (tokenField.current !== null) tokenField.current.value = browserRequestToken() ?? "";
      form.submit();
    })();
  };

  return (
    <form method="post" action={signOutPath} onSubmit={signOut} className="grid">
      <input ref={tokenField} type="hidden" name={requestTokenFieldName} defaultValue="" />
      <SubmitButton pending={pending} ready={hydrated} variant={variant} size={compact ? "sm" : "default"}>
        {pending ? null : <LogOutIcon aria-hidden />}
        <span className={compact ? "sr-only sm:not-sr-only" : undefined}>Sign out</span>
      </SubmitButton>
    </form>
  );
}
