import { randomUUID } from "node:crypto";

import { addUnverifiedSessionCookie } from "../../../fixtures/sign-in";
import { offlineBaseURL } from "../../../fixtures/targets";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import { ApprovalKindsPage } from "../../../pages/platform/approvals/approval-kinds.page";
import { ApprovalRequestPage } from "../../../pages/platform/approvals/approval-request.page";
import { ApprovalsPage, pageLinksSnapshot } from "../../../pages/platform/approvals/approvals.page";

// The session cannot be read without the API, so the pages show every guarded link, as they do for a person who may open
// them, and the API itself still decides once it answers.
test.describe("approvals without the API", () => {
  test.skip(
    !offlineBaseURL,
    "E2E_OFFLINE_BASE_URL is not set and Playwright did not start the offline web instance",
  );
  test.use({
    baseURL: offlineBaseURL ?? "",
    persona: null,
    expectedConsoleError: /the server responded with a status of 500/,
  });
  test.beforeEach(async ({ context }) => {
    await addUnverifiedSessionCookie(context, offlineBaseURL ?? "");
  });

  forEachTheme(
    "explains that the requests are not available instead of offering filters",
    async ({ page, capture }) => {
      const approvals = new ApprovalsPage(page);
      const renewal = sessionRenewed(page);
      await approvals.goto("waiting");
      expect((await renewal).status(), "the renewal the page sends without its API").toBe(500);

      await expect(page).toHaveTitle("Waiting for me · ERP-AI-Pro");
      await expect(approvals.heading).toHaveText("Waiting for me");
      await expect(approvals.pageLinks).toMatchAriaSnapshot(
        pageLinksSnapshot(["waiting", "mine", "decided", "kinds", "all"]),
      );
      await expect(approvals.counts).toHaveCount(0);
      await expect(approvals.unavailable).toHaveRole("status");
      await expect(approvals.unavailable).toContainText("The requests are not available.");
      await expect(approvals.unavailable).toContainText("The API did not respond.");
      await expect(approvals.list.filters).toHaveCount(0);
      await capture("approvals-unavailable");
    },
  );

  forEachTheme("explains that the kinds of request are not available", async ({ page, capture }) => {
    const kinds = new ApprovalKindsPage(page);
    await kinds.goto();

    await expect(page).toHaveTitle("Kinds of request · ERP-AI-Pro");
    await expect(kinds.unavailable).toHaveRole("status");
    await expect(kinds.unavailable).toContainText("The kinds of request are not available.");
    await expect(kinds.unavailable).toContainText("The API did not respond.");
    await expect(kinds.kinds).toHaveCount(0);
    await capture("approval-kinds-unavailable");
  });

  forEachTheme("explains that a request's details are not available", async ({ page, capture }) => {
    const requestPage = new ApprovalRequestPage(page);
    await requestPage.goto(randomUUID());

    await expect(page).toHaveTitle("Approval request · ERP-AI-Pro");
    await expect(requestPage.heading).toHaveText("Approval request");
    await expect(requestPage.unavailable).toHaveRole("status");
    await expect(requestPage.unavailable).toContainText("This request's details are not available.");
    await expect(requestPage.unavailable).toContainText("The API did not respond.");
    await expect(requestPage.actions).toHaveCount(0);
    await capture("approval-request-unavailable");
  });
});
