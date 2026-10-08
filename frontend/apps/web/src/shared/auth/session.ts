import "server-only";

import { redirect } from "next/navigation";
import { cache } from "react";

import { callApiAllowingSignedOut } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";

import { currentPagePath } from "./page-path";
import { loginHref } from "./sign-in-addresses";

export type SignedInPerson = {
  readonly id: string;
  readonly name: string;
  readonly userName: string;
  readonly roles: readonly string[];
};

export type Session =
  | { readonly status: "signed-in"; readonly person: SignedInPerson }
  | { readonly status: "signed-out" }
  | { readonly status: "unavailable" };

export type HeldSession = Exclude<Session, { readonly status: "signed-out" }>;

export const readSession = cache(async (): Promise<Session> => {
  try {
    const me = await callApiAllowingSignedOut((client) => client.api.auth.me.get());
    return {
      status: "signed-in",
      person: { id: me.id ?? "", name: me.name ?? "", userName: me.userName ?? "", roles: me.roles ?? [] },
    };
  } catch (error) {
    return error instanceof ApiError && error.status === 401
      ? { status: "signed-out" }
      : { status: "unavailable" };
  }
});

// The first statement of every Server Function and the check of every page in the (app) group: a visitor without a session
// goes to the sign-in page and comes back here afterwards, while an API that cannot be reached leaves the page to show what
// it can, as it does for every other read.
export async function requireSession(): Promise<HeldSession> {
  const session = await readSession();
  if (session.status === "signed-out") redirect(loginHref(await currentPagePath(), "session-ended"));
  return session;
}
