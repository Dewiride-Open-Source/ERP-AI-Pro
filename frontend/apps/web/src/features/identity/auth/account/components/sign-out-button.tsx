"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { LogOutIcon } from "lucide-react";
import { useRef, useState, type SubmitEvent } from "react";

import { requestTokenFieldName } from "@/shared/api/antiforgery";
import { browserRequestToken, renewBrowserAntiforgeryTokens } from "@/shared/api/browser-antiforgery";
import { readSessionTimes } from "@/shared/api/session/session-watch";
import { loginHref, signOutPath } from "@/shared/auth/sign-in-addresses";

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

  // The page posts the form itself, so the browser follows the redirects through Microsoft's sign-out back to this site. The
  // request token is read as the form is sent, because a sign-in in another tab replaces it, and fetched again when it is
  // missing. A session that has already ended has nothing to sign out of, and the API would refuse the post, so the page goes
  // straight to the signed-out sign-in page instead.
  const signOut = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (pending) return;
    const form = event.currentTarget;
    setPending(true);
    void (async () => {
      if ((await readSessionTimes()).state === "ended") {
        window.location.assign(loginHref(undefined, "signed-out"));
        return;
      }
      if (browserRequestToken() === undefined) await renewBrowserAntiforgeryTokens(() => undefined);
      if (tokenField.current !== null) tokenField.current.value = browserRequestToken() ?? "";
      form.submit();
    })();
  };

  return (
    <form method="post" action={signOutPath} onSubmit={signOut} data-hydrating={hydrated ? undefined : ""}>
      <input ref={tokenField} type="hidden" name={requestTokenFieldName} defaultValue="" />
      <Button
        type="submit"
        variant={variant}
        size={compact ? "sm" : "default"}
        disabled={!hydrated || pending}
      >
        <LogOutIcon aria-hidden />
        <span className={compact ? "sr-only sm:not-sr-only" : undefined}>
          {pending ? "Signing out…" : "Sign out"}
        </span>
      </Button>
    </form>
  );
}
