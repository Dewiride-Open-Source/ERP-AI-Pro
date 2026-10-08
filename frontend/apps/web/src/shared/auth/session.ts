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
  | { readonly status: "unavailable"; readonly failure: unknown };

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
      : { status: "unavailable", failure: error };
  }
});

// The check of every render in the (app) group: a visitor without a session goes to the sign-in page and comes back here
// afterwards, while a render whose API cannot answer shows what it can, because every read it makes is refused without a
// session anyway.
export async function requireSession(): Promise<HeldSession> {
  const session = await readSession();
  if (session.status === "signed-out") redirect(loginHref(await currentPagePath(), "session-ended"));
  return session;
}

// The first statement of every Server Function: a Server Function changes something, so it runs only for a person the API
// has just confirmed, and a session that cannot be read refuses the call with the failure of that read.
export async function requireSignedInPerson(): Promise<SignedInPerson> {
  const session = await requireSession();
  if (session.status === "unavailable") throw session.failure;
  return session.person;
}
