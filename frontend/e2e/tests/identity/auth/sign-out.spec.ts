import type { Page } from "@playwright/test";

import {
  answerEndSession,
  endEntraSession,
  frontChannelSignOutPath,
  requestTokenCookie,
  sessionCookie,
} from "../../../fixtures/sign-in";
import { expect, test } from "../../../fixtures/test";
import {
  isSignInPage,
  LoginPage,
  sessionEndedNotice,
  signedOutNotice,
} from "../../../pages/identity/auth/login.page";
import { SessionDialogs, sessionRenewed } from "../../../pages/identity/auth/session.page";
import { attachmentsPath } from "../../../pages/platform/attachments/files.page";
import { StartPage } from "../../../pages/platform/home/start.page";

const signOutPath = "/api/auth/logout";

const antiforgeryRenewalPath = "/api/auth/antiforgery";

const endSessionEndpoint = /^https:\/\/login\.microsoftonline\.com\/[0-9a-f-]{36}\/oauth2\/v2\.0\/logout$/;

const originOf = (baseURL: string | undefined) => new URL(baseURL ?? "").origin;

async function holdAntiforgeryRenewal(page: Page): Promise<() => void> {
  let release: () => void = () => undefined;
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(
    (url) => url.pathname === antiforgeryRenewalPath,
    async (route) => {
      await released;
      await route.fallback();
    },
  );
  return release;
}

test.describe("signing out", () => {
  test("Sign out ends the session through Microsoft and lands on the signed-out sign-in page", async ({
    baseURL,
    context,
    page,
    person,
    request,
  }) => {
    const start = new StartPage(page);
    const login = new LoginPage(page);
    const endSession = await answerEndSession(page);
    const renewed = sessionRenewed(page);
    await start.goto();
    await renewed;
    const session = (await context.cookies()).find((cookie) => cookie.name === sessionCookie);
    expect(session, "the session cookie of the signed-in person").toBeDefined();

    await expect(start.shell.userMenuButton).toHaveAccessibleName(`Signed in as ${person?.name ?? ""}`);
    await start.shell.signOut();

    await expect(login.notice).toHaveText(signedOutNotice);
    await expect(page).toHaveURL(isSignInPage(originOf(baseURL), undefined, "signed-out"));
    const sentTo = endSession();
    expect(sentTo, "the end-session request").toBeDefined();
    expect(`${sentTo?.origin ?? ""}${sentTo?.pathname ?? ""}`).toMatch(endSessionEndpoint);
    expect(sentTo?.searchParams.get("post_logout_redirect_uri")).toBe(
      `${baseURL}/api/auth/signout-callback-oidc`,
    );
    expect(sentTo?.searchParams.get("logout_hint"), "the hint that spares the account question").toBeTruthy();

    const copy = await request.get("/api/auth/me", {
      headers: { cookie: `${sessionCookie}=${session?.value ?? ""}` },
    });
    expect(copy.status(), "a copy of the session cookie taken before the sign-out").toBe(401);
    await page.goto(attachmentsPath);
    await expect(login.heading).toBeVisible();
    await expect(page).toHaveURL(isSignInPage(originOf(baseURL), attachmentsPath, undefined));
  });

  test("Sign out fetches the request token first when the browser has none", async ({
    baseURL,
    context,
    page,
  }) => {
    const start = new StartPage(page);
    const sent: string[] = [];
    page.on("request", (request) => {
      const { pathname } = new URL(request.url());
      if (pathname === antiforgeryRenewalPath || pathname === signOutPath) {
        sent.push(`${request.method()} ${pathname}`);
      }
    });
    const endSession = await answerEndSession(page);
    const renewed = sessionRenewed(page);
    await start.goto();
    await renewed;
    await context.clearCookies({ name: requestTokenCookie });

    await start.shell.signOut();

    await expect(new LoginPage(page).notice).toHaveText(signedOutNotice);
    await expect(page).toHaveURL(isSignInPage(originOf(baseURL), undefined, "signed-out"));
    expect(sent).toEqual([`GET ${antiforgeryRenewalPath}`, `POST ${signOutPath}`]);
    expect(
      endSession()?.searchParams.get("logout_hint"),
      "the sign-out of the signed-in session",
    ).toBeTruthy();
  });

  test("the user menu keeps the person's name where only the initials show and offers an enabled Sign out", async ({
    page,
    person,
  }) => {
    const start = new StartPage(page);
    const { shell } = start;
    await start.goto();

    await expect(shell.userMenuButton).toBeVisible();
    await expect(shell.userMenuButton).toHaveAccessibleName(`Signed in as ${person?.name ?? ""}`);
    await shell.openUserMenu();
    await expect(shell.signOutItem).toBeEnabled();
    await expect(shell.signOutItem).toHaveAccessibleName("Sign out");
    await page.keyboard.press("Escape");
    await expect(shell.userMenu).toBeHidden();
  });

  test("while the sign-out is under way the user menu stays shut and its button says it is busy", async ({
    baseURL,
    context,
    page,
  }) => {
    const start = new StartPage(page);
    const { shell } = start;
    const endSession = await answerEndSession(page);
    const renewed = sessionRenewed(page);
    await start.goto();
    await renewed;
    await context.clearCookies({ name: requestTokenCookie });
    const release = await holdAntiforgeryRenewal(page);

    await shell.signOut();
    await expect(shell.userMenuButton).toHaveAttribute("aria-busy", "true");
    await expect(shell.userMenuButton).toHaveAttribute("aria-disabled", "true");
    await expect(shell.userMenuButton).toBeFocused();
    for (const key of ["Enter", "ArrowDown"]) {
      await page.keyboard.press(key);
      await expect(shell.userMenuButton, `the menu after ${key}`).toHaveAttribute("aria-expanded", "false");
      await expect(shell.userMenu, `the menu after ${key}`).toHaveCount(0);
    }
    release();

    await expect(new LoginPage(page).notice).toHaveText(signedOutNotice);
    await expect(page).toHaveURL(isSignInPage(originOf(baseURL), undefined, "signed-out"));
    expect(endSession(), "the end-session request").toBeDefined();
  });

  test.describe("when Microsoft reports a sign-out from another app", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 401/ });

    test("the open page offers a new sign-in, and Sign out there still ends the Microsoft sign-in", async ({
      baseURL,
      entraSession,
      page,
      request,
    }) => {
      const dialogs = new SessionDialogs(page);
      const endSession = await answerEndSession(page);
      const renewed = sessionRenewed(page);
      await new StartPage(page).goto();
      await renewed;

      const query = new URLSearchParams({ iss: entraSession?.issuer ?? "", sid: entraSession?.id ?? "" });
      const notice = await request.get(`${frontChannelSignOutPath}?${query.toString()}`);

      expect(notice.status()).toBe(200);
      expect(await notice.text()).toBe("");
      await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));
      await expect(dialogs.ended).toBeVisible();

      await dialogs.endedSignOut.click();

      await expect(new LoginPage(page).notice).toHaveText(signedOutNotice);
      await expect(page).toHaveURL(isSignInPage(originOf(baseURL), undefined, "signed-out"));
      const sentTo = endSession();
      expect(`${sentTo?.origin ?? ""}${sentTo?.pathname ?? ""}`).toMatch(endSessionEndpoint);
      expect(sentTo?.searchParams.get("post_logout_redirect_uri")).toBe(
        `${baseURL}/api/auth/signout-callback-oidc`,
      );
      expect(sentTo?.searchParams.get("logout_hint"), "a sign-out that no session signs in").toBeNull();
    });
  });

  test.describe("once Microsoft has ended this browser's session", () => {
    test("a page opened with the refused session cookie goes to the sign-in page and comes back to it", async ({
      baseURL,
      entraSession,
      page,
      request,
    }) => {
      const login = new LoginPage(page);
      await endEntraSession(request, entraSession);

      for (const returnPath of [`${attachmentsPath}?page=2`, "/design/form-kit"]) {
        await page.goto(returnPath);

        await expect(login.heading).toBeVisible();
        await expect(page).toHaveURL(isSignInPage(originOf(baseURL), returnPath, "session-ended"));
        await expect(login.notice).toHaveText(sessionEndedNotice);
      }

      await page.goto("/login");
      await expect(login.heading).toBeVisible();
      await expect(page).toHaveURL(isSignInPage(originOf(baseURL), undefined, undefined));
      await expect(login.notice).toHaveCount(0);
    });
  });

  test("a sign-out for another session or from another issuer changes nothing", async ({
    entraSession,
    page,
    request,
  }) => {
    await new StartPage(page).goto();

    for (const query of [
      `iss=${encodeURIComponent(entraSession?.issuer ?? "")}&sid=${crypto.randomUUID()}`,
      `iss=${encodeURIComponent("https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0")}&sid=${encodeURIComponent(entraSession?.id ?? "")}`,
    ]) {
      const notice = await request.get(`${frontChannelSignOutPath}?${query}`);
      expect(notice.status(), query).toBe(200);
    }

    expect((await page.request.get("/api/auth/me")).status()).toBe(200);
  });

  test("a front-channel request without the session it names is refused", async ({ request }) => {
    const response = await request.get(
      `${frontChannelSignOutPath}?iss=${encodeURIComponent("https://example.com")}`,
    );

    expect(response.status()).toBe(400);
    expect(await response.json()).toMatchObject({ code: "request.invalid" });
  });
});
