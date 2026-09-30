import { Alert, AlertDescription } from "@dewiride/erp-ui/components/ui/alert";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { CircleAlertIcon, ShieldCheckIcon } from "lucide-react";

import { Wordmark } from "@/shared/brand/wordmark";
import type { SearchParameters } from "@/shared/lists/list-query";

import { SignInButton } from "./sign-in-button";

const signInFailedError = "sign-in-failed";

export function LoginCard({ searchParameters }: { searchParameters: SearchParameters }) {
  return (
    <Card className="w-full max-w-md animate-fade-up border-border/60 bg-card/90 shadow-xl backdrop-blur">
      <CardHeader className="gap-3">
        <Wordmark className="text-base" />
        <CardTitle className="text-2xl tracking-tight">
          <h1>Sign in to your workspace</h1>
        </CardTitle>
        <CardDescription>
          Use your Dewiride Microsoft account. There is nothing else to remember.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        {searchParameters.error === signInFailedError ? (
          <Alert variant="destructive">
            <CircleAlertIcon aria-hidden />
            <AlertDescription>
              We could not sign you in. Try again, or ask your administrator for access.
            </AlertDescription>
          </Alert>
        ) : null}
        <SignInButton />
        <p className="flex items-start gap-2 text-xs leading-relaxed text-muted-foreground">
          <ShieldCheckIcon className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          <span>Single sign-on through Microsoft Entra ID. Your session stays on this device only.</span>
        </p>
      </CardContent>
      <CardFooter className="justify-between text-xs text-muted-foreground">
        <span>Private workspace</span>
        <span>English</span>
      </CardFooter>
    </Card>
  );
}
