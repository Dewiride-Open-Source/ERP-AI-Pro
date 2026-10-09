import { useRef, useState, type SubmitEvent } from "react";

import { requestTokenFieldName } from "@/shared/api/antiforgery";
import { browserRequestToken, renewBrowserAntiforgeryTokens } from "@/shared/api/browser-antiforgery";
import { signOutPath } from "@/shared/auth/sign-in-addresses";

// The page posts the form itself, so the browser follows the redirects through Microsoft's sign-out back to this site. It
// posts even when the session has already ended, because the browser's Microsoft sign-in outlives the session and only
// that sign-out ends it. The request token is read as the form is sent, because a sign-in in another tab replaces it, and
// fetched first when it is missing; an ended session gets none, and the API signs it out without one.
export function useSignOutForm() {
  const [pending, setPending] = useState(false);
  const form = useRef<HTMLFormElement>(null);
  const tokenField = useRef<HTMLInputElement>(null);

  const onSubmit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (pending) return;
    const target = event.currentTarget;
    setPending(true);
    void (async () => {
      if (browserRequestToken() === undefined) await renewBrowserAntiforgeryTokens(() => undefined);
      if (tokenField.current !== null) tokenField.current.value = browserRequestToken() ?? "";
      target.submit();
    })();
  };

  return {
    pending,
    formProps: { ref: form, method: "post", action: signOutPath, onSubmit },
    tokenFieldProps: { ref: tokenField, type: "hidden", name: requestTokenFieldName, defaultValue: "" },
    signOut: () => form.current?.requestSubmit(),
  } as const;
}
