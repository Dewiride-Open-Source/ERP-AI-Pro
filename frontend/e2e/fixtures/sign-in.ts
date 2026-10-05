import { expect, type APIRequestContext, type BrowserContext } from "@playwright/test";

export type Persona = "accountant" | "administrator";

export type SignedInPerson = {
  readonly id: string;
  readonly name: string;
  readonly userName: string;
  readonly roles: readonly string[];
};

export const signInPath = "/api/__test/sign-in";

export const requestTokenCookie = "__Host-erp-xsrf";

export const requestTokenHeader = "x-xsrf-token";

export async function signIn(request: APIRequestContext, persona: Persona): Promise<SignedInPerson> {
  const response = await request.post(`${signInPath}/${persona}`);
  expect(response.status(), `the test sign-in of the ${persona}`).toBe(200);
  return (await response.json()) as SignedInPerson;
}

export async function requestToken(context: BrowserContext): Promise<string> {
  const cookie = (await context.cookies()).find((candidate) => candidate.name === requestTokenCookie);
  expect(cookie, `the ${requestTokenCookie} cookie of the signed-in person`).toBeDefined();
  return decodeURIComponent(cookie?.value ?? "");
}
