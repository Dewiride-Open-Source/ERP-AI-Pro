import {
  approvalKind,
  askForApproval,
  decideApproval,
  readApprovalRequest,
  reportTo,
  requestApi,
  signInAdministrator,
  signInColleague,
  withdrawWaiting,
} from "../../../fixtures/records/approval-records";
import { assignRoles } from "../../../fixtures/records/person-records";
import { expect, signedInApi, test, themes } from "../../../fixtures/test";
import {
  ApprovalRequestPage,
  dialogSnapshot,
  headerSnapshot,
  stepsSnapshot,
} from "../../../pages/platform/approvals/approval-request.page";
import {
  approvalPagePaths,
  ApprovalsPage,
  requestRowSnapshot,
} from "../../../pages/platform/approvals/approvals.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

// A request of a kind with a Finance step waits for every holder of the Finance role once its manager approves it, so the tests
// that ask for one take their project's and theme's kind, run one after another, and read Finance's list filtered by that kind.
test.describe("approvals in steps", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.describe.configure({ mode: "default" });
      test.use({ theme });

      test("the manager approves the first step, which sends the request on to Finance", async ({
        page,
        person,
        request,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "chain");
        const approvals = new ApprovalsPage(page);
        const requestPage = new ApprovalRequestPage(page);
        const shell = new AppShell(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(page.context());
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          await reportTo(await signInAdministrator(request), asker.person, person);
          const id = await askForApproval(asker.request, kind);
          asked.push(id);
          const waiting = await readApprovalRequest(own, id);
          expect(
            waiting.steps.map((step) => step.state),
            "the steps as asked",
          ).toEqual(["waiting", "pending"]);

          await requestPage.goto(id);
          await requestPage.waitUntilInteractive();
          await expect(requestPage.main).toMatchAriaSnapshot(
            headerSnapshot(waiting, viewer, ["approve", "return", "reject"]),
          );
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(waiting, viewer));
          await capture("approval-steps-first");

          await requestPage.approve.click();
          await expect(requestPage.dialog).toMatchAriaSnapshot(
            dialogSnapshot(
              "approve",
              "Approving the Manager step sends the request on to its next step, Finance.",
            ),
          );
          await capture("approval-steps-approve", requestPage.dialog);
          await requestPage.comment.fill("Within the trip's budget.");
          await requestPage.submit.click();
          await requestPage.expectDialogClosed();
          await expect(
            shell.toast("You approved the Manager step. Next, the Finance step waits for Finance."),
          ).toBeVisible();
          await expect(requestPage.statusLine).toBeFocused();
          await expect(requestPage.actions, "a step that waits for Finance").toHaveCount(0);
          const moved = await readApprovalRequest(own, id);
          expect(
            moved.steps.map((step) => step.state),
            "the steps once approved",
          ).toEqual(["approved", "waiting"]);
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(moved, viewer, []));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(moved, viewer));
          await capture("approval-steps-finance");

          await approvals.goto("decided");
          await approvals.waitUntilInteractive();
          await expect(approvals.list.status).toHaveText("Showing 1 request.");
          await expect(await approvals.request(id)).toMatchAriaSnapshot(
            requestRowSnapshot(moved, viewer, "answered", await approvals.list.showsCards()),
          );
        } finally {
          await withdrawWaiting(asker.api, asked);
          await asker.request.dispose();
        }
      });

      test("is told someone acted on the request after the page was loaded, and shows its latest version", async ({
        page,
        person,
        request,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        const kind = approvalKind(testInfo, theme, "chain");
        const requestPage = new ApprovalRequestPage(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(page.context());
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          await reportTo(await signInAdministrator(request), asker.person, person);
          const id = await askForApproval(asker.request, kind);
          asked.push(id);

          await requestPage.goto(id);
          await requestPage.waitUntilInteractive();
          await decideApproval(own, id, "approve");
          await requestPage.sendBack.click();
          await requestPage.comment.fill("Book a cheaper hotel.");
          await requestPage.submit.click();
          await expect(requestPage.summary).toBeFocused();
          await expect(requestPage.summary).toContainText(
            "Someone else acted on this request while you worked. Show the latest version of the request, then try again if it still waits.",
          );
          await expect(requestPage.reload).toHaveText("Show the latest version of the request");
          await capture("approval-steps-out-of-date", requestPage.dialog);

          await requestPage.reload.click();
          await requestPage.expectDialogClosed();
          await expect(requestPage.actions).toHaveCount(0);
          await expect(requestPage.statusLine).toBeFocused();
          await expect(requestPage.main).toMatchAriaSnapshot(
            headerSnapshot(await readApprovalRequest(own, id), viewer, []),
          );
        } finally {
          await withdrawWaiting(asker.api, asked);
          await asker.request.dispose();
        }
      });

      test("Finance finds the request under its kind once the manager approved it, and approves the last step", async ({
        page,
        person,
        request,
        playwright,
        baseURL,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "chain");
        const approvals = new ApprovalsPage(page);
        const requestPage = new ApprovalRequestPage(page);
        const shell = new AppShell(page);
        const viewer = person?.id ?? "";
        const own = signedInApi(page.context());
        const asker = await signInColleague(playwright.request, baseURL, "accountant");
        const manager = await signInColleague(playwright.request, baseURL, "accountant");
        const asked: string[] = [];
        try {
          await assignRoles(request, person, ["finance"]);
          await reportTo(requestApi(request), asker.person, manager.person);
          const id = await askForApproval(asker.request, kind);
          asked.push(id);
          const note = "The fare matches the booking.";
          await decideApproval(manager.api, id, "approve", note);
          const waiting = await readApprovalRequest(own, id);
          expect(waiting.canDecide, "a holder of the Finance role decides the Finance step").toBe(true);
          expect(waiting.decidesInPlace, "the Finance step waits for Finance").toBe(false);

          await approvals.goto("waiting", `?kind=${encodeURIComponent(kind.key)}`);
          await approvals.waitUntilInteractive();
          await expect(approvals.list.optionsFilter("Kind of request")).toHaveText(kind.title);
          const row = await approvals.request(id);
          await expect(row).toMatchAriaSnapshot(
            requestRowSnapshot(waiting, viewer, "waiting", await approvals.list.showsCards()),
          );
          await capture("approvals-waiting-for-finance", row);

          await (await approvals.requestLink(id)).click();
          await expect(requestPage.heading).toHaveText(kind.title);
          await requestPage.waitUntilInteractive();
          await expect(requestPage.main).toMatchAriaSnapshot(
            headerSnapshot(waiting, viewer, ["approve", "return", "reject"]),
          );
          await expect(requestPage.inPlace).toHaveCount(0);
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(waiting, viewer));
          await capture("approval-steps-last");

          await requestPage.approve.click();
          await expect(requestPage.dialog).toMatchAriaSnapshot(
            dialogSnapshot("approve", "Approving the Finance step approves the request."),
          );
          await requestPage.submit.click();
          await requestPage.expectDialogClosed();
          await expect(shell.toast("You approved the request.")).toBeVisible();
          await expect(requestPage.statusLine).toBeFocused();
          const approved = await readApprovalRequest(own, id);
          expect(approved.state, "the request").toBe("approved");
          await expect(requestPage.main).toMatchAriaSnapshot(headerSnapshot(approved, viewer, []));
          await expect(requestPage.stepList).toMatchAriaSnapshot(stepsSnapshot(approved, viewer));

          await approvals.goto("decided", `?kind=${encodeURIComponent(kind.key)}&state=approved`);
          await approvals.waitUntilInteractive();
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.decided);
          await expect(approvals.list.status).toHaveText("Showing 1 request.");
          await expect(await approvals.request(id)).toMatchAriaSnapshot(
            requestRowSnapshot(approved, viewer, "answered", await approvals.list.showsCards()),
          );
        } finally {
          await withdrawWaiting(asker.api, asked);
          await asker.request.dispose();
          await manager.request.dispose();
        }
      });
    });
  }
});
