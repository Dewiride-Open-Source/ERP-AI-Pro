import { randomUUID } from "node:crypto";

import type { Page } from "@playwright/test";

import { departmentIdsNamed } from "../../../../fixtures/organisation-records";
import {
  createDepartmentRecord,
  departmentsApi,
  recordMoveRecord,
  registerPersonRecord,
  removeRecords,
  uniquePrefix,
  type NamedRecord,
} from "../../../../fixtures/person-records";
import { signIn } from "../../../../fixtures/sign-in";
import { expect, test, themes, type SignedInApi } from "../../../../fixtures/test";
import { DepartmentsPage, departmentsPath } from "../../../../pages/identity/users/departments.page";
import { peoplePath } from "../../../../pages/identity/users/people.page";
import { AppShell, showsBreadcrumbs } from "../../../../pages/shared/layout/app-shell.page";

type Tree = {
  readonly prefix: string;
  readonly sales: NamedRecord;
  readonly inside: NamedRecord;
  readonly field: NamedRecord;
  readonly north: NamedRecord;
};

const quoted = JSON.stringify;

// The tree is the test's own, under a prefix no other worker uses, so its assertions meet no other department.
async function seedTree(api: SignedInApi, feature: string): Promise<Tree> {
  const prefix = uniquePrefix(feature);
  const sales = await createDepartmentRecord(api, `${prefix} Sales`);
  const inside = await createDepartmentRecord(api, `${prefix} Inside sales`, sales.id);
  const field = await createDepartmentRecord(api, `${prefix} Field sales`, sales.id);
  const north = await createDepartmentRecord(api, `${prefix} North`, inside.id);
  return { prefix, sales, inside, field, north };
}

async function removeDepartmentsNamed(api: SignedInApi, prefix: string): Promise<void> {
  await removeRecords(api, { departments: await departmentIdsNamed(api, prefix) });
}

function rowSnapshot(name: string, id: string, { under }: { readonly under?: number } = {}): string {
  return [
    ...(under === undefined ? [] : [`- button ${quoted(`Show the departments under ${name}`)}`]),
    `- paragraph: ${quoted(name)}`,
    ...(under === undefined
      ? []
      : [`- paragraph: ${under === 1 ? "1 department under it" : `${under} departments under it`}`]),
    `- button ${quoted(`Add a department under ${name}`)}`,
    `- button ${quoted(`Rename or move ${name}`)}`,
    `- button ${quoted(`Delete ${name}`)}`,
    `- link ${quoted(`Change history of ${name}`)}:`,
    `  - /url: /platform/audit?record=${id}`,
  ].join("\n");
}

function indented(snapshot: string, spaces: number): string {
  return snapshot
    .split("\n")
    .map((line) => `${" ".repeat(spaces)}${line}`)
    .join("\n");
}

// A dialog that starts to close turns its data-state to "closed" at once but stays on the page through its exit animation,
// so whether it closed is judged by its count once it has gone.
async function closes(page: Page, departments: DepartmentsPage): Promise<void> {
  await expect(departments.dialog).toHaveCount(0);
  await expect(page.getByRole("dialog")).toHaveCount(0);
}

test.describe("departments page", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows the departments as a tree, each folded until it is opened, with a link to its changes", async ({
        page,
        api,
        capture,
      }) => {
        const tree = await seedTree(api, "departments-tree");
        const { sales, inside, field, north } = tree;
        try {
          const departments = new DepartmentsPage(page);
          await departments.goto();
          await departments.waitUntilInteractive();

          await expect(page).toHaveTitle(/Departments · ERP-AI-Pro/);
          await expect(departments.toggle(sales.name)).toHaveAttribute("aria-expanded", "false");
          await expect(departments.childList(sales.name)).toBeHidden();
          await expect(departments.row(sales.name)).toMatchAriaSnapshot(
            rowSnapshot(sales.name, sales.id, { under: 2 }),
          );
          await capture("departments-folded", departments.item(sales.name));

          await departments.toggle(sales.name).click();
          await expect(departments.toggle(sales.name)).toHaveAttribute("aria-expanded", "true");
          await expect(departments.childList(sales.name)).toBeVisible();
          await expect(departments.childList(sales.name)).toMatchAriaSnapshot(
            [
              `- list ${quoted(`Departments under ${sales.name}`)}:`,
              "  - listitem:",
              indented(rowSnapshot(field.name, field.id), 4),
              "  - listitem:",
              indented(rowSnapshot(inside.name, inside.id, { under: 1 }), 4),
            ].join("\n"),
          );
          await expect(departments.childList(inside.name)).toBeHidden();
          await capture("departments-open", departments.item(sales.name));

          await departments.toggle(inside.name).focus();
          await page.keyboard.press("Enter");
          await expect(departments.toggle(inside.name)).toHaveAttribute("aria-expanded", "true");
          await expect(departments.row(north.name)).toBeVisible();
          await page.keyboard.press("Space");
          await expect(departments.toggle(inside.name)).toHaveAttribute("aria-expanded", "false");
          await expect(departments.row(north.name)).toBeHidden();

          await departments.collapseAll.focus();
          await page.keyboard.press("Enter");
          await expect(departments.childList(sales.name)).toBeHidden();
          await expect(departments.collapseAll).toBeFocused();
          await departments.expandAll.focus();
          await page.keyboard.press("Enter");
          await expect(departments.row(north.name)).toBeVisible();
          await expect(departments.toggle(sales.name)).toHaveAttribute("aria-expanded", "true");
          await expect(departments.toggle(inside.name)).toHaveAttribute("aria-expanded", "true");
          await expect(departments.expandAll).toBeFocused();

          await departments.history(sales.name).click();
          await expect(page).toHaveURL(
            (url) => url.pathname === "/platform/audit" && url.searchParams.get("record") === sales.id,
          );
          await expect(page.getByRole("heading", { name: "Audit trail", level: 1 })).toBeVisible();
        } finally {
          await removeDepartmentsNamed(api, tree.prefix);
        }
      });

      test("adds, renames and moves departments through the dialog, refusing a blank name and a name taken in the same place", async ({
        page,
        api,
        capture,
      }) => {
        test.slow();
        const prefix = uniquePrefix("departments-edit");
        const sales = await createDepartmentRecord(api, `${prefix} Sales`);
        const inside = await createDepartmentRecord(api, `${prefix} Inside sales`, sales.id);
        const marketing = `${prefix} Marketing`;
        const channel = `${prefix} Channel sales`;
        const brand = `${prefix} Brand marketing`;
        try {
          const departments = new DepartmentsPage(page);
          const shell = new AppShell(page);
          await departments.goto();
          await departments.waitUntilInteractive();

          await departments.addButton.click();
          await expect(departments.dialog).toMatchAriaSnapshot(`
            - dialog "Add a department":
              - heading "Add a department" [level=2]
              - paragraph: Name the department and choose the department it sits under, or leave that empty to add it at the top.
              - group:
                - text: Name
                - textbox "Name"
                - paragraph: Up to 100 characters. No other department in the same place may have it.
              - group:
                - text: Sits under
                - group:
                  - combobox "Sits under"
                  - group:
                    - button "Show options Sits under"
                  - status
                - paragraph: Leave empty to put it at the top.
              - button "Cancel"
              - button "Add the department"
              - button "Close"
          `);
          await capture("departments-add-dialog", departments.dialog);

          await departments.save.click();
          await expect(departments.name).toBeFocused();
          await expect(departments.name).toHaveAttribute("aria-invalid", "true");
          await expect(departments.dialog.getByText("Enter the department's name.")).toBeVisible();
          await capture("departments-add-refused-in-the-browser", departments.dialog);

          await departments.cancel.click();
          await closes(page, departments);
          await expect(departments.addButton).toBeFocused();
          await departments.addButton.click();
          await page.keyboard.press("Escape");
          await closes(page, departments);
          await expect(departments.addButton).toBeFocused();
          await departments.addButton.click();
          await departments.close.click();
          await closes(page, departments);
          await expect(departments.addButton).toBeFocused();

          await departments.addButton.click();
          await departments.name.fill(marketing);
          await departments.save.click();
          await closes(page, departments);
          await expect(shell.toast(`Added ${marketing}.`)).toBeVisible();
          await expect(departments.addButton).toBeFocused();
          await expect(departments.row(marketing)).toBeVisible();

          await departments.addUnder(sales.name).click();
          await expect(departments.dialog).toBeVisible();
          await expect(departments.parent).toHaveValue(sales.name);
          await departments.name.fill(inside.name);
          await departments.save.click();
          await expect(departments.summary).toContainText(
            "Another department in the same place already has this name; choose another name or another place.",
          );
          await expect(departments.summary).toBeFocused();
          await capture("departments-add-refused", departments.dialog);

          await departments.name.fill(channel);
          await departments.save.click();
          await closes(page, departments);
          await expect(shell.toast(`Added ${channel}.`)).toBeVisible();
          await expect(departments.addUnder(sales.name)).toBeFocused();
          await expect(departments.toggle(sales.name)).toHaveAttribute("aria-expanded", "true");
          await expect(departments.row(channel)).toBeVisible();

          await departments.edit(marketing).click();
          await expect(departments.dialog.getByRole("heading", { level: 2 })).toHaveText(
            `Rename or move ${marketing}`,
          );
          await expect(departments.name).toHaveValue(marketing);
          await departments.parent.fill(prefix);
          const parents = page.getByRole("listbox").getByRole("option");
          await expect(parents, "every department but the one moved").toHaveText([
            sales.name,
            `${sales.name} › ${channel}`,
            `${sales.name} › ${inside.name}`,
          ]);
          await departments.parent.press("ArrowDown");
          await expect(parents.first()).toHaveAttribute("aria-selected", "true");
          await departments.parent.press("Enter");
          await expect(page.getByRole("listbox")).toBeHidden();
          await expect(departments.parent).toHaveValue(sales.name);
          await departments.name.fill(brand);
          await capture("departments-edit-dialog", departments.dialog);
          await departments.save.click();
          await closes(page, departments);
          await expect(shell.toast(`Saved ${brand}.`)).toBeVisible();
          await expect(departments.edit(brand)).toBeFocused();
          await expect(departments.childList(sales.name)).toContainText(brand);
          await expect(departments.row(marketing)).toHaveCount(0);
          await capture("departments-moved", departments.item(sales.name));
        } finally {
          await removeDepartmentsNamed(api, prefix);
        }
      });

      test("deletes a department after asking, and explains one the API keeps because it holds others", async ({
        page,
        api,
        capture,
      }) => {
        const tree = await seedTree(api, "departments-delete");
        const { sales, field } = tree;
        try {
          const departments = new DepartmentsPage(page);
          const shell = new AppShell(page);
          await departments.goto();
          await departments.waitUntilInteractive();

          await departments.delete(sales.name).click();
          const salesDeletion = departments.confirmation(`Delete ${sales.name}?`);
          await expect(salesDeletion).toMatchAriaSnapshot(`
            - alertdialog ${quoted(`Delete ${sales.name}?`)}:
              - heading ${quoted(`Delete ${sales.name}?`)} [level=2]
              - paragraph: ${quoted(
                `${sales.name} is removed from the departments. The moves that named it keep its name, so people's history still shows it.`,
              )}
              - button "Keep it"
              - button "Delete"
          `);
          await capture("departments-deletion", salesDeletion);
          await salesDeletion.getByRole("button", { name: "Keep it" }).click();
          await expect(salesDeletion).toHaveCount(0);
          await expect(departments.delete(sales.name)).toBeFocused();

          await departments.delete(sales.name).click();
          await salesDeletion.getByRole("button", { name: "Delete", exact: true }).click();
          await expect(departments.refusal(sales.name)).toHaveRole("alert");
          await expect(departments.refusal(sales.name)).toContainText(
            "This department still has departments under it, or someone is in it today or from a later date. Move them elsewhere first.",
          );
          await expect(departments.delete(sales.name)).toBeFocused();
          await capture("departments-deletion-refused", departments.row(sales.name));
          expect((await api.get(`${departmentsApi}/${sales.id}`)).status(), "the department kept").toBe(200);

          await departments.toggle(sales.name).click();
          await departments.delete(field.name).click();
          await departments
            .confirmation(`Delete ${field.name}?`)
            .getByRole("button", { name: "Delete", exact: true })
            .click();
          await expect(shell.toast(`Deleted ${field.name}.`)).toBeVisible();
          await expect(departments.row(field.name)).toHaveCount(0);
          await expect(departments.listHeading).toBeFocused();
          await expect(departments.row(sales.name)).toContainText("1 department under it");
          expect((await api.get(`${departmentsApi}/${field.id}`)).status(), "the deleted department").toBe(
            404,
          );
        } finally {
          await removeDepartmentsNamed(api, tree.prefix);
        }
      });
    });
  }

  test("explains a department the API keeps because someone is in it", async ({ page, api }) => {
    const prefix = uniquePrefix("departments-in-use");
    const department = await createDepartmentRecord(api, `${prefix} Accounts`);
    const person = await registerPersonRecord(api, {
      displayName: `${prefix} Person`,
      workEmail: `${prefix}-person@example.com`,
    });
    try {
      await recordMoveRecord(api, person.id, { effectiveFrom: "2026-04-01", departmentId: department.id });
      const departments = new DepartmentsPage(page);
      await departments.goto();
      await departments.waitUntilInteractive();

      await departments.delete(department.name).click();
      await departments
        .confirmation(`Delete ${department.name}?`)
        .getByRole("button", { name: "Delete", exact: true })
        .click();

      await expect(departments.refusal(department.name)).toContainText(
        "This department still has departments under it, or someone is in it today or from a later date. Move them elsewhere first.",
      );
      await expect(departments.row(department.name)).toBeVisible();
    } finally {
      await removeRecords(api, { people: [person.id], departments: [department.id] });
    }
  });

  test("keeps Departments marked in the navigation and ends the breadcrumb there, leaving People unmarked", async ({
    page,
  }) => {
    const shell = new AppShell(page);
    await new DepartmentsPage(page).goto();
    await shell.waitUntilInteractive();

    await shell.openNavigation();
    await expect(shell.navigationLink("Departments")).toHaveAttribute("aria-current", "page");
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
              - link "Departments" [disabled]
      `);
      await expect(shell.breadcrumbs.getByRole("link", { name: "People" })).toHaveCount(0);
    }
    await page.goto(peoplePath);
    await shell.openNavigation();
    await expect(shell.navigationLink("People")).toHaveAttribute("aria-current", "page");
    await expect(shell.navigationLink("Departments")).not.toHaveAttribute("aria-current");
    await shell.closeDrawer();
  });
});

test.describe("departments page for an employee", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows the tree with no way to change it, and the API refuses a change", async ({
        context,
        page,
        api,
        capture,
      }) => {
        await signIn(context.request, "administrator");
        const tree = await seedTree(api, "departments-employee");
        const { sales, inside } = tree;
        try {
          await signIn(context.request, "accountant");
          const departments = new DepartmentsPage(page);
          await departments.goto();
          await departments.waitUntilInteractive();

          await expect(page).toHaveTitle(/Departments · ERP-AI-Pro/);
          await expect(departments.addButton).toHaveCount(0);
          await expect(departments.row(sales.name)).toMatchAriaSnapshot(`
            - button ${quoted(`Show the departments under ${sales.name}`)}
            - paragraph: ${quoted(sales.name)}
            - paragraph: 2 departments under it
          `);
          await expect(departments.row(sales.name).getByRole("button")).toHaveCount(1);
          await expect(departments.row(sales.name).getByRole("link")).toHaveCount(0);
          await departments.toggle(sales.name).click();
          await expect(departments.row(inside.name)).toBeVisible();
          await capture("departments-employee", departments.item(sales.name));

          const refused = await api.post(departmentsApi, {
            headers: { "Idempotency-Key": randomUUID() },
            data: { name: `${tree.prefix} Refused` },
          });
          expect(refused.status()).toBe(403);
          expect(await refused.json()).toMatchObject({ code: "request.forbidden" });
        } finally {
          // The open page renews its session once it has loaded, and a sign-in through the same browser context while that
          // renewal is under way leaves the page sending one person's request token with the next person's cookie.
          await page.goto("about:blank");
          await signIn(context.request, "administrator");
          await removeDepartmentsNamed(api, tree.prefix);
        }
      });
    });
  }

  test("is a page the employee may open from the navigation", async ({ page }) => {
    const shell = new AppShell(page);
    await page.goto("/");
    await shell.waitUntilInteractive();
    await shell.openNavigation();
    await shell.navigationLink("Departments").click();

    await expect(page).toHaveURL((url) => url.pathname === departmentsPath);
    await expect(new DepartmentsPage(page).heading).toBeVisible();
  });
});
