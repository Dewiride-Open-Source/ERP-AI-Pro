import type { Page, Request } from "@playwright/test";

import {
  answerEndSession,
  answerMicrosoftSignIn,
  requestToken,
  requestTokenCookie,
  requestTokenHeader,
} from "../../../fixtures/sign-in";
import { expect, forEachTheme, signedInApi, test } from "../../../fixtures/test";
import { isSignInPage, LoginPage, signedOutNotice } from "../../../pages/identity/auth/login.page";
import {
  isSessionRequest,
  SessionDialogs,
  sessionPath,
  sessionRenewed,
} from "../../../pages/identity/auth/session.page";
import { StartPage } from "../../../pages/platform/home/start.page";

const minute = 60 * 1000;

const antiforgeryRenewalPath = "/api/auth/antiforgery";

type SessionTimes = { readonly expiresAt: Date; readonly lifetimeEndsAt: Date };

type SessionAnswer = SessionTimes | "refused";

const clockStart = Date.parse("2026-10-01T09:00:00.000Z");

const onTheClock = (minutes: number, seconds = 0) => new Date(clockStart + minutes * minute + seconds * 1000);

const fromNow = (milliseconds: number) => new Date(Date.now() + milliseconds);

// The page learns how long the session has left only from these answers, so a test decides it here instead of waiting the
// minutes a real session takes to run out. The answers carry no Date header, so the page reads the times on its own clock,
// which is the clock a test installs.
async function answerSession(page: Page, answer: () => SessionAnswer): Promise<Request[]> {
  const renewals: Request[] = [];
  await page.route(`**${sessionPath}`, async (route) => {
    if (isSessionRequest(route.request(), "POST")) renewals.push(route.request());
    const times = answer();
    if (times === "refused") {
      await route.fulfill({
        status: 401,
        contentType: "application/problem+json",
        body: JSON.stringify({
          type: "/problems/request.unauthenticated",
          code: "request.unauthenticated",
          status: 401,
        }),
      });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        expiresAt: times.expiresAt.toISOString(),
        lifetimeEndsAt: times.lifetimeEndsAt.toISOString(),
      }),
    });
  });
  return renewals;
}

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
    await page.keyboard.press("Shift");
    await expect.poll(() => renewals.length, { message: "renewals after a minute of work" }).toBe(2);
  });

  forEachTheme(
    "warns two minutes before an idle session ends and keeps it when the person stays",
    async ({ page, capture }) => {
      const start = new StartPage(page);
      const dialogs = new SessionDialogs(page);
      await page.clock.install({ time: clockStart });
      let times: SessionTimes = { expiresAt: onTheClock(30), lifetimeEndsAt: onTheClock(12 * 60) };
      const renewals = await answerSession(page, () => times);
      const renewed = sessionRenewed(page);
      await start.goto();
      await renewed;
      await start.shell.wordmarkLink.focus();

      await page.clock.pauseAt(onTheClock(27, 59));
      await expect(dialogs.idleWarning).toBeHidden();
      await page.clock.fastForward(2000);

      await expect(dialogs.idleWarning).toBeVisible();
      await expect(dialogs.idleWarning).toContainText(/you will be signed out at 3:00\spm\sIST\./i);
      await expect(dialogs.staySignedIn).toBeFocused();
      await expect(dialogs.idleWarning).toMatchAriaSnapshot(`
        - alertdialog "Are you still there?":
          - heading "Are you still there?" [level=2]
          - paragraph: /Nothing has happened for a while/
          - button "Sign out"
          - button "Stay signed in"
      `);
      await page.clock.resume();
      await capture("session-idle-warning");

      times = { expiresAt: onTheClock(60), lifetimeEndsAt: times.lifetimeEndsAt };
      await dialogs.staySignedIn.click();

      await expect(dialogs.idleWarning).toBeHidden();
      await expect.poll(() => renewals.length).toBe(2);
      expect(renewals.at(-1)?.headers()[requestTokenHeader]).toBeTruthy();
      await expect(start.shell.wordmarkLink, "the control the person used before the warning").toBeFocused();
    },
  );

  test("Escape on the idle warning keeps the session too and returns focus where it was", async ({
    page,
  }) => {
    const start = new StartPage(page);
    const dialogs = new SessionDialogs(page);
    await page.clock.install({ time: clockStart });
    let times: SessionTimes = { expiresAt: onTheClock(30), lifetimeEndsAt: onTheClock(12 * 60) };
    const renewals = await answerSession(page, () => times);
    const renewed = sessionRenewed(page);
    await start.goto();
    await renewed;
    const openSystem = start.openLink("System");
    await openSystem.focus();

    await page.clock.pauseAt(onTheClock(28, 1));
    await expect(dialogs.idleWarning).toBeVisible();
    times = { expiresAt: onTheClock(60), lifetimeEndsAt: times.lifetimeEndsAt };
    await page.keyboard.press("Escape");

    await expect(dialogs.idleWarning).toBeHidden();
    await page.clock.resume();
    await expect.poll(() => renewals.length).toBe(2);
    await expect(openSystem).toBeFocused();
  });

  forEachTheme(
    "tells once, five minutes before, that the session reaches the longest a session lasts",
    async ({ page, capture }) => {
      const start = new StartPage(page);
      const dialogs = new SessionDialogs(page);
      await page.clock.install({ time: clockStart });
      const end = onTheClock(30);
      const renewals = await answerSession(page, () => ({ expiresAt: end, lifetimeEndsAt: end }));
      const renewed = sessionRenewed(page);
      await start.goto();
      await renewed;

      await page.clock.pauseAt(onTheClock(24, 59));
      await expect(dialogs.lifetimeWarning).toBeHidden();
      await page.clock.fastForward(2000);

      await expect(dialogs.lifetimeWarning).toBeVisible();
      await expect(
        dialogs.lifetimeWarning,
        "the notice itself, so a key press cannot dismiss it unread",
      ).toBeFocused();
      await expect(dialogs.lifetimeWarning).toContainText(/you will be signed out at 3:00\spm\sIST\./i);
      await expect(dialogs.lifetimeWarning).toMatchAriaSnapshot(`
        - alertdialog "Your session ends soon":
          - heading "Your session ends soon" [level=2]
          - paragraph: /as long as a session can last, so you will be signed out at .* Save your work before then/
          - button "Continue working"
      `);
      await page.keyboard.press("Tab");
      await expect(dialogs.continueWorking).toBeFocused();
      await page.clock.resume();
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

  test("Escape on the lifetime notice closes it and returns focus where it was", async ({ page }) => {
    const start = new StartPage(page);
    const dialogs = new SessionDialogs(page);
    await page.clock.install({ time: clockStart });
    const end = onTheClock(30);
    await answerSession(page, () => ({ expiresAt: end, lifetimeEndsAt: end }));
    const renewed = sessionRenewed(page);
    await start.goto();
    await renewed;
    const openAttachments = start.openLink("Attachments");
    await openAttachments.focus();

    await page.clock.pauseAt(onTheClock(25, 1));
    await expect(dialogs.lifetimeWarning).toBeVisible();
    await page.keyboard.press("Escape");

    await expect(dialogs.lifetimeWarning).toBeHidden();
    await page.clock.resume();
    await expect(openAttachments).toBeFocused();
  });

  // A page that is already leaving cannot be read, so the sign-out is held where it still waits on the page: the request token
  // it fetches first, because the browser has none.
  test("Sign out in the idle warning keeps the focus and shows it is busy while the browser leaves", async ({
    baseURL,
    context,
    page,
  }) => {
    const dialogs = new SessionDialogs(page);
    await answerSession(page, () => ({
      expiresAt: fromNow(90 * 1000),
      lifetimeEndsAt: fromNow(12 * 60 * minute),
    }));
    let release: () => void = () => undefined;
    const released = new Promise<void>((resolve) => {
      release = resolve;
    });
    await page.route(`**${antiforgeryRenewalPath}`, async (route) => {
      await released;
      await route.continue();
    });
    const endSession = await answerEndSession(page);
    await page.goto("/");
    await expect(dialogs.idleWarning).toBeVisible();
    await context.clearCookies({ name: requestTokenCookie });

    await dialogs.idleSignOut.focus();
    await page.keyboard.press("Enter");

    await expect(dialogs.idleSignOut).toHaveAttribute("aria-busy", "true");
    await expect(dialogs.idleSignOut).toHaveAttribute("aria-disabled", "true");
    await expect(dialogs.idleSignOut).toHaveAccessibleName("Sign out");
    await expect(dialogs.idleSignOut).toBeFocused();
    release();
    await expect(new LoginPage(page).notice).toHaveText(signedOutNotice);
    await expect(page).toHaveURL(isSignInPage(new URL(baseURL ?? "").origin, undefined, "signed-out"));
    expect(
      endSession()?.searchParams.get("logout_hint"),
      "the hint of the session that signed out",
    ).toBeTruthy();
  });

  test.describe("when the API refuses the renewal's request token", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 400/ });

    test("renews the antiforgery pair once and sends the renewal again with the new token", async ({
      context,
      page,
    }) => {
      const sent: string[] = [];
      page.on("request", (request) => {
        const { pathname } = new URL(request.url());
        if (pathname === sessionPath || pathname === antiforgeryRenewalPath) {
          sent.push(`${request.method()} ${pathname}`);
        }
      });
      let refused = false;
      await page.route(`**${sessionPath}`, async (route) => {
        if (refused || !isSessionRequest(route.request(), "POST")) {
          await route.fallback();
          return;
        }
        refused = true;
        await route.fulfill({
          status: 400,
          contentType: "application/problem+json",
          body: JSON.stringify({
            type: "/problems/antiforgery.token-invalid",
            code: "antiforgery.token-invalid",
            status: 400,
          }),
        });
      });
      const tokenBefore = await requestToken(context);
      const resent = page.waitForResponse(
        (response) => isSessionRequest(response.request(), "POST") && response.status() === 200,
      );

      await new StartPage(page).goto();

      const renewal = await resent;
      expect(sent).toEqual([`POST ${sessionPath}`, `GET ${antiforgeryRenewalPath}`, `POST ${sessionPath}`]);
      const tokenAfter = await requestToken(context);
      expect(tokenAfter, "the renewed request token").not.toBe(tokenBefore);
      expect(renewal.request().headers()[requestTokenHeader]).toBe(tokenAfter);
    });
  });

  test.describe("once the session has ended", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 401/ });

    test("an idle page shows the end as soon as the API refuses the session", async ({ page }) => {
      const dialogs = new SessionDialogs(page);
      await page.clock.install({ time: clockStart });
      let answer: SessionAnswer = { expiresAt: onTheClock(30), lifetimeEndsAt: onTheClock(12 * 60) };
      await answerSession(page, () => answer);
      const renewed = sessionRenewed(page);
      await new StartPage(page).goto();
      await renewed;

      await page.clock.pauseAt(onTheClock(28, 1));
      await expect(dialogs.idleWarning).toBeVisible();
      answer = "refused";
      await page.clock.fastForward("02:05");

      await expect(dialogs.ended).toBeVisible();
      await expect(dialogs.idleWarning).toBeHidden();
      await expect(dialogs.signInAgain).toBeFocused();
    });

    forEachTheme("offers a new sign-in that comes back to the same page", async ({ page, capture }) => {
      const dialogs = new SessionDialogs(page);
      const returnPath = "/design/data-table?page=2";
      const renewed = sessionRenewed(page);
      await page.goto(returnPath);
      await expect(page.getByRole("main").getByRole("heading", { level: 1 })).toBeVisible();
      await renewed;

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
      await expect(dialogs.ended).toHaveAttribute("data-state", "open");
      await expect(dialogs.ended).toMatchAriaSnapshot(`
          - alertdialog "Your session has ended":
            - heading "Your session has ended" [level=2]
            - paragraph: /Sign in again to come back to this page, or sign out to also end your Microsoft sign-in/
            - button "Sign out"
            - link "Sign in again"
        `);
      await capture("session-ended");

      const authorize = await answerMicrosoftSignIn(page);
      const signIn = page.waitForRequest((request) => new URL(request.url()).pathname === "/api/auth/login");
      await dialogs.signInAgain.click();
      expect(new URL((await signIn).url()).searchParams.get("returnUrl")).toBe(returnPath);
      await expect(page).toHaveTitle("Microsoft sign-in");
      expect(authorize()?.hostname, "where the sign-in sends the browser").toBe("login.microsoftonline.com");
      expect(authorize()?.pathname).toMatch(/\/oauth2\/v2\.0\/authorize$/);
    });
  });
});
