import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { ShieldCheckIcon } from "lucide-react";

import { Wordmark } from "@/shared/brand/wordmark";

import { SignInButton } from "./sign-in-button";

export function LoginCard() {
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
