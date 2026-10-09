import { reasonParameter, signInReason, type SignInReason } from "@/shared/auth/sign-in-addresses";
import type { SearchParameters } from "@/shared/lists/list-query";

export type LoginPageState = "sign-in-failed" | SignInReason;

const signInFailedError = "sign-in-failed";

const titles: Readonly<Record<LoginPageState, string>> = {
  "sign-in-failed": "Sign-in failed",
  "session-ended": "Session ended",
  "signed-out": "Signed out",
};

export function loginPageState(searchParameters: SearchParameters): LoginPageState | undefined {
  if (searchParameters.error === signInFailedError) return "sign-in-failed";
  return signInReason(searchParameters[reasonParameter]);
}

export function loginPageTitle(searchParameters: SearchParameters): string {
  const state = loginPageState(searchParameters);
  return state === undefined ? "Sign in" : titles[state];
}
