import type { Browser, BrowserContext, Page, Route } from "@playwright/test";
import { randomUUID } from "node:crypto";

import { addDirectoryPerson } from "../../../fixtures/directory";
import {
  createDepartmentRecord,
  createDesignationRecord,
  ownPersonRecordId,
  peopleApi,
  personRecord,
  recordMoveRecord,
  registerPersonRecord,
  removeRecords,
  uniquePrefix,
} from "../../../fixtures/person-records";
import { expect, pressArrowUntilChecked, test, themes, type SignedInApi } from "../../../fixtures/test";
import {
  ownPhoto,
  ownPhotoWidth,
  ownProfileApi,
  profilePath,
  ProfilePage,
  type OwnProfile,
  type ThemeName,
} from "../../../pages/identity/users/profile.page";
import { AppShell } from "../../../pages/shared/layout/app-shell.page";

const darkClass = /(^|\s)dark(\s|$)/;

// The keeper saves through a Server Function, a POST to the page's own path carrying the next-action header.
function isThemeSave(route: Route, pathname: string): boolean {
  const request = route.request();
  return (
    new URL(request.url()).pathname === pathname &&
    request.method() === "POST" &&
    request.headers()["next-action"] !== undefined
  );
}

// Holds the first theme save until the test lets it go, and then cuts it, so that save never reaches the account.
async function holdFirstThemeSave(
  page: Page,
  pathname: string,
): Promise<{ readonly held: Promise<void>; readonly cut: () => void; readonly saves: () => number }> {
  let saves = 0;
  let isHeld: () => void = () => undefined;
  let cut: () => void = () => undefined;
  const held = new Promise<void>((resolve) => {
    isHeld = resolve;
  });
  const released = new Promise<void>((resolve) => {
    cut = resolve;
  });
  await page.route(
    (url) => url.pathname === pathname,
    async (route) => {
      if (!isThemeSave(route, pathname)) {
        await route.fallback();
        return;
      }
      saves += 1;
      if (saves > 1) {
        await route.fallback();
        return;
      }
      isHeld();
      await released;
      await route.abort("connectionreset").catch(() => undefined);
    },
  );
  return { held, cut, saves: () => saves };
}

async function ownProfile(api: SignedInApi): Promise<OwnProfile> {
  const response = await api.get(ownProfileApi);
  expect(response.status(), "the signed-in person's own record").toBe(200);
  return (await response.json()) as OwnProfile;
}

function savedTheme(api: SignedInApi): () => Promise<ThemeName> {
  return async () => (await ownProfile(api)).theme;
}

function initialsOf(name: string): string {
  const words = name.trim().split(/\s+/);
  return [words[0], words.length > 1 ? words.at(-1) : undefined]
    .map((word) => word?.charAt(0).toUpperCase() ?? "")
    .join("");
}

// A second browser of the same person: the context's cookies hold the session, and its storage holds no theme of its own.
async function sameSignInElsewhere(
  browser: Browser,
  context: BrowserContext,
  baseURL: string | undefined,
): Promise<BrowserContext> {
  const elsewhere = await browser.newContext({
    ...(baseURL === undefined ? {} : { baseURL }),
    ignoreHTTPSErrors: true,
    colorScheme: "light",
    locale: "en-IN",
    timezoneId: "Asia/Kolkata",
  });
  await elsewhere.addCookies(await context.cookies());
  return elsewhere;
}

test.describe("My profile", () => {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });

      test("shows an employee their own record and roles, and their initials while the directory holds no photo", async ({
        page,
        person,
        capture,
      }) => {
        const profile = new ProfilePage(page);
        const shell = new AppShell(page);
        const name = person?.name ?? "";

        await profile.goto();
        await shell.waitUntilInteractive();
        await profile.waitUntilInteractive();

        await expect(page).toHaveTitle("My profile · ERP-AI-Pro");
        await expect(profile.person.getByRole("heading", { level: 2 })).toHaveText(name);
        await expect(profile.person).toContainText(person?.userName ?? "");
        await expect(profile.details).toMatchAriaSnapshot(`
          - heading "Your details" [level=2]
          - text: The department, designation and manager are those of today. To change any of these, ask an administrator.
          - term: Employee code
          - definition: Not recorded
          - term: Phone number
          - definition: Not recorded
          - term: Date of joining
          - definition: Not recorded
          - term: Department
          - definition: Not recorded
          - term: Designation
          - definition: Not recorded
          - term: Manager
          - definition: Not recorded
        `);
        await expect(profile.roleList.getByRole("listitem")).toHaveText(["Employee"]);
        await expect(profile.rolesLink).toHaveCount(0);
        await expect(profile.initials).toHaveText(initialsOf(name));
        await expect(profile.photoImage).toHaveCount(0);
        await expect(shell.userMenuButton.locator("[data-slot='avatar-fallback']")).toHaveText(
          initialsOf(name),
        );
        await expect(shell.userMenuButton.locator("[data-slot='avatar-image']")).toHaveCount(0);
        await expect(profile.themeChooser).toMatchAriaSnapshot(`
          - radiogroup "Colour theme":
            - radio "System" [checked]
            - radio "Light"
            - radio "Dark"
        `);

        await capture("profile-employee");
      });

      test("shows the photo of the company directory on the page and on the account button", async ({
        page,
        person,
        request,
        capture,
      }) => {
        const name = person?.name ?? "";
        await addDirectoryPerson(request, {
          objectId: person?.id ?? "",
          displayName: name,
          userPrincipalName: person?.userName ?? "",
          photo: ownPhoto,
        });
        const profile = new ProfilePage(page);
        const shell = new AppShell(page);

        await profile.goto();
        await shell.waitUntilInteractive();
        await profile.waitUntilInteractive();

        await expect(profile.photoImage).toBeVisible();
        await expect(profile.photoImage).toHaveAccessibleName(`Photo of ${name}`);
        await expect
          .poll(() => profile.photoImage.evaluate((image) => (image as HTMLImageElement).naturalWidth))
          .toBe(ownPhotoWidth);
        await expect(profile.initials).toHaveCount(0);
        await expect(shell.userMenuButton.locator("[data-slot='avatar-image']")).toBeVisible();
        await expect(shell.userMenuButton.locator("[data-slot='avatar-fallback']")).toHaveCount(0);
        await expect(shell.userMenuButton).toHaveAccessibleName(`Signed in as ${name}`);

        await capture("profile-with-photo");
      });
    });
  }

  test("opens from My profile in the account menu", async ({ page }) => {
    const shell = new AppShell(page);
    await page.goto("/");
    await shell.waitUntilInteractive();

    await shell.openUserMenu();
    await expect(shell.profileItem).toHaveAttribute("href", profilePath);
    await shell.profileItem.click();

    await expect(page).toHaveURL((url) => url.pathname === profilePath);
    await expect(new ProfilePage(page).heading).toBeVisible();
    await expect(shell.userMenu).toBeHidden();
  });

  test("saves the theme chosen on My profile, which another browser of the same person then shows", async ({
    page,
    context,
    browser,
    api,
    baseURL,
  }) => {
    const profile = new ProfilePage(page);
    await profile.goto();
    await profile.waitUntilInteractive();
    await expect(profile.themeOption("system")).toBeChecked();

    await profile.themeOption("dark").click();

    await expect(profile.themeOption("dark")).toBeChecked();
    await expect(new AppShell(page).theme("dark")).toHaveAttribute("aria-checked", "true");
    await expect(page.locator("html")).toHaveClass(darkClass);
    await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("dark");

    const elsewhere = await sameSignInElsewhere(browser, context, baseURL);
    try {
      const otherPage = await elsewhere.newPage();
      const errors: string[] = [];
      otherPage.on("console", (message) => {
        if (message.type() === "error") errors.push(message.text());
      });
      const otherShell = new AppShell(otherPage);
      const otherProfile = new ProfilePage(otherPage);

      await otherPage.goto("/");
      await otherShell.waitUntilInteractive();
      await expect(otherPage.locator("html")).toHaveClass(darkClass);
      await expect(otherShell.theme("dark")).toHaveAttribute("aria-checked", "true");

      await otherProfile.goto();
      await otherProfile.waitUntilInteractive();

      await expect(otherPage.locator("html")).toHaveClass(darkClass);
      await expect(otherProfile.themeOption("dark")).toBeChecked();
      expect(errors, "console errors of the other browser").toEqual([]);
    } finally {
      await elsewhere.close();
    }
  });

  test("saves the theme chosen with the theme button at the top of any page", async ({ page, api }) => {
    const shell = new AppShell(page);
    await page.goto("/");
    await shell.waitUntilInteractive();

    await shell.theme("light").click();

    await expect(shell.theme("light")).toHaveAttribute("aria-checked", "true");
    await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("light");

    await shell.theme("system").click();

    await expect(shell.theme("system")).toHaveAttribute("aria-checked", "true");
    await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("system");

    const profile = new ProfilePage(page);
    await profile.goto();
    await profile.waitUntilInteractive();
    await expect(profile.themeOption("system")).toBeChecked();
  });

  test("moves and saves the theme from the keyboard", async ({ page, api }) => {
    const profile = new ProfilePage(page);
    await profile.goto();
    await profile.waitUntilInteractive();
    await profile.themeOption("system").focus();

    await pressArrowUntilChecked(page, "ArrowDown", profile.themeOption("light"));
    await expect(profile.themeOption("light")).toBeFocused();
    await expect(page.locator("html")).not.toHaveClass(darkClass);
    await pressArrowUntilChecked(page, "ArrowDown", profile.themeOption("dark"));
    await expect(profile.themeOption("dark")).toBeFocused();
    await expect(page.locator("html")).toHaveClass(darkClass);

    await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("dark");
    await expect(page.locator("html")).toHaveClass(darkClass);
  });

  test.describe("when a theme save does not reach the web server", () => {
    // Chromium logs the provoked reset; WebKit and Firefox log nothing for it.
    test.use({ expectedConsoleError: /^Failed to load resource: net::ERR_CONNECTION_RESET$/ });

    test("keeps the theme chosen just before the page reloads, while its save was still on its way", async ({
      page,
      api,
    }) => {
      const profile = new ProfilePage(page);
      await profile.goto();
      await profile.waitUntilInteractive();
      const save = await holdFirstThemeSave(page, profilePath);

      await profile.themeOption("dark").click();
      await save.held;
      expect(await savedTheme(api)(), "the theme the account holds while the save is on its way").toBe(
        "system",
      );
      await page.reload();
      save.cut();
      await profile.waitUntilInteractive();

      await expect(profile.themeOption("dark")).toBeChecked();
      await expect(page.locator("html")).toHaveClass(darkClass);
      await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("dark");
      expect(save.saves(), "the saves the pages sent").toBe(2);
    });

    test("keeps the theme on screen and saves the next choice", async ({ page, api }) => {
      const profile = new ProfilePage(page);
      await profile.goto();
      await profile.waitUntilInteractive();
      const save = await holdFirstThemeSave(page, profilePath);
      save.cut();

      await profile.themeOption("dark").click();
      await expect.poll(save.saves, { message: "the saves the page sent" }).toBe(1);

      await expect(page.locator("html")).toHaveClass(darkClass);
      await expect(profile.themeOption("dark")).toBeChecked();
      expect(await savedTheme(api)(), "the theme the account holds after the failed save").toBe("system");

      await profile.themeOption("light").click();

      await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("light");
      await expect(page.locator("html")).not.toHaveClass(darkClass);
      expect(save.saves(), "the saves the page sent").toBe(2);
    });

    test("saves the failed choice again from the next page this browser loads", async ({ page, api }) => {
      const profile = new ProfilePage(page);
      await profile.goto();
      await profile.waitUntilInteractive();
      const save = await holdFirstThemeSave(page, profilePath);
      save.cut();

      await profile.themeOption("dark").click();
      await expect.poll(save.saves, { message: "the saves the page sent" }).toBe(1);
      expect(await savedTheme(api)(), "the theme the account holds after the failed save").toBe("system");
      await page.goto("/");
      await new AppShell(page).waitUntilInteractive();

      await expect.poll(savedTheme(api), { message: "the theme the person's account holds" }).toBe("dark");
      await expect(page.locator("html")).toHaveClass(darkClass);
    });
  });

  test.describe("for an administrator", () => {
    test.use({ persona: "administrator" });

    for (const theme of themes) {
      test.describe(theme, () => {
        test.use({ theme });

        test("shows the roles their access gives them and the way to what each role may do", async ({
          page,
          capture,
        }) => {
          const profile = new ProfilePage(page);
          await profile.goto();
          await new AppShell(page).waitUntilInteractive();
          await profile.waitUntilInteractive();

          await expect(profile.roleList.getByRole("listitem")).toHaveText(["Employee", "Administrator"]);
          await expect(profile.rolesLink).toHaveAttribute("href", "/identity/users/permissions");

          await capture("profile-administrator");
        });
      });
    }

    test("opens what each role may do from the roles card", async ({ page }) => {
      const profile = new ProfilePage(page);
      await profile.goto();
      await new AppShell(page).waitUntilInteractive();

      await profile.rolesLink.click();

      await expect(page).toHaveURL((url) => url.pathname === "/identity/users/permissions");
      await expect(page.getByRole("heading", { name: "Roles and permissions", level: 1 })).toBeVisible();
    });

    // An administrator may record their own details, which no employee may, so their profile shows every recorded value.
    test("shows the details recorded for them and the department, designation and manager of today", async ({
      page,
      api,
      person,
    }) => {
      const prefix = uniquePrefix("profile");
      const ownId = await ownPersonRecordId(api, person);
      const employeeCode = `P-${randomUUID().slice(-12)}`;
      const own = await personRecord(api, ownId);
      const described = await api.put(`${peopleApi}/${ownId}`, {
        data: { employeeCode, phoneNumber: "98765 43210", dateOfJoining: "2025-04-01", version: own.version },
      });
      expect(described.status(), "the details of the administrator's own record").toBe(200);
      const department = await createDepartmentRecord(api, `${prefix} Treasury`);
      const designation = await createDesignationRecord(api, `${prefix} Treasurer`);
      const manager = await registerPersonRecord(api, {
        displayName: `${prefix} Kavya Menon`,
        workEmail: `${prefix}.kavya@dewiride.test`,
      });
      const moveId = await recordMoveRecord(api, ownId, {
        effectiveFrom: "2025-04-01",
        departmentId: department.id,
        designationId: designation.id,
        managerId: manager.id,
      });
      try {
        const profile = new ProfilePage(page);
        await profile.goto();
        await new AppShell(page).waitUntilInteractive();

        await expect(profile.details).toMatchAriaSnapshot(`
          - heading "Your details" [level=2]
          - text: The department, designation and manager are those of today. To change any of these, ask an administrator.
          - term: Employee code
          - definition: ${employeeCode}
          - term: Phone number
          - definition: "+919876543210"
          - term: Date of joining
          - definition: 01-04-2025
          - term: Department
          - definition: ${prefix} Treasury
          - term: Designation
          - definition: ${prefix} Treasurer
          - term: Manager
          - definition: ${prefix} Kavya Menon
        `);
      } finally {
        const withdrawn = await api.delete(`${peopleApi}/${ownId}/positions/${moveId}`);
        expect(withdrawn.status(), "the withdrawal of the administrator's move").toBe(204);
        await removeRecords(api, {
          people: [manager.id],
          departments: [department.id],
          designations: [designation.id],
        });
      }
    });
  });
});
