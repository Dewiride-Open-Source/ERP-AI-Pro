import {
  createDepartmentRecord,
  createDesignationRecord,
  deactivatePersonRecord,
  personRecord,
  recordMoveRecord,
  registerPersonRecord,
  removeRecords,
  uniquePrefix,
  type NamedRecord,
  type PersonRecord,
} from "../../../../fixtures/person-records";
import { signIn } from "../../../../fixtures/sign-in";
import { expect, test, themes, type SignedInApi } from "../../../../fixtures/test";
import {
  OrgChartPage,
  orgChartAddress,
  orgChartApi,
  orgChartPath,
} from "../../../../pages/identity/users/org-chart.page";
import { personPath, typedDate } from "../../../../pages/identity/users/person.page";
import { AppShell, showsBreadcrumbs } from "../../../../pages/shared/layout/app-shell.page";

const joinedOn = "2025-04-01";

const quoted = JSON.stringify;

type Organisation = {
  readonly prefix: string;
  readonly operations: NamedRecord;
  readonly north: NamedRecord;
  readonly headOf: NamedRecord;
  readonly senior: NamedRecord;
  readonly head: PersonRecord;
  readonly lead: PersonRecord;
  readonly analyst: PersonRecord;
  readonly clerk: PersonRecord;
  readonly orphan: PersonRecord;
  readonly leaver: PersonRecord;
  readonly promotedOn: string;
};

function isoDateAfter(days: number): string {
  const day = new Date();
  day.setUTCDate(day.getUTCDate() + days);
  return day.toISOString().slice(0, 10);
}

function todayInIndia(): string {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Kolkata",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((candidate) => candidate.type === type)?.value;
  return `${part("year")}-${part("month")}-${part("day")}`;
}

// The departments, designations and people are the test's own, under a prefix no other worker uses, and the chart is read
// for the test's department, so no other worker's people reach its assertions. The leaver manages Zoya and is deactivated
// afterwards, so Zoya names a manager outside the chart.
async function seedOrganisation(api: SignedInApi, feature: string): Promise<Organisation> {
  const prefix = uniquePrefix(feature);
  const promotedOn = isoDateAfter(400);
  const operations = await createDepartmentRecord(api, `${prefix} Operations`);
  const north = await createDepartmentRecord(api, `${prefix} Operations north`, operations.id);
  const headOf = await createDesignationRecord(api, `${prefix} Head of operations`);
  const senior = await createDesignationRecord(api, `${prefix} Senior lead`);
  const register = (name: string, slug: string) =>
    registerPersonRecord(api, {
      displayName: `${prefix} ${name}`,
      workEmail: `${prefix}-${slug}@example.com`,
      dateOfJoining: joinedOn,
    });
  const head = await register("Asha Head", "head");
  const lead = await register("Vikram Lead", "lead");
  const analyst = await register("Meera Analyst", "analyst");
  const clerk = await register("Arjun Clerk", "clerk");
  const orphan = await register("Zoya Orphan", "orphan");
  const leaver = await register("Ravi Leaver", "leaver");
  await recordMoveRecord(api, head.id, {
    effectiveFrom: joinedOn,
    departmentId: operations.id,
    designationId: headOf.id,
  });
  await recordMoveRecord(api, lead.id, {
    effectiveFrom: joinedOn,
    departmentId: north.id,
    managerId: head.id,
  });
  await recordMoveRecord(api, lead.id, {
    effectiveFrom: promotedOn,
    departmentId: north.id,
    designationId: senior.id,
    managerId: head.id,
  });
  await recordMoveRecord(api, analyst.id, {
    effectiveFrom: joinedOn,
    departmentId: north.id,
    managerId: lead.id,
  });
  await recordMoveRecord(api, clerk.id, {
    effectiveFrom: joinedOn,
    departmentId: operations.id,
    managerId: head.id,
  });
  await recordMoveRecord(api, leaver.id, { effectiveFrom: joinedOn, departmentId: operations.id });
  await recordMoveRecord(api, orphan.id, {
    effectiveFrom: joinedOn,
    departmentId: operations.id,
    managerId: leaver.id,
  });
  await deactivatePersonRecord(api, await personRecord(api, leaver.id));
  return {
    prefix,
    operations,
    north,
    headOf,
    senior,
    head,
    lead,
    analyst,
    clerk,
    orphan,
    leaver,
    promotedOn,
  };
}

function removeOrganisation(api: SignedInApi, organisation: Organisation): Promise<void> {
  const { analyst, orphan, clerk, lead, head, leaver, north, operations, headOf, senior } = organisation;
  return removeRecords(api, {
    people: [analyst.id, orphan.id, clerk.id, lead.id, head.id, leaver.id],
    departments: [north.id, operations.id],
    designations: [headOf.id, senior.id],
  });
}

function nameLine(person: PersonRecord, linked: boolean): string[] {
  return linked
    ? [`- paragraph:`, `  - link ${quoted(person.displayName)}:`, `    - /url: ${personPath(person.id)}`]
    : [`- paragraph: ${quoted(person.displayName)}`];
}

function indented(lines: readonly string[], spaces: number): string[] {
  return lines.map((line) => `${" ".repeat(spaces)}${line}`);
}

// The chart of the operations department today: Asha manages Arjun and Vikram, who manages Meera, and Zoya reports to the
// leaver, whose record is deactivated.
function operationsChart(organisation: Organisation, { linked }: { readonly linked: boolean }): string {
  const { head, clerk, lead, orphan, leaver, operations, north, headOf } = organisation;
  return [
    `- list "Today's chart":`,
    "  - listitem:",
    `    - button ${quoted(`Show the people who report to ${head.displayName}`)} [expanded]`,
    ...indented(nameLine(head, linked), 4),
    `    - paragraph: ${quoted(`${headOf.name} · ${operations.name}`)}`,
    "    - paragraph: 2 people report to them",
    `    - list ${quoted(`People who report to ${head.displayName}`)}:`,
    "      - listitem:",
    ...indented(nameLine(clerk, linked), 8),
    `        - paragraph: ${quoted(`No designation · ${operations.name}`)}`,
    "      - listitem:",
    `        - button ${quoted(`Show the people who report to ${lead.displayName}`)}`,
    ...indented(nameLine(lead, linked), 8),
    `        - paragraph: ${quoted(`No designation · ${north.name}`)}`,
    "        - paragraph: 1 person reports to them",
    "  - listitem:",
    ...indented(nameLine(orphan, linked), 4),
    `    - paragraph: ${quoted(`No designation · ${operations.name}`)}`,
    `    - paragraph: ${quoted(`Reports to ${leaver.displayName}, who is not on this chart.`)}`,
  ].join("\n");
}

test.describe("organisation chart", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows a department's people under their managers, folded below the second level, with Expand all and Collapse all", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const organisation = await seedOrganisation(api, "org-chart");
        const { head, lead, analyst, clerk, operations } = organisation;
        try {
          const chart = new OrgChartPage(page);
          await chart.goto(orgChartAddress({ department: operations.id }));
          await chart.waitUntilInteractive();

          await expect(page).toHaveTitle(/Organisation chart · ERP-AI-Pro/);
          await expect(chart.chartHeading).toHaveText("Today's chart");
          await expect(chart.status).toHaveText("5 people are on this chart.");
          await expect(chart.department).toHaveValue(operations.name);
          await expect(chart.date).toHaveValue(typedDate(todayInIndia()));
          await expect(chart.people).toMatchAriaSnapshot(operationsChart(organisation, { linked: true }));
          await expect(chart.toggle(head.displayName)).toHaveAttribute("aria-expanded", "true");
          await expect(chart.toggle(lead.displayName)).toHaveAttribute("aria-expanded", "false");
          await expect(chart.row(analyst.displayName)).toBeHidden();
          await expect(chart.offChartNote).toHaveText(
            "A person is left off the chart when their record is deactivated or deleted, or when they had not joined by the chart's date.",
          );
          await capture("org-chart", chart.card);

          await chart.toggle(lead.displayName).click();
          await expect(chart.toggle(lead.displayName)).toHaveAttribute("aria-expanded", "true");
          await expect(chart.reportsList(lead.displayName)).toMatchAriaSnapshot(`
            - list ${quoted(`People who report to ${lead.displayName}`)}:
              - listitem:
                - paragraph:
                  - link ${quoted(analyst.displayName)}:
                    - /url: ${personPath(analyst.id)}
                - paragraph: ${quoted(`No designation · ${organisation.north.name}`)}
          `);

          await chart.collapseAll.click();
          await expect(chart.reportsList(head.displayName)).toBeHidden();
          await expect(chart.row(clerk.displayName)).toBeHidden();
          await expect(chart.collapseAll).toBeFocused();
          await capture("org-chart-collapsed", chart.people);
          await chart.expandAll.click();
          await expect(chart.row(analyst.displayName)).toBeVisible();
          await expect(chart.row(clerk.displayName)).toBeVisible();
          await expect(chart.expandAll).toBeFocused();

          await chart.toggle(head.displayName).focus();
          await page.keyboard.press("Enter");
          await expect(chart.toggle(head.displayName)).toHaveAttribute("aria-expanded", "false");
          await page.keyboard.press("Space");
          await expect(chart.toggle(head.displayName)).toHaveAttribute("aria-expanded", "true");

          await chart.row(head.displayName).getByRole("link", { name: head.displayName }).click();
          await expect(page).toHaveURL((url) => url.pathname === personPath(head.id));
          await expect(page.getByRole("main").getByRole("heading", { level: 1 })).toHaveText(
            head.displayName,
          );
          await page.goBack();
          await expect(chart.chartHeading).toHaveText("Today's chart");
        } finally {
          await removeOrganisation(api, organisation);
        }
      });

      test("filters by a department chosen from the list, explaining a manager who works elsewhere, and shows every department again", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const organisation = await seedOrganisation(api, "org-chart-filter");
        const { head, lead, analyst, operations, north } = organisation;
        try {
          const chart = new OrgChartPage(page);
          await chart.goto(orgChartAddress({ department: operations.id }));
          await chart.waitUntilInteractive();
          await expect(chart.filters).toMatchAriaSnapshot(`
            - search "Choose what the chart shows":
              - group:
                - text: Department
                - group:
                  - combobox "Department"
                  - group:
                    - button "Clear Department"
                    - button "Show options Department"
                  - status
                - paragraph: Shows the people in it and in the departments under it.
              - group:
                - text: Date
                - group:
                  - textbox "Date":
                    - /placeholder: dd-mm-yyyy
                  - group:
                    - button "Choose date"
                - paragraph: Today unless you choose another day, past or future.
              - button "Show"
              - link "Show every department":
                - /url: ${orgChartPath}
          `);
          await capture("org-chart-filters", chart.filters);

          await chart.chooseDepartment(north.name, `${operations.name} › ${north.name}`);
          await chart.show.click();
          await expect(page).toHaveURL(
            (url) => url.pathname === orgChartPath && url.searchParams.get("department") === north.id,
          );
          await expect(chart.status).toHaveText("2 people are on this chart.");
          expect(await chart.rootNames()).toEqual([lead.displayName]);
          await expect(chart.row(lead.displayName).getByTestId("org-chart-manager-note")).toHaveText(
            `Reports to ${head.displayName}, who is outside ${north.name}.`,
          );
          await expect(chart.row(analyst.displayName)).toBeVisible();
          await expect(chart.show).toBeFocused();
          await capture("org-chart-department", chart.people);

          await chart.everyDepartment.click();
          await expect(page).toHaveURL((url) => url.pathname === orgChartPath && url.search === "");
          await expect(chart.department).toHaveValue("");
          await expect(chart.department).toBeFocused();
        } finally {
          await removeOrganisation(api, organisation);
        }
      });

      test("shows another date's chart to those who see moves, refusing a date it cannot read in the browser", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const organisation = await seedOrganisation(api, "org-chart-date");
        const { lead, operations, north, senior, promotedOn } = organisation;
        try {
          const chart = new OrgChartPage(page);
          const departmentChart = orgChartAddress({ department: operations.id });
          await chart.goto(departmentChart);
          await chart.waitUntilInteractive();
          await expect(chart.row(lead.displayName)).toContainText(`No designation · ${north.name}`);

          await chart.date.fill("31-02-2026");
          await chart.show.click();
          await expect(chart.date).toBeFocused();
          await expect(chart.date).toHaveAttribute("aria-invalid", "true");
          await expect(
            chart.filters.getByText("Enter a real date as day-month-year, for example 31-03-2026."),
          ).toBeVisible();
          await expect(page).toHaveURL((url) => url.searchParams.get("on") === null);
          await capture("org-chart-date-refused", chart.filters);

          await chart.dateTrigger.click();
          const calendar = page.getByRole("dialog", { name: "Choose date", exact: true });
          await expect(calendar).toBeVisible();
          await page.keyboard.press("Escape");
          await expect(calendar).toBeHidden();

          await chart.date.fill(typedDate(promotedOn));
          await chart.show.click();
          await expect(page).toHaveURL(
            (url) =>
              url.searchParams.get("department") === operations.id &&
              url.searchParams.get("on") === promotedOn,
          );
          await expect(chart.chartHeading).toHaveText(`Chart on ${typedDate(promotedOn)}`);
          await expect(chart.row(lead.displayName)).toContainText(`${senior.name} · ${north.name}`);
          await capture("org-chart-another-date", chart.card);

          await chart.today.click();
          await expect(page).toHaveURL(
            (url) =>
              url.searchParams.get("department") === operations.id && url.searchParams.get("on") === null,
          );
          await expect(chart.chartHeading).toHaveText("Today's chart");
          await expect(chart.row(lead.displayName)).toContainText(`No designation · ${north.name}`);

          await chart.goto(orgChartAddress({ department: operations.id, on: "2025-03-31" }));
          await expect(chart.empty).toHaveText(
            `Nobody on this chart is in ${operations.name} or the departments under it.`,
          );
          await expect(chart.status).toHaveText("Nobody is on this chart.");
        } finally {
          await removeOrganisation(api, organisation);
        }
      });
    });
  }

  test("pages through more people at the top of the chart than a page holds, keeping the address canonical", async ({
    page,
    api,
  }) => {
    test.slow();
    const prefix = uniquePrefix("org-chart-pages");
    const department = await createDepartmentRecord(api, `${prefix} Pages`);
    const people: PersonRecord[] = [];
    try {
      for (let index = 1; index <= 21; index += 1) {
        const number = String(index).padStart(2, "0");
        const person = await registerPersonRecord(api, {
          displayName: `${prefix} Person ${number}`,
          workEmail: `${prefix}-${number}@example.com`,
        });
        people.push(person);
        await recordMoveRecord(api, person.id, { effectiveFrom: joinedOn, departmentId: department.id });
      }
      const chart = new OrgChartPage(page);
      await chart.goto(orgChartAddress({ department: department.id }));
      await chart.waitUntilInteractive();

      await expect(chart.status).toHaveText("21 people are on this chart.");
      await expect(chart.pageLabel).toHaveText("Page 1 of 2");
      expect(await chart.rootNames()).toEqual(people.slice(0, 20).map((person) => person.displayName));
      await expect(chart.pageLink("Previous page")).toHaveAttribute("aria-disabled", "true");

      await chart.pageLink("Next page").click();
      await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
      await expect(chart.pageLabel).toHaveText("Page 2 of 2");
      expect(await chart.rootNames()).toEqual([people[20]?.displayName ?? ""]);
      await expect(chart.pageLink("Next page")).toHaveAttribute("aria-disabled", "true");
      await expect(chart.pageLink("Next page")).toBeFocused();

      await chart.pageLink("First page").click();
      await expect(page).toHaveURL((url) => url.searchParams.get("page") === null);
      await expect(chart.pageLabel).toHaveText("Page 1 of 2");

      await page.goto(orgChartAddress({ department: department.id, page: 9 }));
      await expect(page).toHaveURL((url) => url.searchParams.get("page") === "2");
      await expect(chart.pageLabel).toHaveText("Page 2 of 2");
    } finally {
      await removeRecords(api, { people: people.map((person) => person.id), departments: [department.id] });
    }
  });

  test("answers an address that names the view another way with the canonical one", async ({ page }) => {
    const chart = new OrgChartPage(page);

    await page.goto(`${orgChartPath}?department=sales&sort=name&page=1`);
    await expect(page).toHaveURL((url) => url.pathname === orgChartPath && url.search === "");
    await expect(chart.heading).toBeVisible();

    await page.goto(`${orgChartPath}?on=${todayInIndia()}`);
    await expect(page).toHaveURL((url) => url.pathname === orgChartPath && url.search === "");
    await expect(chart.chartHeading).toHaveText("Today's chart");
  });

  test("keeps Organisation chart marked in the navigation and ends the breadcrumb there", async ({
    page,
  }) => {
    const shell = new AppShell(page);
    await new OrgChartPage(page).goto();
    await shell.waitUntilInteractive();

    await shell.openNavigation();
    await expect(shell.navigationLink("Organisation chart")).toHaveAttribute("aria-current", "page");
    await expect(shell.navigationLink("People")).not.toHaveAttribute("aria-current");
    await shell.closeDrawer();
    if (showsBreadcrumbs(page.viewportSize())) {
      await expect(shell.breadcrumbs).toMatchAriaSnapshot(`
        - navigation "Breadcrumb":
          - list:
            - listitem:
              - link "Home":
                - /url: /
            - listitem: Organisation
            - listitem:
              - link "Organisation chart" [disabled]
      `);
    }
  });
});

test.describe("organisation chart for an employee", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows today's chart without links or a date, and explains that another date is not open to them", async ({
        context,
        page,
        api,
        capture,
      }) => {
        test.slow();
        await signIn(context.request, "administrator");
        const organisation = await seedOrganisation(api, "org-chart-employee");
        const { operations } = organisation;
        try {
          await signIn(context.request, "accountant");
          const chart = new OrgChartPage(page);
          await chart.goto(orgChartAddress({ department: operations.id }));
          await chart.waitUntilInteractive();

          await expect(chart.date).toHaveCount(0);
          await expect(chart.people).toMatchAriaSnapshot(operationsChart(organisation, { linked: false }));
          await expect(chart.people.getByRole("link")).toHaveCount(0);
          await capture("org-chart-employee", chart.card);

          await chart.goto(orgChartAddress({ department: operations.id, on: "2025-06-01" }));
          await expect(chart.otherDateRefused).toMatchAriaSnapshot(`
            - status:
              - paragraph: You can see only today's chart.
              - paragraph: The chart of another date shows where people were or will be, which only administrators can see.
              - link "Show today's chart":
                - /url: ${orgChartAddress({ department: operations.id })}
          `);
          await expect(chart.chartHeading).toHaveText(`Chart on ${typedDate("2025-06-01")}`);
          await capture("org-chart-employee-other-date", chart.card);
          await chart.otherDateRefused.getByRole("link", { name: "Show today's chart" }).click();
          await expect(page).toHaveURL((url) => url.searchParams.get("on") === null);
          await expect(chart.chartHeading).toHaveText("Today's chart");

          const refused = await api.get(`${orgChartApi}?on=2025-06-01`);
          expect(refused.status()).toBe(403);
          expect(await refused.json()).toMatchObject({ code: "request.forbidden" });
        } finally {
          // The open page renews its session once it has loaded, and a sign-in through the same browser context while that
          // renewal is under way leaves the page sending one person's request token with the next person's cookie.
          await page.goto("about:blank");
          await signIn(context.request, "administrator");
          await removeOrganisation(api, organisation);
        }
      });
    });
  }

  test("is a page the employee may open from the navigation", async ({ page }) => {
    const shell = new AppShell(page);
    await page.goto("/");
    await shell.waitUntilInteractive();
    await shell.openNavigation();
    await shell.navigationLink("Organisation chart").click();

    await expect(page).toHaveURL((url) => url.pathname === orgChartPath);
    await expect(new OrgChartPage(page).heading).toBeVisible();
  });
});
