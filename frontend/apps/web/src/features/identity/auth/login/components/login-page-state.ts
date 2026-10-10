import { reasonParameter, signInReason, type SignInReason } from "@/shared/auth/sign-in-addresses";
import type { SearchParameters } from "@/shared/lists/list-query";

type SignInError = "sign-in-failed" | "account-deactivated";

export type LoginPageState = SignInError | SignInReason;

const signInErrors: readonly SignInError[] = ["sign-in-failed", "account-deactivated"];

const titles: Readonly<Record<LoginPageState, string>> = {
  "sign-in-failed": "Sign-in failed",
  "account-deactivated": "Account deactivated",
  "session-ended": "Session ended",
  "signed-out": "Signed out",
};

export function loginPageState(searchParameters: SearchParameters): LoginPageState | undefined {
  const error = signInErrors.find((known) => known === searchParameters.error);
  return error ?? signInReason(searchParameters[reasonParameter]);
}

export function loginPageTitle(searchParameters: SearchParameters): string {
  const state = loginPageState(searchParameters);
  return state === undefined ? "Sign in" : titles[state];
}
