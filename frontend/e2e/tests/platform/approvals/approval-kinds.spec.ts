import {
  approvalKind,
  changeApprovalKind,
  readApprovalKind,
  readApprovalKinds,
  readApprovalRequest,
  restoreApprovalKind,
  setUpApprovalKind,
  submitApproval,
  testApprovalKind,
  untouchedApprovalKind,
  withdrawWaiting,
  type ApprovalKindAnswer,
  type TestApprovalKind,
} from "../../../fixtures/records/approval-records";
import { holdServerFunctionCalls } from "../../../fixtures/server-functions";
import { expect, signedInApi, test, themes, type SignedInApi } from "../../../fixtures/test";
import {
  ApprovalKindsPage,
  isKindSave,
  kindSnapshot,
  savedText,
  settingDialogSnapshot,
  workingDaysProblem,
} from "../../../pages/platform/approvals/approval-kinds.page";
import {
  approvalPagePaths,
  ApprovalsPage,
  triesToCloseWhileSaving,
  watchRequests,
} from "../../../pages/platform/approvals/approvals.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

async function settingOf(api: SignedInApi, kind: TestApprovalKind): Promise<ApprovalKindAnswer> {
  const answer = await readApprovalKind(api, kind);
  return answer.setting === null ? setUpApprovalKind(api, kind, true, answer.declaredWorkingDays) : answer;
}

// A kind's setting belongs to the whole kind and the host keeps it for the rest of the run, so the tests that change one take
// their project's and theme's kind, run one after another, and put it back as the kind declares it before and after.
test.describe("kinds of request for an administrator", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.describe.configure({ mode: "default" });
      test.use({ theme });

      test("shows each kind with its steps and changes a kind's working days after refusing a number outside 1 to 30", async ({
        page,
        context,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "settings");
        const approvals = new ApprovalsPage(page);
        const kinds = new ApprovalKindsPage(page);
        const shell = new AppShell(page);
        const api = signedInApi(context);
        const saves = watchRequests(page, isKindSave);
        const asked: string[] = [];
        await restoreApprovalKind(api, kind);
        try {
          const every = await readApprovalKinds(api);
          const declared = await readApprovalKind(api, kind);
          const untouched = await readApprovalKind(api, untouchedApprovalKind);
          const chain = await readApprovalKind(api, testApprovalKind("chain", 1));

          await approvals.goto("waiting");
          await approvals.pageLink("kinds").click();
          await expect(page).toHaveURL((url) => url.pathname === approvalPagePaths.kinds);
          await expect(kinds.heading).toHaveText("Kinds of request");
          await expect(page).toHaveTitle("Kinds of request · ERP-AI-Pro");
          await kinds.waitUntilInteractive();
          await expect(approvals.pageLink("kinds")).toHaveAttribute("aria-current", "page");
          await expect(kinds.card.locator("[data-slot='card-header']")).toMatchAriaSnapshot(`
            - heading "Kinds the ERP asks approval for" [level=2]
            - text: Working days are Monday to Friday and end at midnight India time. A change applies to the requests asked for after it; a request already waiting keeps its steps and its due times.
          `);
          await expect(kinds.list.getByRole("heading", { level: 3 })).toHaveText(
            every.map((each) => each.title),
          );
          await expect(kinds.kind(untouched.title)).toMatchAriaSnapshot(kindSnapshot(untouched, true));
          await expect(kinds.kind(chain.title)).toMatchAriaSnapshot(kindSnapshot(chain, true));
          await expect(kinds.kind(declared.title)).toMatchAriaSnapshot(kindSnapshot(declared, true));
          await capture("approval-kind", kinds.kind(declared.title));

          await kinds.change(kind.title).click();
          await expect(kinds.dialog).toMatchAriaSnapshot(settingDialogSnapshot(declared, true, "2"));
          await capture("approval-kind-dialog", kinds.dialog);
          await kinds.cancel.click();
          await kinds.expectDialogClosed();
          await expect(kinds.change(kind.title)).toBeFocused();
          await kinds.change(kind.title).click();
          await expect(kinds.dialog).toBeVisible();
          await page.keyboard.press("Escape");
          await kinds.expectDialogClosed();
          await expect(kinds.change(kind.title)).toBeFocused();
          await kinds.change(kind.title).press("Enter");
          await expect(kinds.dialog).toBeVisible();
          await kinds.close.click();
          await kinds.expectDialogClosed();
          await expect(kinds.change(kind.title)).toBeFocused();

          await kinds.change(kind.title).click();
          for (const refused of ["0", "31", "two"]) {
            await kinds.days.fill(refused);
            await kinds.save.click();
            await expect(kinds.days).toBeFocused();
            await expect(kinds.days).toHaveAttribute("aria-invalid", "true");
            await expect(kinds.fieldOf(kinds.days)).toContainText(workingDaysProblem);
          }
          expect(saves.count(), "settings sent").toBe(0);
          await capture("approval-kind-refused", kinds.dialog);

          await kinds.days.fill("5");
          await expect(kinds.days).not.toHaveAttribute("aria-invalid", "true");
          const release = await holdServerFunctionCalls(page, approvalPagePaths.kinds);
          await kinds.save.click();
          await expect(kinds.save).toHaveAttribute("aria-busy", "true");
          await triesToCloseWhileSaving(page, kinds.dialog, [kinds.close, kinds.cancel]);
          release();
          await kinds.expectDialogClosed();
          await expect(shell.toast(savedText(declared, true, 5))).toBeVisible();
          await expect(kinds.change(kind.title)).toBeFocused();
          expect(saves.count(), "settings sent").toBe(1);
          const changed = await readApprovalKind(api, kind);
          expect(changed.setting, "the saved setting").toMatchObject({
            required: true,
            workingDaysToDecide: 5,
          });
          await expect(kinds.kind(kind.title)).toMatchAriaSnapshot(kindSnapshot(changed, true));
          await expect(kinds.workingDays(kind.title)).toHaveText(
            "5 working days for each step, as an administrator set it; the ERP's own number is 2.",
          );
          await capture("approval-kind-changed", kinds.kind(kind.title));

          const requestId = await submitApproval(context.request, kind);
          expect(requestId, "a request of a kind that needs approval").not.toBeNull();
          asked.push(requestId ?? "");
          expect(
            (await readApprovalRequest(api, requestId ?? "")).steps[0]?.workingDays,
            "its step's days",
          ).toBe(5);
        } finally {
          await withdrawWaiting(api, asked);
          await restoreApprovalKind(api, kind);
        }
      });

      test("switches approval off for a kind, whose records then go through without a request, and on again", async ({
        page,
        context,
        capture,
      }, testInfo) => {
        test.slow();
        const kind = approvalKind(testInfo, theme, "settings");
        const kinds = new ApprovalKindsPage(page);
        const shell = new AppShell(page);
        const api = signedInApi(context);
        const asked: string[] = [];
        await restoreApprovalKind(api, kind);
        try {
          const declared = await readApprovalKind(api, kind);

          await kinds.goto();
          await kinds.waitUntilInteractive();
          await kinds.change(kind.title).click();
          await expect(kinds.required).toBeChecked();
          await kinds.required.click();
          await expect(kinds.required).not.toBeChecked();
          await expect(kinds.dialog).toMatchAriaSnapshot(settingDialogSnapshot(declared, false, "2"));
          await kinds.save.click();
          await kinds.expectDialogClosed();
          await expect(shell.toast(savedText(declared, false, 2))).toBeVisible();
          const off = await readApprovalKind(api, kind);
          expect(off.setting, "the setting switched off").toMatchObject({
            required: false,
            workingDaysToDecide: 2,
          });
          await expect(kinds.standing(kind.title)).toHaveText("Approval switched off");
          await expect(kinds.kind(kind.title)).toMatchAriaSnapshot(kindSnapshot(off, true));
          await capture("approval-kind-off", kinds.kind(kind.title));
          expect(
            await submitApproval(context.request, kind),
            "a submission while approval is off",
          ).toBeNull();

          await kinds.change(kind.title).click();
          await expect(kinds.required).not.toBeChecked();
          await kinds.required.press("Space");
          await expect(kinds.required).toBeChecked();
          await kinds.save.click();
          await kinds.expectDialogClosed();
          await expect(shell.toast(savedText(declared, true, 2))).toBeVisible();
          await expect(kinds.standing(kind.title)).toHaveText("Needs approval");
          const requestId = await submitApproval(context.request, kind);
          expect(requestId, "a submission once approval is on again").not.toBeNull();
          asked.push(requestId ?? "");
        } finally {
          await withdrawWaiting(api, asked);
          await restoreApprovalKind(api, kind);
        }
      });

      test("is told the setting changed after the page was loaded, and shows the latest settings", async ({
        page,
        context,
        capture,
      }, testInfo) => {
        const kind = approvalKind(testInfo, theme, "settings");
        const kinds = new ApprovalKindsPage(page);
        const api = signedInApi(context);
        await restoreApprovalKind(api, kind);
        try {
          const current = await settingOf(api, kind);

          await kinds.goto();
          await kinds.waitUntilInteractive();
          await kinds.change(kind.title).click();
          await expect(kinds.days).toHaveValue("2");
          if (current.setting !== null) await changeApprovalKind(api, current.setting, true, 3);
          await kinds.days.fill("4");
          await kinds.save.click();
          await expect(kinds.summary).toBeFocused();
          await expect(kinds.summary).toContainText(
            "Someone changed this setting while you worked. Show the latest settings, then try again.",
          );
          await expect(kinds.reload).toHaveText("Show the latest settings");
          await capture("approval-kind-out-of-date", kinds.dialog);

          await kinds.reload.click();
          await kinds.expectDialogClosed();
          await expect(kinds.workingDays(kind.title)).toHaveText(
            "3 working days for each step, as an administrator set it; the ERP's own number is 2.",
          );
        } finally {
          await restoreApprovalKind(api, kind);
        }
      });
    });
  }
});
