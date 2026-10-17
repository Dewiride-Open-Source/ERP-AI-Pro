import { expect, forEachTheme, tabOntoLink, test, themes, type SignedInApi } from "../../../fixtures/test";
import {
  PermissionsPage,
  permissionsApi,
  permissionsPath,
  type CataloguedPermission,
  type PermissionCatalog,
} from "../../../pages/identity/users/permissions.page";
import { AppShell, showsBreadcrumbs } from "../../../pages/shared/layout/app-shell.page";

const peoplePath = "/identity/users";

const builtInRoles = ["employee", "finance", "hr", "manager", "administrator"];

async function readCatalog(api: SignedInApi): Promise<PermissionCatalog> {
  const response = await api.get(permissionsApi);
  expect(response.status(), "the permission catalogue").toBe(200);
  return (await response.json()) as PermissionCatalog;
}

function declared(catalog: PermissionCatalog, name: string): CataloguedPermission {
  const permission = catalog.permissions.find((candidate) => candidate.name === name);
  if (permission === undefined) throw new Error(`The catalogue declares no permission ${name}.`);
  return permission;
}

function holding(catalog: PermissionCatalog, permission: CataloguedPermission): string[] {
  return catalog.roles.map((role) => (permission.heldBy.includes(role) ? "Held" : "Not held"));
}

// A row is named by its header, the description and then the permission's name, followed by every cell.
function rowSnapshot(permission: CataloguedPermission, cells: readonly string[]): string[] {
  const header = `${permission.description} ${permission.name}`;
  return [
    `- row ${JSON.stringify([header, ...cells].join(" "))}:`,
    `  - rowheader ${JSON.stringify(header)}`,
    ...cells.map((cell) => `  - cell ${JSON.stringify(cell)}`),
  ];
}

test.describe("roles and permissions page", () => {
  test.use({ persona: "administrator" });

  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows what each built-in role may do, as the modules declare it", async ({
        page,
        api,
        capture,
        isMobile,
      }) => {
        const catalog = await readCatalog(api);
        expect(catalog.roles, "the built-in roles").toEqual(builtInRoles);
        const permissions = new PermissionsPage(page);

        await permissions.goto();

        await expect(page).toHaveTitle(/Roles and permissions · ERP-AI-Pro/);
        await expect(permissions.unavailable).toHaveCount(0);
        await expect(permissions.empty).toHaveCount(0);
        await expect(permissions.matrix).toMatchAriaSnapshot(`
          - table "What each role may do":
            - rowgroup:
              - row "Permission Employee Finance HR Manager Administrator":
                - columnheader "Permission"
                - columnheader "Employee"
                - columnheader "Finance"
                - columnheader "HR"
                - columnheader "Manager"
                - columnheader "Administrator"
        `);
        await expect(permissions.rows).toHaveCount(catalog.permissions.length);
        expect(await permissions.names(), "the permissions in ordinal order").toEqual(
          catalog.permissions.map((permission) => permission.name),
        );
        for (const permission of catalog.permissions) {
          const row = permissions.row(permission.name);
          await expect(row.getByRole("rowheader"), permission.name).toContainText(permission.description);
          await expect(row.getByRole("cell"), permission.name).toHaveText(holding(catalog, permission));
        }
        await expect(permissions.matrix).toMatchAriaSnapshot(
          [
            "- table:",
            "  - rowgroup:",
            ...[
              ...rowSnapshot(declared(catalog, "identity.users.departments.read"), [
                "Held",
                "Not held",
                "Not held",
                "Not held",
                "Held",
              ]),
              ...rowSnapshot(declared(catalog, "platform.audit.changes.export"), [
                "Not held",
                "Not held",
                "Not held",
                "Not held",
                "Held",
              ]),
            ].map((line) => `    ${line}`),
          ].join("\n"),
        );
        if (isMobile) {
          await expect(permissions.scroller).toHaveAttribute("tabindex", "0");
          await expect(page.getByRole("region", { name: "What each role may do" })).toBeVisible();
        } else {
          await expect(permissions.scroller).not.toHaveAttribute("tabindex");
          await expect(permissions.scroller).not.toHaveAttribute("role");
        }

        await capture("permissions");
      });
    });
  }

  test("opens the people list from the link in its header", async ({ page }) => {
    const permissions = new PermissionsPage(page);
    await permissions.goto();
    await new AppShell(page).waitUntilInteractive();

    await expect(permissions.peopleLink).toHaveAccessibleName("All people");
    await expect(permissions.peopleLink).toHaveAttribute("href", peoplePath);
    await tabOntoLink(page, permissions.peopleLink);
    await page.keyboard.press("Enter");

    await expect(page).toHaveURL((url) => url.pathname === peoplePath);
    await expect(page.getByRole("heading", { name: "People", level: 1 })).toBeVisible();
  });

  test("keeps People marked in the navigation, where the page has no entry of its own", async ({ page }) => {
    const shell = new AppShell(page);
    await new PermissionsPage(page).goto();
    await shell.waitUntilInteractive();

    await shell.openNavigation();
    await expect(shell.navigationLink("People")).toHaveAttribute("aria-current", "true");
    await expect(shell.primaryNavigation.getByRole("link", { name: "Roles and permissions" })).toHaveCount(0);
    await shell.closeDrawer();
  });

  test.describe("with room for the breadcrumb", () => {
    test.skip(({ viewport }) => !showsBreadcrumbs(viewport), "the breadcrumb shows from 1024 px wide");

    test("ends the breadcrumb at People, which leads back to the list", async ({ page }) => {
      const shell = new AppShell(page);
      await new PermissionsPage(page).goto();
      await shell.waitUntilInteractive();

      await expect(shell.breadcrumbs).toMatchAriaSnapshot(`
        - navigation "Breadcrumb":
          - list:
            - listitem:
              - link "Home":
                - /url: /
            - listitem: Organisation
            - listitem:
              - link "People":
                - /url: ${peoplePath}
      `);
      const people = shell.breadcrumbs.getByRole("link", { name: "People" });
      await expect(people).not.toHaveAttribute("aria-current");

      await people.click();
      await expect(page).toHaveURL((url) => url.pathname === peoplePath);
      await expect(people).toHaveAttribute("aria-current", "page");
    });
  });
});

test.describe("roles and permissions page for an employee", () => {
  forEachTheme("tells them they have no access to it", async ({ page, capture }) => {
    await page.goto(permissionsPath);

    await expect(
      page.getByRole("heading", { name: "You do not have access to this page", level: 1 }),
    ).toBeVisible();
    await expect(page).toHaveTitle(/No access · ERP-AI-Pro/);
    await expect(page.getByTestId("permissions-card")).toHaveCount(0);
    await expect(page.getByRole("main")).toMatchAriaSnapshot(`
      - main:
        - heading "You do not have access to this page" [level=1]
        - paragraph: Ask an administrator for access.
        - link "Go to the home page"
    `);
    await capture("permissions-no-access");
  });

  test("the API refuses them the catalogue", async ({ api }) => {
    const refused = await api.get(permissionsApi);

    expect(refused.status()).toBe(403);
    expect(await refused.json()).toMatchObject({ code: "request.forbidden" });
  });
});
