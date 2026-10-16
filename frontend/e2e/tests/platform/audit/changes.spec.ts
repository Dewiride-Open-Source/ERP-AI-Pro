import type { Locator, Page, Route } from "@playwright/test";
import { randomUUID } from "node:crypto";

import { withholdClientBundles } from "../../../fixtures/client-bundles";
import { endEntraSession } from "../../../fixtures/sign-in";
import { expect, forEachTheme, test, themes, type SignedInApi } from "../../../fixtures/test";
import { isSignInPage, LoginPage, sessionEndedNotice } from "../../../pages/identity/auth/login.page";
import { sessionRenewed } from "../../../pages/identity/auth/session.page";
import {
  AuditTrailPage,
  auditTrailPath,
  changesApi,
  exportApi,
} from "../../../pages/platform/audit/changes.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const departmentsApi = "/api/identity/users/departments";

const departmentCount = 10;

const pageSize = 20;

const byteOrderMark = Buffer.from([0xef, 0xbb, 0xbf]);

const exportHeader = [
  "Time (IST)",
  "Time (UTC)",
  "Action",
  "Subject type",
  "Subject id",
  "Subject",
  "Record type",
  "Record id",
  "Changed by",
  "Changed by id",
  "Client address",
  "Correlation id",
  "Field",
  "Before",
  "Before name",
  "After",
  "After name",
]
  .map((cell) => `"${cell}"`)
  .join(",");

const idProblem = "Enter a complete id, such as 0192f3c4-5b6a-7d8e-9f01-23456789abcd.";

type Department = { readonly id: string; readonly name: string };

async function createDepartment(api: SignedInApi, name: string): Promise<Department> {
  const response = await api.post(departmentsApi, {
    headers: { "Idempotency-Key": randomUUID() },
    data: { name },
  });
  expect(response.status(), `the creation of ${name}`).toBe(201);
  const { id } = (await response.json()) as { id?: unknown };
  expect(typeof id, `the id of ${name}`).toBe("string");
  return { id: id as string, name };
}

async function deleteDepartment(api: SignedInApi, department: Department): Promise<void> {
  const response = await api.delete(`${departmentsApi}/${department.id}`);
  expect(response.status(), `the deletion of ${department.name}`).toBe(204);
}

// Each department is created and then deleted, so the signed-in person has made 20 changes after the creation of their own
// record at sign-in: 21 in all, one more than the first page holds.
async function seedDepartments(api: SignedInApi): Promise<readonly Department[]> {
  const prefix = `e2e-audit-${randomUUID().slice(-12)}`;
  const departments: Department[] = [];
  for (let index = 1; index <= departmentCount; index += 1) {
    departments.push(await createDepartment(api, `${prefix}-${String(index).padStart(2, "0")}`));
  }
  for (const department of departments) await deleteDepartment(api, department);
  return departments;
}

async function changeTimes(api: SignedInApi, actorId: string): Promise<readonly Date[]> {
  const query = new URLSearchParams({ filter: `actorId:eq:${actorId}`, pageSize: "100" });
  const response = await api.get(`${changesApi}?${query}`);
  expect(response.status(), "the person's changes").toBe(200);
  const { items } = (await response.json()) as { items?: readonly { occurredAt?: string }[] };
  return (items ?? []).map((item) => new Date(item.occurredAt ?? ""));
}

function indiaDay(instant: Date): string {
  return new Intl.DateTimeFormat("en-IN", {
    timeZone: "Asia/Kolkata",
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(instant);
}

function isoIndiaDate(instant: Date): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Kolkata",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(instant);
}

function dayBefore(isoDate: string): string {
  const day = new Date(`${isoDate}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() - 1);
  return day.toISOString().slice(0, 10);
}

function typedDate(isoDate: string): string {
  const [year, month, day] = isoDate.split("-");
  return `${day}-${month}-${year}`;
}

async function holdListNavigation(page: Page): Promise<() => void> {
  let release: () => void = () => undefined;
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(
    (url) => url.pathname === auditTrailPath,
    async (route: Route) => {
      if (route.request().headers()["rsc"] !== "1") {
        await route.fallback();
        return;
      }
      await released;
      await route.fallback();
    },
  );
  return release;
}

async function paste(field: Locator, text: string): Promise<void> {
  await field.evaluate((element, pasted) => {
    const clipboardData = new DataTransfer();
    clipboardData.setData("text/plain", pasted);
    // Firefox takes no clipboard from a paste event's init, so the clipboard is the event's own property, where a page's
    // handler reads it in every engine.
    const event = new ClipboardEvent("paste", { bubbles: true, cancelable: true });
    Object.defineProperty(event, "clipboardData", { value: clipboardData });
    element.dispatchEvent(event);
  }, text);
}

async function fitsItsWidth(target: Locator): Promise<boolean> {
  return target.evaluate((element) => element.scrollWidth <= element.clientWidth);
}

test.describe("audit trail", () => {
  test.use({ persona: "administrator" });

  test.describe("before the page is interactive", () => {
    test("keeps the download disabled so no click is lost", async ({ page }) => {
      const auditTrail = new AuditTrailPage(page);
      await withholdClientBundles(page);
      await page.goto(auditTrailPath);

      await expect(auditTrail.heading).toBeVisible();
      await expect(auditTrail.feed).toHaveAttribute("data-hydrating", "");
      await expect(auditTrail.exportButton).toHaveAttribute("data-hydrating", "");
      await expect(auditTrail.exportButton).toBeDisabled();
    });
  });

  test.describe("once Microsoft has ended this browser's session", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 401/ });

    test("the download goes to the sign-in page with the view as the address to return to", async ({
      baseURL,
      entraSession,
      page,
      person,
      request,
    }) => {
      const auditTrail = new AuditTrailPage(page);
      const ownChanges = `?actor=${person?.id ?? ""}`;
      const renewed = sessionRenewed(page);
      await auditTrail.goto(ownChanges);
      await auditTrail.waitUntilInteractive();
      await renewed;
      await endEntraSession(request, entraSession);

      await auditTrail.exportButton.click();

      await expect(page).toHaveURL(
        isSignInPage(new URL(baseURL ?? "").origin, `${auditTrailPath}${ownChanges}`, "session-ended"),
      );
      await expect(new LoginPage(page).notice).toHaveText(sessionEndedNotice);
    });
  });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows the person's own changes by day, filters and pages them, and downloads them", async ({
        page,
        api,
        person,
        capture,
      }) => {
        test.slow();
        const auditTrail = new AuditTrailPage(page);
        const { list } = auditTrail;
        const shell = new AppShell(page);
        const departments = await seedDepartments(api);
        const first = departments[0]!;
        const last = departments[departmentCount - 1]!;
        const personId = person?.id ?? "";
        const name = person?.name ?? "";
        const ownChanges = `?actor=${personId}`;
        const changedAt = await changeTimes(api, personId);
        expect(changedAt, "the person's changes").toHaveLength(21);
        const changedOn = isoIndiaDate(changedAt[0]!);
        test.skip(
          changedAt.some((instant) => isoIndiaDate(instant) !== changedOn),
          "The person's changes straddle midnight in India, so they fall on two days.",
        );
        const changedOnHeading = indiaDay(changedAt[0]!);

        await auditTrail.goto(ownChanges);
        await auditTrail.waitUntilInteractive();

        await expect(page).toHaveTitle(/Audit trail · ERP-AI-Pro/);
        await expect(auditTrail.unavailable).toHaveCount(0);
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 21 changes.`);
        await expect(list.pageLabel).toHaveText("Page 1 of 2");
        await expect(auditTrail.changes).toHaveCount(pageSize);
        await expect(list.textFilter("Changed by id")).toHaveValue(personId);
        expect(await auditTrail.summaries()).toEqual([
          ...[...departments]
            .reverse()
            .map((department) => `${name} deleted the department ${department.name}.`),
          ...[...departments]
            .reverse()
            .map((department) => `${name} created the department ${department.name}.`),
        ]);
        await expect(auditTrail.days.first()).toMatchAriaSnapshot(`
          - listitem:
            - heading "${changedOnHeading}" [level=3]
            - list "${changedOnHeading}":
              - listitem:
                - time: /^\\d{1,2}:\\d{2} [ap]m IST$/
                - paragraph: ${name} deleted the department ${last.name}.
                - link "Show the history of ${last.name}":
                  - /url: ${auditTrailPath}?record=${last.id}
        `);
        const created = auditTrail.change(`${name} created the department ${last.name}.`);
        await expect(created).toMatchAriaSnapshot(`
          - listitem:
            - time: /^\\d{1,2}:\\d{2} [ap]m IST$/
            - paragraph: ${name} created the department ${last.name}.
            - group: Created with 1 value
            - link "Show the history of ${last.name}":
              - /url: ${auditTrailPath}?record=${last.id}
        `);
        await expect(auditTrail.changes.getByRole("link", { name: /^Show changes by / })).toHaveCount(0);
        await capture("audit-trail");

        await created.getByText("Created with 1 value", { exact: true }).click();
        await expect(created.getByRole("group")).toMatchAriaSnapshot(`
          - group:
            - text: Created with 1 value
            - term: Name
            - definition: ${last.name}
        `);
        await capture("audit-trail-created-values", created);

        await list.nextPage.click();
        await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
        await expect(list.pageLabel).toHaveText("Page 2 of 2");
        await expect(list.status).toHaveText("Showing 21–21 of 21 changes.");
        expect(await auditTrail.summaries()).toEqual([`${name} created the person ${name}.`]);
        await list.previousPage.click();
        await expect(page).toHaveURL((url) => url.search === ownChanges);
        await expect(list.pageLabel).toHaveText("Page 1 of 2");

        await expect(auditTrail.card.getByRole("combobox", { name: "Changes per page" })).toBeVisible();
        await list.choosePageSize(50);
        await expect(page).toHaveURL((url) => url.searchParams.get("size") === "50");
        await expect(list.status).toHaveText("Showing all 21 changes.");
        await expect(auditTrail.changes).toHaveCount(21);
        await expect(list.nextPage).toHaveCount(0);
        await list.choosePageSize(pageSize);
        await expect(page).toHaveURL((url) => url.search === ownChanges);
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 21 changes.`);

        await list.chooseOptions("Record type", ["Department"]);
        await list.applyFilters.click();
        await expect(page).toHaveURL(
          (url) => url.searchParams.getAll("type").join() === "identity.users.departments",
        );
        await expect(list.status).toHaveText("Showing all 20 changes.");
        await list.chooseOptions("Action", ["Deleted"]);
        await list.applyFilters.click();
        await expect(page).toHaveURL((url) => url.searchParams.getAll("action").join() === "deleted");
        await expect(list.status).toHaveText("Showing all 10 changes.");
        expect(await auditTrail.summaries()).toEqual(
          [...departments]
            .reverse()
            .map((department) => `${name} deleted the department ${department.name}.`),
        );
        // The router renders the head of a view it navigates to as a deferred value, after the view itself, so a capture
        // that follows a navigation waits for the title too, or the scan can meet a document without one.
        await expect(page).toHaveTitle(/Audit trail · ERP-AI-Pro/);
        await capture("audit-trail-filtered");

        for (const filter of ["Record type", "Action"]) {
          await list.optionsFilter(filter).click();
          await page.getByRole("menuitem", { name: "Clear choices" }).click();
          await expect(page.getByRole("menu")).toHaveCount(0);
        }
        await list.applyFilters.click();
        await expect(page).toHaveURL((url) => url.search === ownChanges);
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 21 changes.`);

        await list.rangeStart("Changed between").fill(typedDate(changedOn));
        await list.rangeEnd("Changed between").fill(typedDate(changedOn));
        await list.applyFilters.click();
        await expect(page).toHaveURL(
          (url) =>
            url.searchParams.get("changedFrom") === changedOn &&
            url.searchParams.get("changedTo") === changedOn,
        );
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 21 changes.`);
        await list.rangeStart("Changed between").fill(typedDate(dayBefore(changedOn)));
        await list.rangeEnd("Changed between").fill(typedDate(dayBefore(changedOn)));
        await list.applyFilters.click();
        await expect(list.noMatches).toContainText("No changes match these filters.");
        await expect(auditTrail.timeline).toHaveCount(0);
        await expect(list.status).toHaveText("No changes to show.");
        await expect(page).toHaveTitle(/Audit trail · ERP-AI-Pro/);
        await capture("audit-trail-no-matches");
        await list.noMatches.getByRole("link", { name: "Clear filters" }).click();
        await expect(page).toHaveURL((url) => url.pathname === auditTrailPath && url.search === "");
        await auditTrail.goto(ownChanges);
        await auditTrail.waitUntilInteractive();

        await list.chooseOptions("Action", ["Created"]);
        await list.applyFilters.click();
        await expect(list.status).toHaveText("Showing all 11 changes.");
        await list.filters.getByRole("link", { name: "Clear filters" }).click();
        await expect(page).toHaveURL((url) => url.pathname === auditTrailPath && url.search === "");
        await expect(list.optionsFilter("Record type")).toBeFocused();
        await expect(list.textFilter("Changed by id")).toHaveValue("");
        await page.goBack();
        await expect(page).toHaveURL((url) => url.searchParams.getAll("action").join() === "created");
        await expect(list.status).toHaveText("Showing all 11 changes.");
        await expect(list.optionsFilter("Action")).toHaveText(/Created/);
        await auditTrail.goto(ownChanges);
        await auditTrail.waitUntilInteractive();

        const history = auditTrail.change(`${name} deleted the department ${first.name}.`);
        const releaseHistory = await holdListNavigation(page);
        await history.getByRole("link", { name: `Show the history of ${first.name}` }).click();
        await expect(auditTrail.changesHeading).toBeFocused();
        await expect(list.status).toHaveText("Loading…");
        await expect(auditTrail.feedItems).toHaveAttribute("aria-busy", "true");
        releaseHistory();
        await expect(page).toHaveURL((url) => url.search === `?record=${first.id}`);
        await expect(list.status).toHaveText("Showing all 2 changes.");
        await expect(auditTrail.feedItems).not.toHaveAttribute("aria-busy");
        await expect(auditTrail.changesHeading).toBeFocused();
        expect(await auditTrail.summaries()).toEqual([
          `${name} deleted the department ${first.name}.`,
          `${name} created the department ${first.name}.`,
        ]);
        await expect(auditTrail.changes.getByRole("link", { name: /^Show the history of / })).toHaveCount(0);
        await expect(list.textFilter("Subject id")).toHaveValue(first.id);
        await auditTrail.changes
          .first()
          .getByRole("link", { name: `Show changes by ${name}` })
          .click();
        await expect(page).toHaveURL((url) => url.search === ownChanges);
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 21 changes.`);
        await expect(auditTrail.changesHeading).toBeFocused();

        const record = list.textFilter("Subject id");
        await record.fill("not-an-id");
        await list.applyFilters.click();
        await expect(record).toBeFocused();
        await expect(record).toHaveAttribute("aria-invalid", "true");
        await expect(list.filters.getByText(idProblem)).toBeVisible();
        await expect(page).toHaveURL((url) => url.search === ownChanges);
        await capture("audit-trail-invalid-id", list.filters);
        await record.fill(first.id.toUpperCase().slice(0, 35));
        await expect(list.filters.getByText(idProblem)).toBeVisible();
        await record.fill("");
        await expect(list.filters.getByText(idProblem)).toHaveCount(0);
        await record.fill("not-an-id");
        await expect(list.filters.getByText(idProblem)).toBeVisible();
        await record.selectText();
        await paste(record, ` ${first.id}\n`);
        await expect(record).toHaveValue(first.id);
        await expect(list.filters.getByText(idProblem)).toHaveCount(0);
        await list.applyFilters.click();
        await expect(page).toHaveURL((url) => url.search === `${ownChanges}&record=${first.id}`);
        await expect(list.status).toHaveText("Showing all 2 changes.");
        await record.fill("");
        await list.applyFilters.click();
        await expect(page).toHaveURL((url) => url.search === ownChanges);
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 21 changes.`);

        await expect(auditTrail.exportButton).toBeEnabled();
        await expect(auditTrail.exportRefusal).toHaveCount(0);
        const exported = await auditTrail.download();
        expect(exported.name).toMatch(/^change-history-\d{8}-\d{6}\.csv$/);
        expect(
          exported.content.subarray(0, byteOrderMark.length).equals(byteOrderMark),
          "a byte order mark",
        ).toBe(true);
        const lines = exported.content.subarray(byteOrderMark.length).toString("utf8").split("\r\n");
        expect(lines[0]).toBe(exportHeader);
        for (const department of departments) {
          expect(
            lines.some((line) => line.includes(`"${department.name}"`)),
            department.name,
          ).toBe(true);
        }
        expect(
          lines.slice(1).every((line) => line === "" || line.includes(`"${personId}"`)),
          "only the person's changes",
        ).toBe(true);
        await expect(shell.toast("Downloaded the changes as a CSV file.")).toBeVisible();
        await expect(page).toHaveURL((url) => url.pathname === auditTrailPath && url.search === ownChanges);
        await expect(auditTrail.exportError).toHaveCount(0);
      });

      test.describe("when the API refuses the download", () => {
        test.use({ expectedConsoleError: /the server responded with a status of 422/ });

        test("explains the refusal beside the button on every try and keeps the page", async ({
          page,
          person,
          capture,
        }) => {
          const auditTrail = new AuditTrailPage(page);
          const reason =
            "These changes hold more than 1,00,000 values, too many for one download. Narrow the filters, for example to fewer days.";
          const ownChanges = `?actor=${person?.id ?? ""}`;
          let answered: Promise<void> = Promise.resolve();
          await page.route(`**${exportApi}?*`, async (route) => {
            await answered;
            await route.fulfill({
              status: 422,
              contentType: "application/problem+json",
              json: {
                type: "/problems/audit.export-too-large",
                title: "The request could not be completed.",
                status: 422,
                detail:
                  "The export would hold more than 100,000 lines; narrow the filter, for example to fewer days.",
                code: "audit.export-too-large",
              },
            });
          });
          await auditTrail.goto(ownChanges);
          await auditTrail.waitUntilInteractive();
          await expect(auditTrail.list.status).toHaveText("Showing 1 change.");

          const refused = page.waitForResponse((response) => new URL(response.url()).pathname === exportApi);
          await auditTrail.exportButton.click();

          expect((await refused).status()).toBe(422);
          await expect(auditTrail.exportError).toHaveText(reason);
          await expect(auditTrail.exportError).toHaveRole("alert");
          await expect(auditTrail.exportButton).toBeEnabled();
          await expect(page).toHaveURL((url) => url.search === ownChanges);
          await capture("audit-trail-download-refused");

          let answer: () => void = () => undefined;
          answered = new Promise<void>((resolve) => {
            answer = resolve;
          });
          const refusedAgain = page.waitForResponse(
            (response) => new URL(response.url()).pathname === exportApi,
          );
          await auditTrail.exportButton.click();
          await expect(auditTrail.exportError).toHaveCount(0);
          await expect(auditTrail.exportButton).toHaveAttribute("aria-busy", "true");
          answer();

          expect((await refusedAgain).status()).toBe(422);
          await expect(auditTrail.exportError).toHaveText(reason);
          await expect(auditTrail.exportError).toHaveRole("alert");
          await expect(auditTrail.exportButton).not.toHaveAttribute("aria-busy");
        });
      });

      test("wraps a long name without a break inside the card at any width", async ({
        page,
        api,
        person,
        capture,
      }) => {
        const auditTrail = new AuditTrailPage(page);
        const name = person?.name ?? "";
        const longName = `e2e_audit_${randomUUID().replaceAll("-", "").repeat(3)}`.slice(0, 100);
        const department = await createDepartment(api, longName);
        await deleteDepartment(api, department);

        await auditTrail.goto(`?actor=${person?.id ?? ""}`);
        await auditTrail.waitUntilInteractive();
        await expect(auditTrail.list.status).toHaveText("Showing all 3 changes.");
        const created = auditTrail.change(`${name} created the department ${longName}.`);
        await created.getByText("Created with 1 value", { exact: true }).click();
        await expect(created.getByRole("definition")).toHaveText(longName);
        await expect(
          auditTrail
            .change(`${name} deleted the department ${longName}.`)
            .getByRole("link", { name: `Show the history of ${longName}` }),
        ).toBeVisible();

        expect(await fitsItsWidth(auditTrail.card), "the changes card fits its width").toBe(true);
        expect(await fitsItsWidth(page.locator("html")), "the page fits its width").toBe(true);
        await capture("audit-trail-long-name", auditTrail.card);
      });
    });
  }
});

test.describe("audit trail for an employee", () => {
  forEachTheme("tells them they have no access to it", async ({ page, capture }) => {
    await page.goto(auditTrailPath);

    await expect(
      page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
    ).toBeVisible();
    await expect(page).toHaveTitle(/No access · ERP-AI-Pro/);
    await expect(page.getByTestId("audit-changes-card")).toHaveCount(0);
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - main:
        - heading "You do not have access to this page" [level=1]
        - paragraph: Ask an administrator for access.
        - link "Go to the home page"
    `);
    await capture("audit-trail-no-access");
  });

  test("the API refuses them the changes and their download", async ({ api }) => {
    for (const path of [changesApi, exportApi]) {
      const refused = await api.get(path);
      expect(refused.status(), path).toBe(403);
      expect(await refused.json(), path).toMatchObject({ code: "request.forbidden" });
    }
  });
});
