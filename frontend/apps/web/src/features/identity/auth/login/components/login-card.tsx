import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { ShieldCheckIcon } from "lucide-react";
import type { Route } from "next";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

import { sessionCookieName } from "@/shared/auth/page-access";
import { readSession } from "@/shared/auth/session";
import { localReturnPath, returnUrlParameter } from "@/shared/auth/sign-in-addresses";
import { Wordmark } from "@/shared/brand/wordmark";
import type { SearchParameters } from "@/shared/lists/list-query";

import { BrandPanel } from "./brand-panel";
import { loginPageState } from "./login-page-state";
import { SignInButton } from "./sign-in-button";
import { SignInNotice } from "./sign-in-notice";

const noticeId = "sign-in-notice";

export async function LoginCard({ searchParameters }: { searchParameters: SearchParameters }) {
  const returnPath = localReturnPath(searchParameters[returnUrlParameter]);
  const state = loginPageState(searchParameters);

  // A visitor whose session still holds has nothing to do here and goes straight on. Only the API can tell whether the
  // session cookie still names a session, so it is asked only when the browser sent one.
  if ((await cookies()).has(sessionCookieName) && (await readSession()).status === "signed-in") {
    redirect((returnPath ?? "/") as Route);
  }

  return (
    <div className="grid w-full max-w-5xl items-center gap-section lg:grid-cols-2">
      <Card className="mx-auto w-full max-w-md animate-fade-up border-border/60 bg-card/90 shadow-xl backdrop-blur">
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
          <SignInNotice id={noticeId} state={state} />
          <SignInButton returnPath={returnPath} describedBy={state === undefined ? undefined : noticeId} />
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
      <BrandPanel />
    </div>
  );
}
