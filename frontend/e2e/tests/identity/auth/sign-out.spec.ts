import type { Page } from "@playwright/test";

import { expect, test } from "../../../fixtures/test";
import { LoginPage } from "../../../pages/identity/auth/login.page";
import { SessionDialogs } from "../../../pages/identity/auth/session.page";
import { attachmentsPath } from "../../../pages/platform/attachments/files.page";
import { StartPage } from "../../../pages/platform/home/start.page";

const frontChannelPath = "/api/auth/signout-oidc";

const endSessionEndpoint = /^https:\/\/login\.microsoftonline\.com\/[0-9a-f-]{36}\/oauth2\/v2\.0\/logout$/;

// A route sees only the first address of a redirect, so the sign-out's own answer is fetched for the page, which applies its
// cookies, and the page is sent where Entra sends the browser once it has signed it out: the post-logout redirect address
// with the state the API gave it. Microsoft's end-session page is never reached from a test.
async function answerEndSession(page: Page): Promise<() => URL | undefined> {
  let endSession: URL | undefined;
  await page.route("**/api/auth/logout", async (route) => {
    const answer = await route.fetch({ maxRedirects: 0 });
    expect(answer.status(), "the sign-out's answer").toBe(302);
    endSession = new URL(answer.headers()["location"] ?? "");
    const back = new URL(endSession.searchParams.get("post_logout_redirect_uri") ?? "");
    back.searchParams.set("state", endSession.searchParams.get("state") ?? "");
    await route.fulfill({ status: 302, headers: { location: back.toString() } });
  });
  return () => endSession;
}

test.describe("signing out", () => {
  test("Sign out ends the session through Microsoft and lands on the signed-out sign-in page", async ({
    baseURL,
    page,
    person,
  }) => {
    const start = new StartPage(page);
    const login = new LoginPage(page);
    const endSession = await answerEndSession(page);
    await start.goto();

    await expect(start.shell.account).toContainText(person?.name ?? "");
    await start.shell.signOut.click();

    await expect(login.notice).toHaveText("You have signed out.");
    await expect(page).toHaveURL((url) => url.pathname === "/login" && url.search === "?reason=signed-out");
    const sentTo = endSession();
    expect(sentTo, "the end-session request").toBeDefined();
    expect(`${sentTo?.origin ?? ""}${sentTo?.pathname ?? ""}`).toMatch(endSessionEndpoint);
    expect(sentTo?.searchParams.get("post_logout_redirect_uri")).toBe(
      `${baseURL}/api/auth/signout-callback-oidc`,
    );
    expect(sentTo?.searchParams.get("logout_hint"), "the hint that spares the account question").toBeTruthy();

    expect((await page.request.get("/api/auth/me")).status()).toBe(401);
    await page.goto(attachmentsPath);
    await expect(login.heading).toBeVisible();
    await expect(page).toHaveURL((url) => url.searchParams.get("returnUrl") === attachmentsPath);
  });

  test("the sign-out control keeps its name when only its icon shows", async ({ page }) => {
    const start = new StartPage(page);
    await start.goto();

    await expect(start.shell.signOut).toBeVisible();
    await expect(start.shell.signOut).toBeEnabled();
    await expect(start.shell.signOut).toHaveAccessibleName("Sign out");
  });

  test.describe("when Microsoft reports a sign-out from another app", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 401/ });

    test("the session of this browser ends and the open page offers a new sign-in", async ({
      entraSession,
      page,
      request,
    }) => {
      const dialogs = new SessionDialogs(page);
      await new StartPage(page).goto();

      const notice = await request.get(
        `${frontChannelPath}?iss=${encodeURIComponent(entraSession?.issuer ?? "")}&sid=${encodeURIComponent(entraSession?.id ?? "")}`,
      );

      expect(notice.status()).toBe(200);
      expect(await notice.text()).toBe("");
      expect((await page.request.get("/api/auth/me")).status()).toBe(401);
      await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));
      await expect(dialogs.ended).toBeVisible();
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
      const notice = await request.get(`${frontChannelPath}?${query}`);
      expect(notice.status(), query).toBe(200);
    }

    expect((await page.request.get("/api/auth/me")).status()).toBe(200);
  });

  test("a front-channel request without the session it names is refused", async ({ request }) => {
    const response = await request.get(
      `${frontChannelPath}?iss=${encodeURIComponent("https://example.com")}`,
    );

    expect(response.status()).toBe(400);
    expect(await response.json()).toMatchObject({ code: "request.invalid" });
  });
});
