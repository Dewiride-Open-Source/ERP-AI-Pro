import type { Page, Request } from "@playwright/test";

import { holdServerFunctionCalls } from "../../../fixtures/server-functions";
import { endEntraSession } from "../../../fixtures/sign-in";
import { changesPagesWithoutTransitions, expect, forEachTheme, test } from "../../../fixtures/test";
import { isSignInPage, LoginPage, sessionEndedNotice } from "../../../pages/identity/auth/login.page";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import {
  dismissedApprovalsCookie,
  FeedbackPage,
  feedbackPath,
  SlowReportPage,
  slowReportPath,
} from "../../../pages/platform/design/feedback.page";

type ReminderEvent = { readonly kind: "click" | "height"; readonly height: string; readonly at: number };

type PageChangeAnimation = {
  readonly name: string;
  readonly pseudoElement: string;
  readonly duration: number;
};

type PageChange = { readonly animations: readonly PageChangeAnimation[]; readonly skipped?: string };

type MotionDurations = { readonly fast: number; readonly normal: number };

declare global {
  interface Window {
    reminderEvents?: ReminderEvent[];
    returnedRows?: string[];
    pageChanges?: PageChange[];
  }
}

const travel = "Travel advance for the Pune client visit";
const vendor = "Onboarding of Konark Electricals as a vendor";
const purchase = "Purchase order PO-2026-0412 for laptops";
const budget = "Budget revision for the festive campaign";
const renewal = "Renewal of the design software subscription";

function isServerFunctionCall(request: Request): boolean {
  return (
    request.method() === "POST" &&
    new URL(request.url()).pathname === feedbackPath &&
    request.headers()["next-action"] !== undefined
  );
}

function countServerFunctionCalls(page: Page): { count: () => number } {
  let calls = 0;
  page.on("request", (request) => {
    if (isServerFunctionCall(request)) calls += 1;
  });
  return { count: () => calls };
}

// Records every element added to the approvals card whose text names the request, so a row that came back for a moment
// between the optimistic removal and the Server Function's answer is caught even though it is gone again afterwards.
async function watchForReturningRow(page: Page, request: string): Promise<void> {
  await page.evaluate((text) => {
    const returned: string[] = [];
    window.returnedRows = returned;
    const card = document.querySelector("[data-testid='feedback-approvals']");
    if (card === null) throw new Error("The approvals card is missing.");
    new MutationObserver((records) => {
      for (const record of records) {
        for (const node of record.addedNodes) {
          if (node.textContent?.includes(text)) returned.push(node.textContent);
        }
      }
    }).observe(card, { childList: true, subtree: true });
  }, request);
}

async function watchReminders(page: Page): Promise<void> {
  await page.evaluate(() => {
    const events: ReminderEvent[] = [];
    window.reminderEvents = events;
    const list = document.querySelector("[data-testid='reminders']");
    if (list === null) throw new Error("The reminders list is missing.");
    document.addEventListener(
      "click",
      () => events.push({ kind: "click", height: "", at: performance.now() }),
      {
        capture: true,
      },
    );
    new MutationObserver((records) => {
      for (const record of records) {
        if (record.target instanceof HTMLElement && record.target.dataset.slot === "animated-list-item") {
          events.push({ kind: "height", height: record.target.style.height, at: performance.now() });
        }
      }
    }).observe(list, { attributes: true, attributeFilter: ["style"], subtree: true });
  });
}

// A loaded runner can draw fewer frames than an animation lasts, so an item that moves may skip every height between closed
// and open; an animation only ends once its duration has passed since it started, so the time from the click to the item's
// last height is at least that duration however few frames the runner draws.
async function reminderMotion(
  page: Page,
  openHeight: number,
): Promise<{ between: string[]; milliseconds: number }> {
  const events = await page.evaluate(() => window.reminderEvents?.splice(0) ?? []);
  const heights = events.filter(({ kind }) => kind === "height");
  const between = heights
    .map(({ height }) => height)
    .filter((height) => {
      const pixels = /^(\d+(?:\.\d+)?)px$/.exec(height)?.[1];
      return pixels !== undefined && Number(pixels) > 0 && Number(pixels) < openHeight;
    });
  const clicked = events.find(({ kind }) => kind === "click")?.at;
  const settled = heights.at(-1)?.at;
  return { between, milliseconds: clicked === undefined || settled === undefined ? 0 : settled - clicked };
}

// React runs each change of page through document.startViewTransition, whose ready promise settles once the transition's
// pseudo-elements exist and their animations have started, so the animations of every change are read there. The wrapper
// sits on the prototype, because the order of a page's and its context's init scripts is not defined and the context
// fixture may remove the prototype's method; a page without it changes without a transition and records nothing.
function recordPageChanges(): void {
  const changes: PageChange[] = [];
  window.pageChanges = changes;
  const prototype = Document.prototype;
  if (!("startViewTransition" in prototype)) return;
  const start = prototype.startViewTransition;
  prototype.startViewTransition = function (this: Document, update) {
    const transition = start.call(this, update);
    void transition.ready.then(
      () => {
        changes.push({
          animations: document.getAnimations().flatMap((animation) => {
            const effect = animation.effect;
            if (!(animation instanceof CSSAnimation) || !(effect instanceof KeyframeEffect)) return [];
            const pseudoElement = effect.pseudoElement ?? "";
            if (!pseudoElement.startsWith("::view-transition")) return [];
            return [
              {
                name: animation.animationName,
                pseudoElement,
                duration: Number(effect.getComputedTiming().duration),
              },
            ];
          }),
        });
      },
      (reason: unknown) => changes.push({ animations: [], skipped: String(reason) }),
    );
    return transition;
  };
}

async function animatedPageChanges(page: Page, count: number): Promise<PageChange[]> {
  const animated = async () =>
    (await page.evaluate(() => window.pageChanges ?? [])).filter(
      (change) => change.skipped !== undefined || change.animations.length > 0,
    );
  await expect
    .poll(async () => (await animated()).length, { message: "changes of page that animated" })
    .toBe(count);
  return animated();
}

// The page being left fades out over the fast duration and the arriving one rises in over the normal one, while the root
// snapshot, the shell around the page, stays still.
function expectPageChange(change: PageChange, durations: MotionDurations): void {
  expect(change.skipped, "a change of page the browser skipped").toBeUndefined();
  expect(change.animations.map(({ name }) => name).sort(), "animations of the change of page").toEqual([
    "page-enter",
    "page-exit",
  ]);
  for (const { name, pseudoElement, duration } of change.animations) {
    const entering = name === "page-enter";
    expect(pseudoElement, `the pseudo-element ${name} runs on`).toMatch(
      entering ? /^::view-transition-new\(/ : /^::view-transition-old\(/,
    );
    expect(duration, `milliseconds of ${name}`).toBeCloseTo(entering ? durations.normal : durations.fast, 3);
  }
}

async function motionDurations(page: Page): Promise<MotionDurations> {
  const { fast, normal } = await page.evaluate(() => {
    const root = getComputedStyle(document.documentElement);
    return {
      fast: root.getPropertyValue("--motion-duration-fast"),
      normal: root.getPropertyValue("--motion-duration-normal"),
    };
  });
  return { fast: seconds(fast) * 1000, normal: seconds(normal) * 1000 };
}

function seconds(duration: string): number {
  const match = /^(\d*\.?\d+(?:e-?\d+)?)(ms|s)$/.exec(duration.trim());
  if (match?.[1] === undefined) throw new Error(`Not a duration: ${duration}`);
  return Number(match[1]) / (match[2] === "ms" ? 1000 : 1);
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

      await feedback.selectCheckbox(travel).check();
      await feedback.selectCheckbox(budget).check();
      await expect(approvals.selection).toContainText("2 requests selected");

      const releaseDismissal = await holdServerFunctionCalls(page, feedbackPath);
      await feedback.dismissButton(travel).click();
      await expect(await approvals.row(travel)).toHaveCount(0);
      await expect(approvals.status).toContainText("Showing all 4 requests.");
      await expect(feedback.approvalsHeading).toBeFocused();
      await watchForReturningRow(page, travel);
      releaseDismissal();
      await expect(feedback.shell.toast(`Dismissed “${travel}”.`)).toBeVisible();
      await expect(await approvals.row(travel)).toHaveCount(0);
      expect(await page.evaluate(() => window.returnedRows ?? []), "rows that came back").toEqual([]);
      await expect(approvals.selection).toContainText("1 request selected");
      await expect(feedback.restoreApprovals).toBeVisible();

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
      await refusal.getByRole("button", { name: "Close toast" }).click();
      await expect(refusal).toHaveCount(0);

      await feedback.goto();
      await expect(approvals.status).toContainText("Showing all 4 requests.");
      await feedback.restoreApprovals.click();
      await expect(feedback.shell.toast("Restored every request.")).toBeVisible();
      await expect(approvals.status).toContainText("Showing all 5 requests.");
      await expect(feedback.restoreApprovals).toHaveCount(0);
    },
  );

  test("dismisses one request for a double click, although the next row moves under the pointer", async ({
    page,
  }) => {
    const feedback = new FeedbackPage(page);
    const { approvals } = feedback;
    const calls = countServerFunctionCalls(page);
    await feedback.goto();

    await feedback.dismissButton(purchase).dblclick();
    await expect(feedback.shell.toast(`Dismissed “${purchase}”.`)).toBeVisible();
    await expect(approvals.status).toContainText("Showing all 4 requests.");
    await expect(await approvals.row(travel)).toHaveCount(1);
    expect(calls.count(), "Server Function calls").toBe(1);
  });

  test.describe("when the Server Function call itself fails", () => {
    // Chromium logs the provoked reset; WebKit and Firefox log nothing for it.
    test.use({ expectedConsoleError: /^Failed to load resource: net::ERR_CONNECTION_RESET$/ });

    forEachTheme(
      "brings the request back and says the service did not respond",
      async ({ page, capture }) => {
        const feedback = new FeedbackPage(page);
        const { approvals } = feedback;
        await feedback.goto();
        await page.route(
          (url) => url.pathname === feedbackPath,
          (route) =>
            isServerFunctionCall(route.request()) ? route.abort("connectionreset") : route.fallback(),
        );

        await feedback.dismissButton(renewal).click();
        const failure = feedback.shell.toast(`“${renewal}” was not dismissed.`);
        await expect(failure).toBeVisible();
        await expect(failure).toContainText("The ERP service did not respond. Try again.");
        await expect(await approvals.row(renewal)).toHaveCount(1);
        await expect(approvals.status).toContainText("Showing all 5 requests.");
        await capture("approvals-unreachable", failure);
      },
    );
  });

  forEachTheme(
    "adds a reminder, refuses an empty one and removes every reminder",
    async ({ page, capture }) => {
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
      const added = feedback.reminderItems.last();
      await expect(feedback.reminderItems).toHaveCount(4);
      await expect(added).toContainText("File the quarterly TDS return");
      await expect(added).not.toHaveAttribute("data-animating");
      await expect(added).toHaveCSS("overflow", "visible");
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

      for (const reminder of [
        "Send the September payslips",
        "Renew the office lease",
        "File the quarterly TDS return",
      ]) {
        await feedback.removeReminderButton(reminder).click();
      }
      await expect(feedback.reminderItems).toHaveCount(0);
      await expect(feedback.remindersCard.getByTestId("reminders-empty")).toHaveText("No reminders left.");
      await capture("reminders-empty", feedback.remindersCard);
    },
  );

  forEachTheme(
    "offers to load the bank feed again, stays busy until the answer and says when it is connected",
    async ({ page, capture }) => {
      const feedback = new FeedbackPage(page);
      const calls = countServerFunctionCalls(page);
      await feedback.goto();
      const tryAgain = feedback.retryCard.getByRole("button", { name: "Try again" });

      await expect(
        feedback.retryCard.getByRole("heading", { name: "The bank feed did not load", level: 2 }),
      ).toBeVisible();
      await expect(feedback.retryCard).toContainText("Reference: BANK-FEED-TIMEOUT");
      await capture("retry-state", feedback.retryCard);

      const release = await holdServerFunctionCalls(page, feedbackPath);
      await tryAgain.focus();
      await page.keyboard.press("Enter");
      await expect(tryAgain).toHaveAttribute("aria-busy", "true");
      await expect(tryAgain).toHaveAttribute("aria-disabled", "true");
      await expect(tryAgain).toBeFocused();
      await page.keyboard.press("Enter");
      release();
      await expect(feedback.bankFeedConnected).toBeFocused();
      await expect(feedback.bankFeedConnected).toHaveText(
        "The bank feed is connected and today's transactions are in.",
      );
      expect(calls.count(), "Server Function calls").toBe(1);
    },
  );

  test.describe("once Microsoft has ended this browser's session", () => {
    test("loading the bank feed again goes to the sign-in page and comes back to this page", async ({
      baseURL,
      entraSession,
      page,
      request,
    }) => {
      const feedback = new FeedbackPage(page);
      const renewed = sessionRenewed(page);
      await feedback.goto();
      await renewed;
      await endEntraSession(request, entraSession);

      await feedback.retryCard.getByRole("button", { name: "Try again" }).click();

      await expect(page).toHaveURL(
        isSignInPage(new URL(baseURL ?? "").origin, feedbackPath, "session-ended"),
      );
      await expect(new LoginPage(page).notice).toHaveText(sessionEndedNotice);
    });

    test("dismissing a request goes to the sign-in page and dismisses nothing", async ({
      baseURL,
      context,
      entraSession,
      page,
      request,
    }) => {
      const feedback = new FeedbackPage(page);
      const renewed = sessionRenewed(page);
      await feedback.goto();
      await renewed;
      await endEntraSession(request, entraSession);

      await feedback.dismissButton(travel).click();

      await expect(page).toHaveURL(
        isSignInPage(new URL(baseURL ?? "").origin, feedbackPath, "session-ended"),
      );
      await expect(new LoginPage(page).notice).toHaveText(sessionEndedNotice);
      const cookies = await context.cookies();
      expect(
        cookies.find((cookie) => cookie.name === dismissedApprovalsCookie),
        "the dismissed requests of the page",
      ).toBeUndefined();
    });
  });

  test.describe("when the page itself fails", () => {
    // React logs the caught error object; Firefox reports an Error object logged to the console as the bare text "Error".
    test.use({ expectedConsoleError: /The feedback page failed on purpose\.|^Error$/ });

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
      await expect(feedback.shell.main).toBeFocused();
    });
  });

  // The page streams its skeleton ahead of the report on a visit in every engine. On a client navigation WebKit shows the new
  // page only once the whole answer has arrived, so the link is followed in the next test without asserting the skeleton.
  // A capture of the skeleton would outlast its three seconds in Firefox and WebKit; the kitchen sink's skeleton specimen, a
  // LoadingStatus too, is the one captured and scanned.
  forEachTheme(
    "shows the report's own loading status while it is prepared, then the report without a transition",
    async ({ page, capture }) => {
      const report = new SlowReportPage(page);
      await page.addInitScript(recordPageChanges);
      await page.goto(slowReportPath, { waitUntil: "commit" });

      await expect(report.loading).toBeVisible();
      await expect(report.loading.getByRole("heading", { level: 1 })).toHaveText(
        "Receivables ageing (example)",
      );
      await expect(report.heading).toBeVisible({ timeout: 15_000 });
      await expect(report.loading).toHaveCount(0);
      expect(
        (await page.evaluate(() => window.pageChanges ?? [])).flatMap(({ animations }) => animations),
        "animations of the first load and of the report replacing its skeleton",
      ).toEqual([]);
      await capture("report");
    },
  );

  test("opens the report from this page and comes back", async ({ page }) => {
    const feedback = new FeedbackPage(page);
    const report = new SlowReportPage(page);
    await feedback.goto();

    await feedback.designPageLink("Receivables ageing (takes three seconds)").click();
    await expect(report.heading).toBeVisible({ timeout: 15_000 });
    await expect(page).toHaveURL((url) => url.pathname === slowReportPath);
    await report.backToFeedback.click();
    await expect(feedback.heading).toBeVisible();
  });

  test("follows each link to another design page and back", async ({ page }) => {
    const feedback = new FeedbackPage(page);
    await feedback.goto();

    for (const [title, heading] of [
      ["Form kit", "Form kit"],
      ["Data table", "Data table"],
      ["Kitchen sink", "Design system"],
    ] as const) {
      await feedback.designPageLink(title).click();
      await expect(page.getByRole("heading", { name: heading, level: 1 })).toBeVisible();
      await page.goBack();
      await expect(feedback.heading).toBeVisible();
    }
  });

  for (const reducedMotion of ["no-preference", "reduce"] as const) {
    test(`moves the page and the list only when motion is allowed (${reducedMotion})`, async ({
      browserName,
      page,
    }) => {
      await page.emulateMedia({ reducedMotion });
      await page.addInitScript(recordPageChanges);
      const feedback = new FeedbackPage(page);
      const report = new SlowReportPage(page);
      await feedback.goto();
      const moves = reducedMotion === "no-preference";

      const durations = await motionDurations(page);
      if (moves) {
        expect(durations.fast, "--motion-duration-fast in milliseconds").toBeCloseTo(150, 3);
        expect(durations.normal, "--motion-duration-normal in milliseconds").toBeCloseTo(200, 3);
      } else {
        expect(durations.fast, "--motion-duration-fast in milliseconds").toBeLessThanOrEqual(0.01);
        expect(durations.normal, "--motion-duration-normal in milliseconds").toBeLessThanOrEqual(0.01);
      }

      // An AnimatedList item moves over the normal duration, and clock readings in the page are coarsened.
      const shortestMove = durations.normal * 0.9;
      const openHeight = (await feedback.reminderItems.last().boundingBox())?.height ?? 0;
      expect(openHeight).toBeGreaterThan(0);
      await watchReminders(page);
      await feedback.reminderInput.fill("Book the auditor's visit");
      await feedback.addReminder.click();
      const added = feedback.reminderItems.last();
      await expect(added).toContainText("Book the auditor's visit");
      await expect(added).not.toHaveAttribute("data-animating");
      await expect.poll(async () => (await added.boundingBox())?.height).toBe(openHeight);
      const entering = await reminderMotion(page, openHeight);
      if (moves) {
        expect(entering.milliseconds, "milliseconds the added item took to open").toBeGreaterThanOrEqual(
          shortestMove,
        );
      } else expect(entering.between, "heights the added item passed through").toEqual([]);

      await feedback.removeReminderButton("Book the auditor's visit").click();
      await expect(feedback.reminderItems).toHaveCount(3);
      const leaving = await reminderMotion(page, openHeight);
      if (moves) {
        expect(leaving.milliseconds, "milliseconds the removed item took to close").toBeGreaterThanOrEqual(
          shortestMove,
        );
      } else expect(leaving.between, "heights the removed item passed through").toEqual([]);

      const viewTransitions = await page.evaluate(() => "startViewTransition" in document);
      await feedback.designPageLink("Receivables ageing (takes three seconds)").click();
      await expect(report.heading).toBeVisible({ timeout: 15_000 });
      await report.backToFeedback.click();
      await expect(feedback.heading).toBeVisible();
      if (!viewTransitions) {
        test.info().annotations.push({
          type: "skip",
          description: changesPagesWithoutTransitions(browserName)
            ? "the context fixture removes document.startViewTransition in WebKit on Windows, whose port crashes or stalls in about half of the client navigations that run a view transition; Linux WebKit in CI keeps the check"
            : "this browser has no document.startViewTransition, so its pages change without a transition",
        });
        return;
      }
      for (const change of await animatedPageChanges(page, 2)) expectPageChange(change, durations);
    });
  }
});
