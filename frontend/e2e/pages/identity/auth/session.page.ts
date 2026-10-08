import type { Locator, Page } from "@playwright/test";

export class SessionDialogs {
  readonly idleWarning: Locator;
  readonly staySignedIn: Locator;
  readonly lifetimeWarning: Locator;
  readonly continueWorking: Locator;
  readonly ended: Locator;
  readonly signInAgain: Locator;

  constructor(page: Page) {
    this.idleWarning = page.getByRole("alertdialog", { name: "Are you still there?" });
    this.staySignedIn = this.idleWarning.getByRole("button", { name: "Stay signed in" });
    this.lifetimeWarning = page.getByRole("alertdialog", { name: "Your session ends soon" });
    this.continueWorking = this.lifetimeWarning.getByRole("button", { name: "Continue working" });
    this.ended = page.getByRole("alertdialog", { name: "Your session has ended" });
    this.signInAgain = this.ended.getByRole("link", { name: "Sign in again" });
  }
}
