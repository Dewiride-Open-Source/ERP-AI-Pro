import { expect, type Locator, type Page } from "@playwright/test";

export class LoginPage {
  readonly heading: Locator;
  readonly signInButton: Locator;
  readonly wordmark: Locator;
  readonly glows: Locator;
  readonly failure: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { name: "Sign in to your workspace" });
    this.signInButton = page.getByTestId("sign-in-microsoft");
    this.wordmark = page.getByText("ERP-AI-Pro", { exact: true }).first();
    this.glows = page.locator(".animate-glow");
    this.failure = page.getByRole("main").getByRole("alert");
  }

  async goto(query = ""): Promise<void> {
    await this.page.goto(`/login${query}`);
    await expect(this.heading).toBeVisible();
  }
}
