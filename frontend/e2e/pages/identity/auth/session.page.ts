import type { Locator, Page, Request, Response } from "@playwright/test";

export const sessionPath = "/api/auth/session";

export function isSessionRequest(request: Request, method: "GET" | "POST"): boolean {
  return request.method() === method && new URL(request.url()).pathname === sessionPath;
}

export function sessionRenewed(page: Page): Promise<Response> {
  return page.waitForResponse((response) => isSessionRequest(response.request(), "POST"));
}

export class SessionDialogs {
  readonly idleWarning: Locator;
  readonly staySignedIn: Locator;
  readonly idleSignOut: Locator;
  readonly lifetimeWarning: Locator;
  readonly continueWorking: Locator;
  readonly ended: Locator;
  readonly signInAgain: Locator;
  readonly endedSignOut: Locator;

  constructor(page: Page) {
    this.idleWarning = page.getByRole("alertdialog", { name: "Are you still there?" });
    this.staySignedIn = this.idleWarning.getByRole("button", { name: "Stay signed in" });
    this.idleSignOut = this.idleWarning.getByRole("button", { name: "Sign out" });
    this.lifetimeWarning = page.getByRole("alertdialog", { name: "Your session ends soon" });
    this.continueWorking = this.lifetimeWarning.getByRole("button", { name: "Continue working" });
    this.ended = page.getByRole("alertdialog", { name: "Your session has ended" });
    this.signInAgain = this.ended.getByRole("link", { name: "Sign in again" });
    this.endedSignOut = this.ended.getByRole("button", { name: "Sign out" });
  }
}
