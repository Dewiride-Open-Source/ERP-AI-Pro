import { expect, type Locator, type Page } from "@playwright/test";

export const loginPath = "/login";

export const sessionEndedNotice = "Your session has ended. Sign in again to go back to the page you were on.";

export const signedOutNotice = "You have signed out.";

export type SignInReason = "session-ended" | "signed-out";

export function isSignInPage(
  origin: string,
  returnPath: string | undefined,
  reason: SignInReason | undefined,
): (url: URL) => boolean {
  return (url) =>
    url.origin === origin &&
    url.pathname === loginPath &&
    url.searchParams.get("returnUrl") === (returnPath ?? null) &&
    url.searchParams.get("reason") === (reason ?? null);
}

export class LoginPage {
  readonly heading: Locator;
  readonly signInButton: Locator;
  readonly wordmark: Locator;
  readonly glows: Locator;
  readonly failure: Locator;
  readonly notice: Locator;
  readonly brand: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Sign in to your workspace" });
    this.signInButton = page.getByTestId("sign-in-microsoft");
    this.wordmark = page.getByText("ERP-AI-Pro", { exact: true }).first();
    this.glows = page.locator(".animate-glow");
    this.failure = page.getByRole("main").getByRole("alert");
    this.notice = page.getByTestId("sign-in-notice");
    this.brand = page.getByTestId("sign-in-brand");
  }

  async goto(query = ""): Promise<void> {
    await this.page.goto(`${loginPath}${query}`);
    await expect(this.heading).toBeVisible();
  }
}
