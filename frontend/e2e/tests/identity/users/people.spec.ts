import type { Locator, Page } from "@playwright/test";

import { withholdClientBundles } from "../../../fixtures/client-bundles";
import {
  createDepartmentRecord,
  createDesignationRecord,
  deactivatePersonRecord,
  peopleApi,
  recordMoveRecord,
  registerPersonRecord,
  removeRecords,
  uniquePrefix,
  type NamedRecord,
  type PersonRole,
} from "../../../fixtures/person-records";
import { expect, forEachTheme, test, themes, type SignedInApi } from "../../../fixtures/test";
import { PeoplePage, peoplePath } from "../../../pages/identity/users/people.page";

const pageSize = 10;

type PersonSeed = {
  readonly firstName: string;
  readonly dateOfJoining?: string;
  readonly roles?: readonly PersonRole[];
};

// Eleven people fill one page of ten and one more. Their names sort in this order, while their employee codes and work emails
// sort the other way round, so a sort by either shows the last of them first.
const personSeeds: readonly PersonSeed[] = [
  { firstName: "Aarav", dateOfJoining: "2026-01-05", roles: ["finance", "hr"] },
  { firstName: "Bhavna", dateOfJoining: "2025-06-10", roles: ["manager"] },
  { firstName: "Chirag", dateOfJoining: "2024-03-15" },
  { firstName: "Devika" },
  { firstName: "Eshan" },
  { firstName: "Farah" },
  { firstName: "Gautam" },
  { firstName: "Harini" },
  { firstName: "Ishaan" },
  { firstName: "Jaya" },
  { firstName: "Kabir" },
];

type Created = {
  readonly people: string[];
  readonly departments: string[];
  readonly designations: string[];
};

type SeededPerson = {
  readonly id: string;
  readonly displayName: string;
  readonly workEmail: string;
  readonly employeeCode: string;
};

type SeededPeople = {
  readonly prefix: string;
  readonly people: readonly SeededPerson[];
  readonly department: NamedRecord;
  readonly designation: NamedRecord;
};

function nothingCreated(): Created {
  return { people: [], departments: [], designations: [] };
}

// Every worker lists the same people, so a test lists only the ones it registered, under a prefix of its own, and never counts
// anyone else's. Each record joins the test's list as soon as it exists, so a seeding that fails half-way leaves nothing.
async function seedPeople(api: SignedInApi, created: Created): Promise<SeededPeople> {
  const prefix = uniquePrefix("people");
  const token = prefix.slice(-12);
  const department = await createDepartmentRecord(api, `${prefix} Sales`);
  created.departments.push(department.id);
  const designation = await createDesignationRecord(api, `${prefix} Analyst`);
  created.designations.push(designation.id);

  const people: (SeededPerson & { readonly version: string })[] = [];
  for (const [index, seed] of personSeeds.entries()) {
    const number = String(personSeeds.length - index).padStart(2, "0");
    const registered = await registerPersonRecord(api, {
      displayName: `${prefix} ${seed.firstName}`,
      workEmail: `${prefix}-p${number}@example.com`,
      employeeCode: `${token}-${number}`,
      ...(seed.dateOfJoining === undefined ? {} : { dateOfJoining: seed.dateOfJoining }),
      roles: seed.roles ?? [],
    });
    created.people.push(registered.id);
    people.push({
      id: registered.id,
      displayName: registered.displayName,
      workEmail: registered.workEmail,
      employeeCode: registered.employeeCode ?? "",
      version: registered.version,
    });
  }

  const [aarav, bhavna, chirag] = people;
  await recordMoveRecord(api, aarav!.id, {
    effectiveFrom: "2026-01-05",
    departmentId: department.id,
    designationId: designation.id,
  });
  await recordMoveRecord(api, bhavna!.id, {
    effectiveFrom: "2025-06-10",
    departmentId: department.id,
    managerId: aarav!.id,
  });
  await deactivatePersonRecord(api, chirag!);

  return {
    prefix,
    people: people.map(({ id, displayName, workEmail, employeeCode }) => ({
      id,
      displayName,
      workEmail,
      employeeCode,
    })),
    department,
    designation,
  };
}

// The people were registered managers first, so the reversed list removes each report before their manager.
function removeSeeded(api: SignedInApi, created: Created): Promise<void> {
  return removeRecords(api, {
    people: [...created.people].reverse(),
    departments: created.departments,
    designations: created.designations,
  });
}

function names(people: readonly SeededPerson[]): string[] {
  return people.map((person) => person.displayName);
}

async function expectAddress(page: Page, search: string): Promise<void> {
  await expect(page).toHaveURL((url) => url.pathname === peoplePath && url.search === search);
}

async function fitsItsWidth(target: Locator): Promise<boolean> {
  return target.evaluate((element) => element.scrollWidth <= element.clientWidth);
}

test.describe("people list", () => {
  test.use({ persona: "administrator" });

  test.describe("before the page is interactive", () => {
    test("keeps the list's controls disabled so no click is lost", async ({ page }) => {
      const people = new PeoplePage(page);
      await withholdClientBundles(page);
      await page.goto(peoplePath);

      await expect(people.heading).toBeVisible();
      await expect(people.list.root).toHaveAttribute("data-hydrating", "");
      await expect(people.list.columnsButton).toBeDisabled();
    });
  });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("lists people with every detail, finds them by name and opens a person from their name", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const people = new PeoplePage(page);
        const { list } = people;
        const created = nothingCreated();

        try {
          const seeded = await seedPeople(api, created);
          const [aarav, bhavna, chirag] = seeded.people;
          await people.goto();
          await people.waitUntilInteractive();

          await expect(page).toHaveTitle(/People · ERP-AI-Pro/);
          await expect(people.unavailable).toHaveCount(0);
          await expect(page.getByRole("main").getByText("Organisation", { exact: true })).toBeVisible();
          await expect(people.registerLink).toHaveAttribute("href", `${peoplePath}/new`);
          await expect(people.permissionsLink).toHaveAttribute("href", `${peoplePath}/permissions`);
          await expect(list.filters).toMatchAriaSnapshot(`
            - search "Filter people":
              - group:
                - text: Name
                - group:
                  - group
                  - searchbox "Name"
              - group:
                - text: Work email
                - group:
                  - group
                  - searchbox "Work email"
              - group:
                - text: Employee code
                - group:
                  - group
                  - searchbox "Employee code"
              - group:
                - text: Status
                - button "Status Any"
              - group:
                - text: Department
                - button "Department Any"
              - group:
                - text: Designation
                - button "Designation Any"
              - group:
                - text: Joined between From
                - group:
                  - textbox "Joined between From"
                - text: To
                - group:
                  - textbox "Joined between To"
                - button "Choose dates"
              - button "Apply filters"
          `);

          const name = list.textFilter("Name");
          await name.fill(seeded.prefix);
          await name.press("Enter");
          await expectAddress(page, `?name=${seeded.prefix}`);
          await expect(name).toBeFocused();
          await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 people. Sorted by Name, A to Z.`);
          await expect(list.pageLabel).toHaveText("Page 1 of 2");
          await expect.poll(() => list.titles()).toEqual(names(seeded.people.slice(0, pageSize)));

          if (await list.showsCards()) {
            await expect(list.table).toBeHidden();
            await expect(people.row(aarav!.displayName)).toMatchAriaSnapshot(`
              - listitem:
                - link "${aarav!.displayName}":
                  - /url: ${peoplePath}/${aarav!.id}
                - term: Work email
                - definition: ${aarav!.workEmail}
                - term: Employee code
                - definition: ${aarav!.employeeCode}
                - term: Department
                - definition: ${seeded.department.name}
                - term: Designation
                - definition: ${seeded.designation.name}
                - term: Manager
                - definition: —
                - term: Joined
                - definition: 05-01-2026
                - term: Status
                - definition: Active
                - term: Roles in the ERP
                - definition: Finance, HR
                - term: Last signed in
                - definition: Not signed in yet
            `);
            await expect(people.row(bhavna!.displayName)).toMatchAriaSnapshot(`
              - listitem:
                - link "${bhavna!.displayName}":
                  - /url: ${peoplePath}/${bhavna!.id}
                - term: Work email
                - definition: ${bhavna!.workEmail}
                - term: Employee code
                - definition: ${bhavna!.employeeCode}
                - term: Department
                - definition: ${seeded.department.name}
                - term: Designation
                - definition: —
                - term: Manager
                - definition: ${aarav!.displayName}
                - term: Joined
                - definition: 10-06-2025
                - term: Status
                - definition: Active
                - term: Roles in the ERP
                - definition: Manager
                - term: Last signed in
                - definition: Not signed in yet
            `);
            await expect(people.row(chirag!.displayName)).toMatchAriaSnapshot(`
              - listitem:
                - link "${chirag!.displayName}":
                  - /url: ${peoplePath}/${chirag!.id}
                - term: Work email
                - definition: ${chirag!.workEmail}
                - term: Employee code
                - definition: ${chirag!.employeeCode}
                - term: Department
                - definition: —
                - term: Designation
                - definition: —
                - term: Manager
                - definition: —
                - term: Joined
                - definition: 15-03-2024
                - term: Status
                - definition: Deactivated
                - term: Roles in the ERP
                - definition: None
                - term: Last signed in
                - definition: Not signed in yet
            `);
          } else {
            await expect(list.cardList).toBeHidden();
            await expect(list.columnHeader("Name")).toHaveAttribute("aria-sort", "ascending");
            await expect(list.table.getByRole("row").first()).toMatchAriaSnapshot(`
              - row:
                - columnheader "Name":
                  - button "Name"
                - columnheader "Work email":
                  - button "Work email"
                - columnheader "Employee code":
                  - button "Employee code"
                - columnheader "Department":
                  - button "Department"
                - columnheader "Designation":
                  - button "Designation"
                - columnheader "Manager":
                  - button "Manager"
                - columnheader "Joined":
                  - button "Joined"
                - columnheader "Status":
                  - button "Status"
                - columnheader "Roles in the ERP"
                - columnheader "Last signed in":
                  - button "Last signed in"
            `);
            await expect(people.row(aarav!.displayName)).toMatchAriaSnapshot(`
              - row:
                - rowheader "${aarav!.displayName}":
                  - link "${aarav!.displayName}":
                    - /url: ${peoplePath}/${aarav!.id}
                - cell "${aarav!.workEmail}"
                - cell "${aarav!.employeeCode}"
                - cell "${seeded.department.name}"
                - cell "${seeded.designation.name}"
                - cell "—"
                - cell "05-01-2026"
                - cell "Active"
                - cell "Finance, HR"
                - cell "Not signed in yet"
            `);
            await expect(people.row(bhavna!.displayName)).toMatchAriaSnapshot(`
              - row:
                - rowheader "${bhavna!.displayName}":
                  - link "${bhavna!.displayName}":
                    - /url: ${peoplePath}/${bhavna!.id}
                - cell "${bhavna!.workEmail}"
                - cell "${bhavna!.employeeCode}"
                - cell "${seeded.department.name}"
                - cell "—"
                - cell "${aarav!.displayName}"
                - cell "10-06-2025"
                - cell "Active"
                - cell "Manager"
                - cell "Not signed in yet"
            `);
            await expect(people.row(chirag!.displayName)).toMatchAriaSnapshot(`
              - row:
                - rowheader "${chirag!.displayName}":
                  - link "${chirag!.displayName}":
                    - /url: ${peoplePath}/${chirag!.id}
                - cell "${chirag!.workEmail}"
                - cell "${chirag!.employeeCode}"
                - cell "—"
                - cell "—"
                - cell "—"
                - cell "15-03-2024"
                - cell "Deactivated"
                - cell "None"
                - cell "Not signed in yet"
            `);

            // Ten columns are wider than the card on a desktop screen, so the table scrolls sideways inside the card and its
            // scrolling area takes keyboard focus.
            await expect(page.getByRole("region", { name: "All people" })).toHaveAttribute("tabindex", "0");
            const lastColumn = list.columnHeader("Last signed in");
            await expect(lastColumn).not.toBeInViewport();
            await list.columnHeader("Name").hover();
            await page.mouse.wheel(4000, 0);
            await expect(lastColumn).toBeInViewport();
            await page.mouse.wheel(-4000, 0);
            await expect(lastColumn).not.toBeInViewport();
          }
          expect(await fitsItsWidth(people.listCard), "the list stays inside its card").toBe(true);
          await capture("people");

          await people.nameLink(aarav!.displayName).click();
          await expect(page).toHaveURL((url) => url.pathname === `${peoplePath}/${aarav!.id}`);
          await expect(page.getByRole("main").getByRole("heading", { level: 1 })).toContainText(
            aarav!.displayName,
          );
        } finally {
          await removeSeeded(api, created);
        }
      });

      test("filters by email, employee code, status, department, designation and joining date", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const people = new PeoplePage(page);
        const { list } = people;
        const created = nothingCreated();

        try {
          const seeded = await seedPeople(api, created);
          const [aarav, bhavna, chirag] = seeded.people;
          const { prefix, department, designation } = seeded;
          await people.goto(`?name=${prefix}`);
          await people.waitUntilInteractive();
          await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 people. Sorted by Name, A to Z.`);

          const email = list.textFilter("Work email");
          await email.fill(chirag!.workEmail.slice(0, chirag!.workEmail.indexOf("@")));
          await email.press("Enter");
          await expect(page).toHaveURL((url) => url.searchParams.get("email") === `${prefix}-p09`);
          await expect(list.status).toHaveText("Showing 1 person.");
          await expect.poll(() => list.titles()).toEqual([chirag!.displayName]);

          await email.fill("");
          const code = list.textFilter("Employee code");
          await code.fill(aarav!.employeeCode);
          await code.press("Enter");
          await expectAddress(page, `?name=${prefix}&code=${aarav!.employeeCode}`);
          await expect(list.status).toHaveText("Showing 1 person.");
          await expect.poll(() => list.titles()).toEqual([aarav!.displayName]);

          await code.fill("");
          await list.chooseOptions("Status", ["Deactivated"]);
          await expect(list.optionsFilter("Status")).toHaveAccessibleName("Status Deactivated");
          await list.applyFilters.click();
          await expectAddress(page, `?name=${prefix}&status=deactivated`);
          await expect(list.status).toHaveText("Showing 1 person.");
          await expect.poll(() => list.titles()).toEqual([chirag!.displayName]);

          await list.optionsFilter("Status").click();
          await page.getByRole("menuitem", { name: "Clear choices" }).click();
          await expect(page.getByRole("menu")).toHaveCount(0);
          await list.chooseOptions("Department", [department.name]);
          await list.applyFilters.click();
          await expectAddress(page, `?name=${prefix}&department=${department.id}`);
          await expect(list.status).toHaveText("Showing all 2 people. Sorted by Name, A to Z.");
          await expect.poll(() => list.titles()).toEqual([aarav!.displayName, bhavna!.displayName]);

          await list.chooseOptions("Designation", [designation.name]);
          await list.applyFilters.click();
          await expectAddress(
            page,
            `?name=${prefix}&department=${department.id}&designation=${designation.id}`,
          );
          await expect(list.status).toHaveText("Showing 1 person.");
          await expect.poll(() => list.titles()).toEqual([aarav!.displayName]);
          // The router renders the head of a view it navigates to after the view itself, so a capture that follows a
          // navigation waits for the title too, or the scan can meet a document without one.
          await expect(page).toHaveTitle(/People · ERP-AI-Pro/);
          await capture("people-filtered");

          for (const filter of ["Department", "Designation"]) {
            await list.optionsFilter(filter).click();
            await page.getByRole("menuitem", { name: "Clear choices" }).click();
            await expect(page.getByRole("menu")).toHaveCount(0);
          }
          await list.rangeStart("Joined between").fill("01-01-2025");
          await list.rangeEnd("Joined between").fill("31-12-2026");
          await list.applyFilters.click();
          await expectAddress(page, `?name=${prefix}&joinedFrom=2025-01-01&joinedTo=2026-12-31`);
          await expect(list.status).toHaveText("Showing all 2 people. Sorted by Name, A to Z.");
          await expect.poll(() => list.titles()).toEqual([aarav!.displayName, bhavna!.displayName]);

          await list.chooseOptions("Status", ["Deactivated"]);
          await list.applyFilters.click();
          await expectAddress(
            page,
            `?name=${prefix}&status=deactivated&joinedFrom=2025-01-01&joinedTo=2026-12-31`,
          );
          await expect(list.noMatches).toContainText("No people match these filters.");
          await expect(list.status).toHaveText("No people to show.");
          await expect(list.table).toHaveCount(0);
          await expect(list.pageSize).toHaveCount(0);
          await expect(page).toHaveTitle(/People · ERP-AI-Pro/);
          await capture("people-no-matches");

          await list.noMatches.getByRole("link", { name: "Clear filters" }).click();
          await expectAddress(page, "");
          await expect(list.textFilter("Name")).toBeFocused();
          await expect(list.textFilter("Name")).toHaveValue("");
          await expect(list.rangeStart("Joined between")).toHaveValue("");
        } finally {
          await removeSeeded(api, created);
        }
      });

      test("hides and shows columns and keeps them in the address", async ({ page, api, capture }) => {
        test.slow();
        const people = new PeoplePage(page);
        const { list } = people;
        const created = nothingCreated();

        try {
          const seeded = await seedPeople(api, created);
          const [aarav] = seeded.people;
          const filtered = `?name=${seeded.prefix}`;
          await people.goto(filtered);
          await people.waitUntilInteractive();

          await list.columnsButton.click();
          await expect(page.getByRole("menu")).toMatchAriaSnapshot(`
            - menu:
              - group:
                - text: Show columns
                - menuitemcheckbox "Work email" [checked]
                - menuitemcheckbox "Employee code" [checked]
                - menuitemcheckbox "Department" [checked]
                - menuitemcheckbox "Designation" [checked]
                - menuitemcheckbox "Manager" [checked]
                - menuitemcheckbox "Joined" [checked]
                - menuitemcheckbox "Status" [checked]
                - menuitemcheckbox "Roles in the ERP" [checked]
                - menuitemcheckbox "Last signed in" [checked]
          `);
          await expect(page.locator("body"), "the page keeps its scrollbar").not.toHaveAttribute(
            "data-scroll-locked",
          );
          await capture("people-columns-menu");
          await page.keyboard.press("Escape");
          await expect(page.getByRole("menu")).toHaveCount(0);
          await expect(list.columnsButton).toBeFocused();

          await list.setColumnVisible("Work email", false);
          await list.setColumnVisible("Last signed in", false);
          await expectAddress(page, `${filtered}&hide=workEmail%2ClastSignedInAt`);
          const cards = await list.showsCards();
          const shownTerms = [
            "Employee code",
            "Department",
            "Designation",
            "Manager",
            "Joined",
            "Status",
            "Roles in the ERP",
          ];
          if (cards) await expect(people.row(aarav!.displayName).getByRole("term")).toHaveText(shownTerms);
          else await expect(list.table.getByRole("columnheader")).toHaveText(["Name", ...shownTerms]);

          await page.reload();
          await people.waitUntilInteractive();
          await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 people. Sorted by Name, A to Z.`);
          if (cards) await expect(people.row(aarav!.displayName).getByRole("term")).toHaveText(shownTerms);
          else await expect(list.columnHeader("Work email")).toHaveCount(0);

          await list.nextPage.click();
          await expectAddress(page, `${filtered}&page=2&hide=workEmail%2ClastSignedInAt`);
          await list.setColumnVisible("Work email", true);
          await list.setColumnVisible("Last signed in", true);
          await expectAddress(page, `${filtered}&page=2`);
        } finally {
          await removeSeeded(api, created);
        }
      });
    });
  }

  test("sorts by each column through the API and keeps the sort in the address", async ({ page, api }) => {
    test.slow();
    const people = new PeoplePage(page);
    const { list } = people;
    const created = nothingCreated();

    try {
      const seeded = await seedPeople(api, created);
      const [aarav, bhavna, chirag] = seeded.people;
      const filtered = `?name=${seeded.prefix}`;
      const byCodeFromAToZ = names([...seeded.people].reverse().slice(0, pageSize));
      await people.goto(filtered);
      await people.waitUntilInteractive();

      const first = async (count: number) => (await list.titles()).slice(0, count);
      // A person without a department, designation or manager sorts before everyone with one from A to Z, so the people
      // who have one lead the list only from Z to A; a sort by a column the seeded people share checks the address alone.
      const sorts: readonly {
        readonly header: string;
        readonly choice: string;
        readonly sort: string;
        readonly shows?: () => Promise<void>;
      }[] = [
        {
          header: "Employee code",
          choice: "Employee code, A to Z",
          sort: "employeeCode:asc",
          shows: () => expect.poll(() => list.titles()).toEqual(byCodeFromAToZ),
        },
        {
          header: "Work email",
          choice: "Work email, A to Z",
          sort: "workEmail:asc",
          shows: () => expect.poll(() => list.titles()).toEqual(byCodeFromAToZ),
        },
        {
          header: "Joined",
          choice: "Joined, latest first",
          sort: "dateOfJoining:desc",
          shows: () => expect.poll(() => first(3)).toEqual(names([aarav!, bhavna!, chirag!])),
        },
        {
          header: "Status",
          choice: "Status, A to Z",
          sort: "status:asc",
          shows: () => expect.poll(() => list.titles()).not.toContain(chirag!.displayName),
        },
        {
          header: "Status",
          choice: "Status, Z to A",
          sort: "status:desc",
          shows: () => expect.poll(() => first(1)).toEqual([chirag!.displayName]),
        },
        { header: "Designation", choice: "Designation, A to Z", sort: "designation:asc" },
        {
          header: "Designation",
          choice: "Designation, Z to A",
          sort: "designation:desc",
          shows: () => expect.poll(() => first(1)).toEqual([aarav!.displayName]),
        },
        { header: "Manager", choice: "Manager, A to Z", sort: "manager:asc" },
        {
          header: "Manager",
          choice: "Manager, Z to A",
          sort: "manager:desc",
          shows: () => expect.poll(() => first(1)).toEqual([bhavna!.displayName]),
        },
        { header: "Department", choice: "Department, A to Z", sort: "department:asc" },
        {
          header: "Department",
          choice: "Department, Z to A",
          sort: "department:desc",
          shows: () => expect.poll(async () => (await first(2)).sort()).toEqual(names([aarav!, bhavna!])),
        },
        { header: "Last signed in", choice: "Last signed in, latest first", sort: "lastSignedInAt:desc" },
      ];
      for (const { header, choice, sort, shows } of sorts) {
        await list.sortBy(header, choice);
        await expect(page).toHaveURL((url) => url.searchParams.get("sort") === sort);
        await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 people. Sorted by ${choice}.`);
        await shows?.();
      }

      await page.goBack();
      await expect(page).toHaveURL((url) => url.searchParams.get("sort") === "department:desc");
      await expect(list.status).toContainText("Sorted by Department, Z to A.");
      await page.goForward();
      await expect(page).toHaveURL((url) => url.searchParams.get("sort") === "lastSignedInAt:desc");

      await list.sortBy("Name", "Name, A to Z");
      await expectAddress(page, filtered);
      await expect(list.status).toHaveText(`Showing 1–${pageSize} of 11 people. Sorted by Name, A to Z.`);
      await expect.poll(() => list.titles()).toEqual(names(seeded.people.slice(0, pageSize)));
    } finally {
      await removeSeeded(api, created);
    }
  });

  test("pages with links and changes the page size, keeping the view in the address", async ({
    page,
    api,
  }) => {
    test.slow();
    const people = new PeoplePage(page);
    const { list } = people;
    const created = nothingCreated();

    try {
      const seeded = await seedPeople(api, created);
      const last = seeded.people.at(-1)!;
      const filtered = `?name=${seeded.prefix}`;
      await people.goto(filtered);
      await people.waitUntilInteractive();
      await expect(list.previousPage).toHaveAttribute("aria-disabled", "true");
      await expect(list.firstPage).toHaveAttribute("aria-disabled", "true");
      await expect(await list.rows()).toHaveCount(pageSize);

      await list.nextPage.click();
      await expectAddress(page, `${filtered}&page=2`);
      await expect(list.pageLabel).toHaveText("Page 2 of 2");
      await expect(list.status).toHaveText("Showing 11–11 of 11 people. Sorted by Name, A to Z.");
      await expect.poll(() => list.titles()).toEqual([last.displayName]);
      await expect(list.nextPage).toHaveAttribute("aria-disabled", "true");
      await expect(list.lastPage).toHaveAttribute("aria-disabled", "true");

      await list.previousPage.click();
      await expectAddress(page, filtered);
      await expect(list.pageLabel).toHaveText("Page 1 of 2");
      await list.lastPage.click();
      await expectAddress(page, `${filtered}&page=2`);
      await list.firstPage.click();
      await expectAddress(page, filtered);

      for (const size of [20, 50, 100]) {
        await list.choosePageSize(size);
        await expectAddress(page, `${filtered}&size=${size}`);
        await expect(list.status).toHaveText("Showing all 11 people. Sorted by Name, A to Z.");
        await expect(await list.rows()).toHaveCount(11);
        await expect(list.nextPage).toHaveCount(0);
      }
      await list.choosePageSize(pageSize);
      await expectAddress(page, filtered);
      await expect(list.pageLabel).toHaveText("Page 1 of 2");
    } finally {
      await removeSeeded(api, created);
    }
  });

  test.describe("from an address typed by hand", () => {
    test("brings an address it cannot show back to one it can", async ({ page, api }) => {
      test.slow();
      const people = new PeoplePage(page);
      const created = nothingCreated();

      try {
        const seeded = await seedPeople(api, created);
        const { prefix, department } = seeded;

        await people.goto("?page=abc&size=7&sort=roles%3Aasc&tab=open");
        await expectAddress(page, "");

        await people.goto(`?name=${prefix}&page=99`);
        await expectAddress(page, `?name=${prefix}&page=2`);
        await expect(people.list.pageLabel).toHaveText("Page 2 of 2");

        await people.goto(
          `?name=%20${prefix}%20&status=invited&department=not-an-id&department=${department.id.toUpperCase()}`,
        );
        await expectAddress(page, `?name=${prefix}&department=${department.id}`);
        await people.waitUntilInteractive();
        await expect(people.list.status).toHaveText("Showing all 2 people. Sorted by Name, A to Z.");
        await expect(people.list.optionsFilter("Department")).toHaveAccessibleName(
          `Department ${department.name}`,
        );
      } finally {
        await removeSeeded(api, created);
      }
    });
  });

  test("opens the registration and the roles and permissions pages from its header", async ({ page }) => {
    const people = new PeoplePage(page);
    await people.goto();

    await people.registerLink.click();
    await expect(page).toHaveURL((url) => url.pathname === `${peoplePath}/new`);
    await expect(page.getByRole("main").getByRole("heading", { level: 1 })).toBeVisible();
    await page.goBack();
    await expect(people.heading).toBeVisible();

    await people.permissionsLink.click();
    await expect(page).toHaveURL((url) => url.pathname === `${peoplePath}/permissions`);
    await expect(page.getByRole("main").getByRole("heading", { level: 1 })).toBeVisible();
  });
});

test.describe("people list for an employee", () => {
  forEachTheme("tells them they have no access to it", async ({ page, capture }) => {
    const people = new PeoplePage(page);
    await page.goto(peoplePath);

    await expect(
      page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
    ).toBeVisible();
    await expect(page).toHaveTitle(/No access · ERP-AI-Pro/);
    await expect(people.listCard).toHaveCount(0);
    await expect(people.registerLink).toHaveCount(0);
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - main:
        - heading "You do not have access to this page" [level=1]
        - paragraph: Ask an administrator for access.
        - link "Go to the home page"
    `);
    await capture("people-no-access");
  });

  test("the API refuses them the list of people", async ({ api }) => {
    const refused = await api.get(peopleApi);

    expect(refused.status()).toBe(403);
    expect(await refused.json()).toMatchObject({ code: "request.forbidden" });
  });
});
