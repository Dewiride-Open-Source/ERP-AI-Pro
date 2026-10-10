import { Alert, AlertDescription } from "@dewiride/erp-ui/components/ui/alert";
import { CircleAlertIcon, CircleCheckIcon, ClockIcon } from "lucide-react";

import type { LoginPageState } from "./login-page-state";

// A failed or refused sign-in needs the person to act, so it is an alert; the other two confirm what already happened, so
// they are a status.
export function SignInNotice({ id, state }: { id: string; state: LoginPageState | undefined }) {
  switch (state) {
    case "sign-in-failed":
      return (
        <Alert id={id} variant="destructive">
          <CircleAlertIcon aria-hidden />
          <AlertDescription>
            We could not sign you in. Try again, or ask your administrator for access.
          </AlertDescription>
        </Alert>
      );
    case "account-deactivated":
      return (
        <Alert id={id} variant="destructive">
          <CircleAlertIcon aria-hidden />
          <AlertDescription>
            Your account has been deactivated, so you cannot sign in. Ask your administrator if you need
            access again.
          </AlertDescription>
        </Alert>
      );
    case "session-ended":
      return (
        <Alert id={id} role="status" data-testid="sign-in-notice">
          <ClockIcon aria-hidden />
          <AlertDescription>
            Your session has ended. Sign in again to go back to the page you were on.
          </AlertDescription>
        </Alert>
      );
    case "signed-out":
      return (
        <Alert id={id} role="status" data-testid="sign-in-notice">
          <CircleCheckIcon aria-hidden />
          <AlertDescription>You have signed out.</AlertDescription>
        </Alert>
      );
    case undefined:
      return null;
  }
}
