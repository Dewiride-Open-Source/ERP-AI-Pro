import { Alert, AlertDescription } from "@dewiride/erp-ui/components/ui/alert";
import { CircleAlertIcon, CircleCheckIcon, ClockIcon } from "lucide-react";

import type { SignInReason } from "@/shared/auth/sign-in-addresses";

// A failed sign-in needs the person's attention at once; the other two only confirm what already happened, so they are
// announced politely.
export function SignInNotice({ failed, reason }: { failed: boolean; reason: SignInReason | undefined }) {
  if (failed) {
    return (
      <Alert variant="destructive">
        <CircleAlertIcon aria-hidden />
        <AlertDescription>
          We could not sign you in. Try again, or ask your administrator for access.
        </AlertDescription>
      </Alert>
    );
  }

  switch (reason) {
    case "session-ended":
      return (
        <Alert role="status" data-testid="sign-in-notice">
          <ClockIcon aria-hidden />
          <AlertDescription>
            Your session has ended. Sign in again to carry on where you left off.
          </AlertDescription>
        </Alert>
      );
    case "signed-out":
      return (
        <Alert role="status" data-testid="sign-in-notice">
          <CircleCheckIcon aria-hidden />
          <AlertDescription>You have signed out.</AlertDescription>
        </Alert>
      );
    case undefined:
      return null;
  }
}
