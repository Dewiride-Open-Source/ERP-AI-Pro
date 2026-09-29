import type { Page } from "@playwright/test";

import { holdServerFunctionCalls } from "../../../fixtures/server-functions";
import { expect, forEachTheme, test } from "../../../fixtures/test";
import { FeedbackPage, feedbackPath } from "../../../pages/platform/design/feedback.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

declare global {
  interface Window {
    reminderHeights?: string[];
  }
}

function reportHeading(page: Page) {
  return page.getByRole("heading", { name: "Receivables ageing (example)", level: 1 });
}

async function watchReminderHeights(page: Page): Promise<void> {
  await page.evaluate(() => {
    const heights: string[] = [];
    window.reminderHeights = heights;
    const list = document.querySelector("[data-testid='reminders']");
    if (list === null) throw new Error("The reminders list is missing.");
    new MutationObserver((records) => {
      for (const record of records) {
        if (record.target instanceof HTMLElement && record.target.dataset.slot === "animated-list-item") {
          heights.push(record.target.style.height);
        }
      }
    }).observe(list, { attributes: true, attributeFilter: ["style"], subtree: true });
  });
}

async function heightsBetweenClosedAndOpen(page: Page, openHeight: number): Promise<string[]> {
  const heights = await page.evaluate(() => window.reminderHeights ?? []);
  return heights.filter((height) => {
    const pixels = /^(\d+(?:\.\d+)?)px$/.exec(height)?.[1];
    return pixels !== undefined && Number(pixels) > 0 && Number(pixels) < openHeight;
  });
}

test.describe("feedback", () => {
  forEachTheme("asks before archiving or discarding and says what happened", async ({ page, capture }) => {
    const feedback = new FeedbackPage(page);
    await feedback.goto();
    const archive = feedback.dialog("Archive this quotation?");

    await feedback.archiveButton.focus();
    await page.keyboard.press("Enter");
    await expect(archive).toBeVisible();
    await expect(archive.getByRole("button", { name: "Cancel" })).toBeFocused();
    await expect(archive).toMatchAriaSnapshot(`
      - alertdialog "Archive this quotation?":
        - heading "Archive this quotation?" [level=2]
        - paragraph: QT-2026-00017 moves to the archive, where it can be restored.
        - button "Cancel"
        - button "Archive"
    `);
    await capture("confirm-dialog", archive);
    await page.keyboard.press("Escape");
    await expect(archive).toBeHidden();
    await expect(feedback.archiveButton).toBeFocused();
    await expect(feedback.confirmOutcome).toHaveText("Nothing has been archived or discarded yet.");

    await feedback.archiveButton.click();
    await archive.getByRole("button", { name: "Archive", exact: true }).click();
    await expect(archive).toBeHidden();
    await expect(feedback.archiveButton).toBeFocused();
    await expect(feedback.confirmOutcome).toHaveText("QT-2026-00017 is in the archive.");
    await expect(feedback.shell.toast("Archived QT-2026-00017.")).toBeVisible();

    const discard = feedback.dialog("Discard this draft?");
    await feedback.discardButton.click();
    await discard.getByRole("button", { name: "Keep the draft" }).click();
    await expect(discard).toBeHidden();
    await expect(feedback.discardButton).toBeFocused();
    await feedback.discardButton.click();
    await discard.getByRole("button", { name: "Discard", exact: true }).click();
    await expect(feedback.confirmOutcome).toBeFocused();
    await expect(feedback.confirmOutcome).toHaveText("The draft invoice was discarded.");
    await expect(feedback.shell.toast("Discarded the draft invoice.")).toBeVisible();
  });

  forEachTheme(
    "takes a dismissed request off the list at once and brings a refused one back",
    async ({ page, capture }) => {
      const feedback = new FeedbackPage(page);
      const { approvals } = feedback;
      await feedback.goto();
      await expect(approvals.status).toContainText("Showing all 5 requests.");
      await expect(feedback.restoreApprovals).toHaveCount(0);

      const travel = "Travel advance for the Pune client visit";
      const releaseDismissal = await holdServerFunctionCalls(page, feedbackPath);
      await feedback.dismissButton(travel).click();
      await expect(await approvals.row(travel)).toHaveCount(0);
      await expect(approvals.status).toContainText("Showing all 4 requests.");
      await expect(feedback.approvalsHeading).toBeFocused();
      releaseDismissal();
      await expect(feedback.shell.toast(`Dismissed “${travel}”.`)).toBeVisible();
      await expect(await approvals.row(travel)).toHaveCount(0);
      await expect(feedback.restoreApprovals).toBeVisible();

      const vendor = "Onboarding of Konark Electricals as a vendor";
      const releaseRefusal = await holdServerFunctionCalls(page, feedbackPath);
      await feedback.dismissButton(vendor).click();
      await expect(await approvals.row(vendor)).toHaveCount(0);
      await expect(approvals.status).toContainText("Showing all 3 requests.");
      releaseRefusal();
      const refusal = feedback.shell.toast(`“${vendor}” was not dismissed.`);
      await expect(refusal).toBeVisible();
      await expect(refusal).toContainText("This request is locked while finance reviews it.");
      await expect(await approvals.row(vendor)).toHaveCount(1);
      await expect(approvals.status).toContainText("Showing all 4 requests.");
      await capture("approvals-after-a-refusal", feedback.approvalsCard);

      await feedback.goto();
      await expect(approvals.status).toContainText("Showing all 4 requests.");
      await feedback.restoreApprovals.click();
      await expect(feedback.shell.toast("Restored every request.")).toBeVisible();
      await expect(approvals.status).toContainText("Showing all 5 requests.");
      await expect(feedback.restoreApprovals).toHaveCount(0);
    },
  );

  forEachTheme("adds a reminder, refuses an empty one and removes one", async ({ page, capture }) => {
    const feedback = new FeedbackPage(page);
    await feedback.goto();
    await expect(feedback.reminderItems).toHaveCount(3);

    await feedback.addReminder.click();
    await expect(feedback.reminderInput).toBeFocused();
    await expect(feedback.reminderInput).toHaveAttribute("aria-invalid", "true");
    await expect(feedback.remindersCard).toContainText("Enter a reminder.");
    await capture("reminders-refused", feedback.remindersCard);

    await feedback.reminderInput.fill("File the quarterly TDS return");
    await page.keyboard.press("Enter");
    await expect(feedback.reminderItems).toHaveCount(4);
    await expect(feedback.reminderItems.last()).toContainText("File the quarterly TDS return");
    await expect(feedback.reminderInput).toHaveValue("");
    await expect(feedback.reminderInput).not.toHaveAttribute("aria-invalid");

    await feedback.removeReminderButton("Reconcile the bank statement").click();
    await expect(feedback.reminderItems).toHaveCount(3);
    await expect(feedback.shell.toast("Removed “Reconcile the bank statement”.")).toBeVisible();
    await expect(feedback.reminderInput).toBeFocused();
    await expect(feedback.reminders).toMatchAriaSnapshot(`
      - list "Reminders (example)":
        - listitem:
          - text: Send the September payslips
          - button "Remove Send the September payslips"
        - listitem:
          - text: Renew the office lease
          - button "Remove Renew the office lease"
        - listitem:
          - text: File the quarterly TDS return
          - button "Remove File the quarterly TDS return"
    `);
    await capture("reminders", feedback.remindersCard);
  });

  forEachTheme(
    "offers to load the bank feed again and says when it is connected",
    async ({ page, capture }) => {
      const feedback = new FeedbackPage(page);
      await feedback.goto();
      const tryAgain = feedback.retryCard.getByRole("button", { name: "Try again" });

      await expect(
        feedback.retryCard.getByRole("heading", { name: "The bank feed did not load", level: 2 }),
      ).toBeVisible();
      await expect(feedback.retryCard).toContainText("Reference: BANK-FEED-TIMEOUT");
      await capture("retry-state", feedback.retryCard);
      await tryAgain.focus();
      await page.keyboard.press("Enter");
      await expect(feedback.bankFeedConnected).toBeFocused();
      await expect(feedback.bankFeedConnected).toHaveText(
        "The bank feed is connected and today's transactions are in.",
      );
    },
  );

  test.describe("when the page itself fails", () => {
    test.use({ expectedConsoleError: /The feedback page failed on purpose\./ });

    forEachTheme("shows the shell's error state and recovers with Try again", async ({ page, capture }) => {
      const feedback = new FeedbackPage(page);
      await feedback.goto();
      const failure = page.getByRole("heading", { name: "We could not show this page", level: 1 });

      await feedback.simulateFailure.click();
      await expect(failure).toBeFocused();
      await expect(feedback.shell.banner).toBeVisible();
      await expect(feedback.shell.main).toMatchAriaSnapshot(`
        - main:
          - heading "We could not show this page" [level=1]
          - text: Something went wrong while preparing it. Try again, and if it keeps happening, share the reference with support.
          - button "Try again"
      `);
      await capture("shell-error");
      await page.keyboard.press("Tab");
      await expect(feedback.shell.main.getByRole("button", { name: "Try again" })).toBeFocused();
      await page.keyboard.press("Enter");
      await expect(feedback.heading).toBeVisible();
      await expect(failure).toHaveCount(0);
    });
  });

  // The server sends the shell's loading status ahead of a page that has no loading file of its own, and React's inline
  // script swaps the page in once it arrives; with scripts turned off the swap never runs, so the status stays to be checked.
  test.describe("with scripts turned off", () => {
    test.use({ javaScriptEnabled: false });

    forEachTheme(
      "sends the shell's loading status ahead of a page without its own loading file",
      async ({ page }) => {
        const shell = new AppShell(page);
        await page.goto("/design/form-kit");

        await expect(shell.pageLoading).toBeVisible();
        await expect(shell.pageLoading).toHaveText("Loading the page");
        await expect(page.getByRole("heading", { name: "Form kit", level: 1 })).toBeHidden();
      },
    );
  });

  test("shows the report's own loading status when it is opened from this page", async ({
    page,
    capture,
  }) => {
    const feedback = new FeedbackPage(page);
    const reportLoading = page.getByRole("status", { name: "Loading the report" });
    await feedback.goto();

    await feedback.navigationCard
      .getByRole("link", { name: "Receivables ageing (takes three seconds)" })
      .click();
    await expect(reportLoading).toBeVisible();
    await capture("report-loading");
    await expect(reportHeading(page)).toBeVisible({ timeout: 15_000 });
    await expect(reportLoading).toHaveCount(0);
    await page.getByRole("link", { name: "Back to feedback" }).click();
    await expect(feedback.heading).toBeVisible();
  });

  for (const reducedMotion of ["no-preference", "reduce"] as const) {
    test(`moves the page and the list only when motion is allowed (${reducedMotion})`, async ({ page }) => {
      await page.emulateMedia({ reducedMotion });
      const feedback = new FeedbackPage(page);
      await feedback.goto();
      const moves = reducedMotion === "no-preference";

      await expect(feedback.shell.pageTransition).toHaveCSS("animation-name", "page-enter");
      await expect(feedback.shell.pageTransition).toHaveCSS("animation-duration", moves ? "0.2s" : "1e-05s");

      const openHeight = (await feedback.reminderItems.last().boundingBox())?.height ?? 0;
      expect(openHeight).toBeGreaterThan(0);
      await watchReminderHeights(page);
      await feedback.reminderInput.fill("Book the auditor's visit");
      await feedback.addReminder.click();
      const added = feedback.reminderItems.last();
      await expect(added).toContainText("Book the auditor's visit");
      await expect(added).toHaveCSS("opacity", "1");
      await expect.poll(async () => (await added.boundingBox())?.height).toBe(openHeight);

      const between = await heightsBetweenClosedAndOpen(page, openHeight);
      if (moves) expect(between.length, "heights the added item passed through").toBeGreaterThan(0);
      else expect(between, "heights the added item passed through").toEqual([]);

      await feedback.removeReminderButton("Book the auditor's visit").click();
      await expect(feedback.reminderItems).toHaveCount(3);
    });
  }
});
