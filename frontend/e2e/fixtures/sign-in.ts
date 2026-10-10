import { expect, type APIRequestContext, type BrowserContext, type Page } from "@playwright/test";

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

export const frontChannelSignOutPath = "/api/auth/signout-oidc";

export const microsoftSignInOrigin = "https://login.microsoftonline.com";

export const sessionCookie = "__Host-erp-session";

export const antiforgeryCookie = "__Host-erp-antiforgery";

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

// Microsoft reports a sign-out in another app through the front-channel request, which reaches the API without the browser's
// cookies, so the browser keeps a session cookie the API refuses from then on. The test's own request context sends it,
// because the browser context's one shares the browser's cookies. A test calls this only after the page's own renewal has
// been answered (sessionRenewed), or that renewal could reach the API after the end and be refused.
export async function endEntraSession(
  request: APIRequestContext,
  session: EntraSession | undefined,
): Promise<void> {
  expect(session, "the Microsoft Entra session of the signed-in person").toBeDefined();
  const query = new URLSearchParams({ iss: session?.issuer ?? "", sid: session?.id ?? "" });
  const answer = await request.get(`${frontChannelSignOutPath}?${query.toString()}`);
  expect(answer.status(), "the front-channel sign-out").toBe(200);
}

// A route sees only the first address of a redirect, so the sign-in's answer is fetched for the page and checked, and the
// page shows a stand-in instead of following it to Microsoft.
export async function answerMicrosoftSignIn(page: Page): Promise<() => URL | undefined> {
  let authorize: URL | undefined;
  await page.route(
    (url) => url.pathname === "/api/auth/login",
    async (route) => {
      const answer = await route.fetch({ maxRedirects: 0 });
      expect(answer.status(), "the sign-in's answer").toBe(302);
      authorize = new URL(answer.headers()["location"] ?? "");
      await route.fulfill({
        status: 200,
        contentType: "text/html",
        body: "<!doctype html><title>Microsoft sign-in</title>",
      });
    },
  );
  return () => authorize;
}

// Chromium and Firefox route only the first request of a redirect, and Playwright cannot answer an intercepted request with a
// redirect in WebKit, so the sign-out's own answer is fetched for the page, which applies its cookies, and the page receives
// in its place a document that moves on where Entra sends the browser once it has signed it out: the post-logout redirect
// address with the state the API gave it. Microsoft's end-session page is never reached from a test.
export async function answerEndSession(page: Page): Promise<() => URL | undefined> {
  let endSession: URL | undefined;
  await page.route("**/api/auth/logout", async (route) => {
    const answer = await route.fetch({ maxRedirects: 0 });
    expect(answer.status(), "the sign-out's answer").toBe(302);
    endSession = new URL(answer.headers()["location"] ?? "");
    const back = new URL(endSession.searchParams.get("post_logout_redirect_uri") ?? "");
    back.searchParams.set("state", endSession.searchParams.get("state") ?? "");
    await route.fulfill({
      status: 200,
      contentType: "text/html",
      body: `<!doctype html><title>Microsoft sign-out</title><script>location.replace(${JSON.stringify(back.toString())});</script>`,
    });
  });
  return () => endSession;
}
