import { expect, type APIRequestContext, type BrowserContext } from "@playwright/test";

export type Persona = "accountant" | "administrator";

export type SignedInPerson = {
  readonly id: string;
  readonly name: string;
  readonly userName: string;
  readonly roles: readonly string[];
};

export type EntraSession = { readonly id: string; readonly issuer: string };

export type SignIn = { readonly person: SignedInPerson; readonly entraSession: EntraSession };

type PersonaSignInAnswer = SignedInPerson & { readonly entraSessionId: string; readonly issuer: string };

export const signInPath = "/api/__test/sign-in";

export const sessionCookie = "__Host-erp-session";

export const requestTokenCookie = "__Host-erp-xsrf";

export const requestTokenHeader = "x-xsrf-token";

export async function signIn(request: APIRequestContext, persona: Persona): Promise<SignIn> {
  const response = await request.post(`${signInPath}/${persona}`);
  expect(response.status(), `the test sign-in of the ${persona}`).toBe(200);
  const { entraSessionId, issuer, ...person } = (await response.json()) as PersonaSignInAnswer;
  return { person, entraSession: { id: entraSessionId, issuer } };
}

export async function requestToken(context: BrowserContext): Promise<string> {
  const cookie = (await context.cookies()).find((candidate) => candidate.name === requestTokenCookie);
  expect(cookie, `the ${requestTokenCookie} cookie of the signed-in person`).toBeDefined();
  return decodeURIComponent(cookie?.value ?? "");
}

// The web origin without an API cannot sign anyone in, and proxy.ts only checks that a session cookie is present, so a cookie
// no API would accept lets its pages render the way they do for a person whose API went away.
export async function addUnverifiedSessionCookie(context: BrowserContext, origin: string): Promise<void> {
  await context.addCookies([
    { name: sessionCookie, value: "unverified", url: origin, secure: true, httpOnly: true, sameSite: "Lax" },
  ]);
}
