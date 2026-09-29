import { expect, type Locator, type Page } from "@playwright/test";

import { AppShell } from "../../shared/layout/app-shell.page";
import { DataTableRegion } from "../../shared/lists/data-table.page";

export const feedbackPath = "/design/feedback";

export class FeedbackPage {
  readonly shell: AppShell;
  readonly heading: Locator;
  readonly confirmCard: Locator;
  readonly archiveButton: Locator;
  readonly discardButton: Locator;
  readonly confirmOutcome: Locator;
  readonly approvalsCard: Locator;
  readonly approvalsHeading: Locator;
  readonly approvals: DataTableRegion;
  readonly restoreApprovals: Locator;
  readonly remindersCard: Locator;
  readonly reminderInput: Locator;
  readonly addReminder: Locator;
  readonly reminders: Locator;
  readonly reminderItems: Locator;
  readonly retryCard: Locator;
  readonly bankFeedConnected: Locator;
  readonly simulateFailure: Locator;
  readonly navigationCard: Locator;

  constructor(private readonly page: Page) {
    this.shell = new AppShell(page);
    this.heading = page.getByRole("heading", { name: "Feedback", level: 1 });
    this.confirmCard = page.getByTestId("feedback-confirm");
    this.archiveButton = this.confirmCard.getByRole("button", { name: "Archive the quotation" });
    this.discardButton = this.confirmCard.getByRole("button", { name: "Discard the draft" });
    this.confirmOutcome = page.getByTestId("confirm-demo-outcome");
    this.approvalsCard = page.getByTestId("feedback-approvals");
    this.approvalsHeading = page.getByRole("heading", { name: "Approval requests (example)", level: 2 });
    this.approvals = new DataTableRegion(page, this.approvalsCard);
    this.restoreApprovals = this.approvalsCard.getByRole("button", { name: "Restore the requests" });
    this.remindersCard = page.getByTestId("feedback-reminders");
    this.reminderInput = this.remindersCard.getByRole("textbox", { name: "Reminder" });
    this.addReminder = this.remindersCard.getByRole("button", { name: "Add reminder" });
    this.reminders = page.getByTestId("reminders");
    this.reminderItems = this.reminders.getByTestId("reminder");
    this.retryCard = page.getByTestId("feedback-retry");
    this.bankFeedConnected = page.getByTestId("retry-demo-connected");
    this.simulateFailure = page.getByRole("button", { name: "Simulate a page failure" });
    this.navigationCard = page.getByTestId("feedback-navigation");
  }

  async goto(query = ""): Promise<void> {
    await this.page.goto(`${feedbackPath}${query}`);
    await expect(this.heading).toBeVisible();
    await this.approvals.waitUntilInteractive();
  }

  dialog(title: string): Locator {
    return this.page.getByRole("alertdialog", { name: title });
  }

  removeReminderButton(text: string): Locator {
    return this.reminders.getByRole("button", { name: `Remove ${text}` });
  }

  dismissButton(request: string): Locator {
    return this.approvalsCard.getByRole("button", { name: `Dismiss ${request}` });
  }
}
