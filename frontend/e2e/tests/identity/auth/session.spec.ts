import type { Page, Request } from "@playwright/test";

import { requestTokenHeader } from "../../../fixtures/sign-in";
import { expect, forEachTheme, signedInApi, test } from "../../../fixtures/test";
import { SessionDialogs } from "../../../pages/identity/auth/session.page";
import { StartPage } from "../../../pages/platform/home/start.page";

const sessionPath = "/api/auth/session";

const minute = 60 * 1000;

type SessionTimes = { readonly expiresAt: Date; readonly lifetimeEndsAt: Date };

function isSessionRequest(request: Request, method: "GET" | "POST"): boolean {
  return request.method() === method && new URL(request.url()).pathname === sessionPath;
}

// The page learns how long the session has left only from these answers, so a test decides it here instead of waiting the
// minutes a real session takes to run out.
async function answerSession(page: Page, times: () => SessionTimes): Promise<Request[]> {
  const renewals: Request[] = [];
  await page.route(`**${sessionPath}`, async (route) => {
    if (isSessionRequest(route.request(), "POST")) renewals.push(route.request());
    const { expiresAt, lifetimeEndsAt } = times();
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      headers: { date: new Date().toUTCString() },
      body: JSON.stringify({
        expiresAt: expiresAt.toISOString(),
        lifetimeEndsAt: lifetimeEndsAt.toISOString(),
      }),
    });
  });
  return renewals;
}

const fromNow = (milliseconds: number) => new Date(Date.now() + milliseconds);

test.describe("session", () => {
  test("renews the session from the page while the person works, at most once a minute", async ({ page }) => {
    await page.clock.install();
    const renewals: Request[] = [];
    page.on("request", (request) => {
      if (isSessionRequest(request, "POST")) renewals.push(request);
    });

    await new StartPage(page).goto();
    await expect.poll(() => renewals.length, { message: "the renewal of the page load" }).toBe(1);
    const renewal = await renewals[0]?.response();
    expect(renewal?.status()).toBe(200);
    expect(renewals[0]?.headers()[requestTokenHeader], "the request token of the renewal").toBeTruthy();
    const { expiresAt } = (await renewal?.json()) as { expiresAt: string };
    expect(Date.parse(expiresAt)).toBeGreaterThan(Date.now());

    await page.clock.fastForward(30 * 1000);
    await page.getByRole("main").getByRole("heading", { level: 1 }).click();
    await page.keyboard.press("Shift");
    await page.clock.fastForward(31 * 1000);
    await page.getByRole("main").getByRole("heading", { level: 1 }).click();
    await expect.poll(() => renewals.length, { message: "renewals after a minute of work" }).toBe(2);
  });

  forEachTheme(
    "warns before an idle session ends and keeps it when the person stays",
    async ({ page, capture }) => {
      const dialogs = new SessionDialogs(page);
      let times: SessionTimes = { expiresAt: fromNow(90 * 1000), lifetimeEndsAt: fromNow(12 * 60 * minute) };
      const renewals = await answerSession(page, () => times);

      await page.goto("/");

      await expect(dialogs.idleWarning).toBeVisible();
      await expect(dialogs.idleWarning).toContainText(/you will be signed out at \d{1,2}:\d{2}\s?[ap]m/i);
      await expect(dialogs.staySignedIn).toBeFocused();
      await expect(dialogs.idleWarning).toMatchAriaSnapshot(`
        - alertdialog "Are you still there?":
          - heading "Are you still there?" [level=2]
          - paragraph: /Nothing has happened for a while/
          - button "Sign out"
          - button "Stay signed in"
      `);
      await capture("session-idle-warning");

      times = { expiresAt: fromNow(30 * minute), lifetimeEndsAt: times.lifetimeEndsAt };
      const renewed = renewals.length;
      await dialogs.staySignedIn.click();

      await expect(dialogs.idleWarning).toBeHidden();
      await expect.poll(() => renewals.length).toBe(renewed + 1);
      expect(renewals.at(-1)?.headers()[requestTokenHeader]).toBeTruthy();
    },
  );

  test("Escape on the idle warning keeps the session too", async ({ page }) => {
    const dialogs = new SessionDialogs(page);
    let times: SessionTimes = { expiresAt: fromNow(60 * 1000), lifetimeEndsAt: fromNow(12 * 60 * minute) };
    const renewals = await answerSession(page, () => times);
    await page.goto("/");
    await expect(dialogs.idleWarning).toBeVisible();

    times = { expiresAt: fromNow(30 * minute), lifetimeEndsAt: times.lifetimeEndsAt };
    const renewed = renewals.length;
    await page.keyboard.press("Escape");

    await expect(dialogs.idleWarning).toBeHidden();
    await expect.poll(() => renewals.length).toBe(renewed + 1);
  });

  forEachTheme(
    "warns once before the session reaches the longest a session lasts",
    async ({ page, capture }) => {
      const dialogs = new SessionDialogs(page);
      const end = fromNow(4 * minute);
      const renewals = await answerSession(page, () => ({ expiresAt: end, lifetimeEndsAt: end }));

      await page.goto("/");

      await expect(dialogs.lifetimeWarning).toBeVisible();
      await expect(dialogs.lifetimeWarning).toContainText("as long as a session can last");
      await expect(dialogs.continueWorking).toBeFocused();
      await capture("session-lifetime-warning");

      await dialogs.continueWorking.click();
      await expect(dialogs.lifetimeWarning).toBeHidden();

      const checked = page.waitForResponse((response) => isSessionRequest(response.request(), "GET"));
      await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));
      await checked;
      await page.evaluate(
        () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))),
      );
      await expect(dialogs.lifetimeWarning).toBeHidden();
      expect(renewals.length, "renewals of a session at the end of its lifetime").toBe(1);
    },
  );

  test.describe("once the session has ended", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 401/ });

    forEachTheme("offers a new sign-in that comes back to the same page", async ({ page, capture }) => {
      const dialogs = new SessionDialogs(page);
      const returnPath = "/design/data-table?page=2";
      await page.goto(returnPath);
      await expect(page.getByRole("main").getByRole("heading", { level: 1 })).toBeVisible();

      const signOut = await signedInApi(page.context()).post("/api/auth/logout", { maxRedirects: 0 });
      expect(signOut.status()).toBe(302);
      await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));

      await expect(dialogs.ended).toBeVisible();
      await expect(dialogs.signInAgain).toBeFocused();
      await expect(dialogs.signInAgain).toHaveAttribute(
        "href",
        `/api/auth/login?returnUrl=${encodeURIComponent(returnPath)}`,
      );
      await page.keyboard.press("Escape");
      await expect(dialogs.ended).toBeVisible();
      await expect(dialogs.ended).toMatchAriaSnapshot(`
          - alertdialog "Your session has ended":
            - heading "Your session has ended" [level=2]
            - paragraph: /Sign in again to come back to this page/
            - link "Sign in again"
        `);
      await capture("session-ended");

      // A route sees only the first address of a redirect, so the sign-in's answer is fetched for the page and checked, and
      // the page shows a stand-in instead of following it to Microsoft.
      let authorize: URL | undefined;
      await page.route(
        (url) => url.pathname === "/api/auth/login",
        async (route) => {
          const answer = await route.fetch({ maxRedirects: 0 });
          authorize = new URL(answer.headers()["location"] ?? "");
          await route.fulfill({
            status: 200,
            contentType: "text/html",
            body: "<!doctype html><title>Microsoft sign-in</title>",
          });
        },
      );
      const signIn = page.waitForRequest((request) => new URL(request.url()).pathname === "/api/auth/login");
      await dialogs.signInAgain.click();
      expect(new URL((await signIn).url()).searchParams.get("returnUrl")).toBe(returnPath);
      await expect(page).toHaveTitle("Microsoft sign-in");
      expect(authorize?.hostname, "where the sign-in sends the browser").toBe("login.microsoftonline.com");
      expect(authorize?.pathname).toMatch(/\/oauth2\/v2\.0\/authorize$/);
    });
  });
});
