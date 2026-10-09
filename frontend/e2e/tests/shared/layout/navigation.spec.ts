import type { Page } from "@playwright/test";

import { expect, forEachTheme, holdShortcut, tabOntoLink, test } from "../../../fixtures/test";
import { AttachmentsPage, attachmentsPath } from "../../../pages/platform/attachments/files.page";
import { formKitPath } from "../../../pages/platform/design/form-kit.page";
import { homePath, StartPage } from "../../../pages/platform/home/start.page";
import { SystemInfoPage } from "../../../pages/platform/system-info/info.page";
import {
  AppShell,
  showsBreadcrumbs,
  showsNavigationDrawer,
} from "../../../pages/shared/layout/app-shell.page";

const sidebarStateCookie = "sidebar_state";

const sidebarStateSeconds = 7 * 24 * 60 * 60;

const sidebarSnapshot = `
  - list:
    - listitem:
      - link "ERP-AI-Pro":
        - /url: /
  - navigation "Primary":
    - list:
      - listitem:
        - link "Home":
          - /url: /
    - text: Platform
    - list "Platform":
      - listitem:
        - link "System":
          - /url: /platform/system-info
      - listitem:
        - link "Attachments":
          - /url: /platform/attachments
`;

const drawerSnapshot = `
  - dialog "Navigation":
    - heading "Navigation" [level=2]
    - paragraph: The pages you can open.
    - list:
      - listitem:
        - link "ERP-AI-Pro":
          - /url: /
    - button "Close navigation"
    - navigation "Primary":
      - list:
        - listitem:
          - link "Home":
            - /url: /
      - text: Platform
      - list "Platform":
        - listitem:
          - link "System":
            - /url: /platform/system-info
        - listitem:
          - link "Attachments":
            - /url: /platform/attachments
`;

const wideBannerOnTheStartPage = `
  - banner:
    - button "Navigation"
    - navigation "Breadcrumb":
      - list:
        - listitem:
          - link "Home" [disabled]
    - button "Search"
    - radiogroup "Colour theme":
      - radio "Light"
      - radio "Dark"
      - radio "System" [checked]
    - button /^Signed in as .+/
`;

const narrowBanner = `
  - banner:
    - button "Navigation"
    - link "ERP-AI-Pro":
      - /url: /
    - button "Search"
    - radiogroup "Colour theme":
      - radio "Light"
      - radio "Dark"
      - radio "System" [checked]
    - button /^Signed in as .+/
`;

async function afterPendingTimers(page: Page): Promise<void> {
  await page.evaluate(() => new Promise<void>((resolve) => setTimeout(resolve, 0)));
}

async function expectSidebarState(
  page: Page,
  shell: AppShell,
  state: "expanded" | "collapsed",
): Promise<void> {
  const expanded = state === "expanded";
  await expect(shell.sidebar).toHaveAttribute("data-state", state);
  await expect(shell.sidebar).toHaveAttribute("data-collapsible", expanded ? "" : "icon");
  await expect(shell.menuButton).toHaveAttribute("aria-expanded", String(expanded));
  await expect
    .poll(
      async () =>
        (await page.context().cookies()).find((cookie) => cookie.name === sidebarStateCookie)?.value,
    )
    .toBe(String(expanded));
  const cookie = (await page.context().cookies()).find((candidate) => candidate.name === sidebarStateCookie);
  expect(cookie?.expires ?? 0, "the sidebar state cookie's expiry").toBeGreaterThan(
    Date.now() / 1000 + sidebarStateSeconds - 60,
  );
  expect(cookie?.expires ?? 0, "the sidebar state cookie's expiry").toBeLessThan(
    Date.now() / 1000 + sidebarStateSeconds + 60,
  );
}

test.describe("app shell navigation", () => {
  test.describe("on a wide screen", () => {
    test.skip(
      ({ viewport }) => showsNavigationDrawer(viewport),
      "the sidebar shows from the md breakpoint up; a narrower screen has the drawer",
    );

    forEachTheme(
      "the sidebar lists Home and every area's pages and marks the page that is open",
      async ({ page, capture }) => {
        const shell = new AppShell(page);
        await new AttachmentsPage(page).goto();
        await shell.waitUntilInteractive();

        await expect(shell.sidebar).toHaveAttribute("data-state", "expanded");
        await expect(shell.sidebar).toMatchAriaSnapshot(sidebarSnapshot);
        await expect(shell.navigationLink("Attachments")).toHaveAttribute("aria-current", "page");
        for (const other of ["Home", "System"]) {
          await expect(shell.navigationLink(other)).not.toHaveAttribute("aria-current");
        }
        await capture("sidebar-expanded", shell.sidebarPanel);

        await shell.navigationLink("System").click();
        await expect(page).toHaveURL((url) => url.pathname === "/platform/system-info");
        await expect(new SystemInfoPage(page).heading).toBeVisible();
        await expect(shell.navigationLink("System")).toHaveAttribute("aria-current", "page");
        await expect(shell.navigationLink("Attachments")).not.toHaveAttribute("aria-current");
        await shell.navigationLink("Home").click();
        await expect(page).toHaveURL((url) => url.pathname === homePath);
        await expect(new StartPage(page).heading).toBeVisible();
        await expect(shell.navigationLink("Home")).toHaveAttribute("aria-current", "page");
      },
    );

    forEachTheme(
      "the Navigation button and Ctrl+B collapse the sidebar to icons named by tooltips, and a reload keeps it so",
      async ({ page, capture }) => {
        const shell = new AppShell(page);
        const tooltip = page.getByRole("tooltip");
        await new SystemInfoPage(page).goto();
        await shell.waitUntilInteractive();
        await expect(shell.menuButton).toHaveAttribute("aria-keyshortcuts", "Control+B Meta+B");
        await expect(shell.menuButton).not.toHaveAttribute("aria-haspopup");
        await expect(shell.sidebar).toHaveAttribute("data-state", "expanded");
        await expect(shell.menuButton).toHaveAttribute("aria-expanded", "true");

        await shell.menuButton.click();
        await expectSidebarState(page, shell, "collapsed");
        await shell.navigationLink("Attachments").hover();
        await expect(tooltip).toHaveText("Attachments");
        await page.keyboard.press("Escape");
        await expect(tooltip).toHaveCount(0);
        await shell.navigationLink("System").focus();
        await expect(tooltip).toHaveText("System");
        await page.keyboard.press("Escape");
        await expect(tooltip).toHaveCount(0);
        await shell.main.hover();
        await capture("sidebar-collapsed", shell.sidebarPanel);

        await page.reload();
        await shell.waitUntilInteractive();
        await expect(shell.sidebar).toHaveAttribute("data-state", "collapsed");
        await expect(shell.menuButton).toHaveAttribute("aria-expanded", "false");

        await page.keyboard.press("ControlOrMeta+b");
        await expectSidebarState(page, shell, "expanded");
        await shell.navigationLink("Attachments").hover();
        await expect(shell.navigationLink("Attachments")).toHaveAttribute("data-state", /open$/);
        await expect(tooltip, "a tooltip beside a label that already shows").toHaveCount(0);
        await page.keyboard.press("ControlOrMeta+b");
        await expectSidebarState(page, shell, "collapsed");
        await shell.menuButton.click();
        await expectSidebarState(page, shell, "expanded");
      },
    );

    test("holding Ctrl+B toggles the sidebar once", async ({ page }) => {
      const shell = new AppShell(page);
      await new SystemInfoPage(page).goto();
      await shell.waitUntilInteractive();
      await expect(shell.sidebar).toHaveAttribute("data-state", "expanded");

      await holdShortcut(page, "b", { repeats: 3 });

      await expectSidebarState(page, shell, "collapsed");
    });

    test("the breadcrumb shows from 1024 px wide, where it fits beside the expanded sidebar", async ({
      isMobile,
      page,
    }) => {
      test.skip(isMobile, "a phone or a tablet keeps the width of its screen");
      const shell = new AppShell(page);
      const height = page.viewportSize()?.height ?? 0;
      await page.setViewportSize({ width: 1023, height });
      await new AttachmentsPage(page).goto();
      await shell.waitUntilInteractive();
      await expect(shell.breadcrumbs).toBeHidden();

      await page.setViewportSize({ width: 1024, height });
      await expect(shell.breadcrumbs).toBeVisible();
      await expect(shell.sidebar).toHaveAttribute("data-state", "expanded");
      const banner = await shell.banner.boundingBox();
      expect(banner?.height, "height of the top bar").toBe(56);
      const rows = await shell.breadcrumbs
        .getByRole("listitem")
        .evaluateAll(
          (items) => new Set(items.map((item) => Math.round(item.getBoundingClientRect().top))).size,
        );
      expect(rows, "rows the trail takes").toBe(1);
      const trail = await shell.breadcrumbs.boundingBox();
      expect((trail?.y ?? 0) + (trail?.height ?? 0), "bottom of the trail").toBeLessThanOrEqual(
        (banner?.y ?? 0) + (banner?.height ?? 0),
      );
    });

    test.describe("with room for the breadcrumb", () => {
      test.skip(({ viewport }) => !showsBreadcrumbs(viewport), "the breadcrumb shows from 1024 px wide");

      test("the breadcrumb follows a client navigation", async ({ page }) => {
        const shell = new AppShell(page);
        const home = shell.breadcrumbs.getByRole("link", { name: "Home" });
        await new StartPage(page).goto();
        await shell.waitUntilInteractive();

        await shell.navigationLink("Attachments").click();
        await expect(new AttachmentsPage(page).heading).toBeVisible();
        await expect(shell.breadcrumbs).toMatchAriaSnapshot(`
          - navigation "Breadcrumb":
            - list:
              - listitem:
                - link "Home":
                  - /url: /
              - listitem: Platform
              - listitem:
                - link "Attachments" [disabled]
        `);
        await expect(shell.breadcrumbs.getByRole("listitem")).toHaveCount(3);
        await expect(shell.breadcrumbs.getByRole("link", { name: "Attachments" })).toHaveAttribute(
          "aria-current",
          "page",
        );

        await home.click();
        await expect(page).toHaveURL((url) => url.pathname === homePath);
        await expect(new StartPage(page).heading).toBeVisible();
        await expect(shell.breadcrumbs.getByRole("listitem")).toHaveCount(1);
        await expect(home).toHaveAttribute("aria-current", "page");
      });

      test("the breadcrumb shows where each page sits in the navigation", async ({ page }) => {
        const shell = new AppShell(page);
        const home = shell.breadcrumbs.getByRole("link", { name: "Home" });

        await new StartPage(page).goto();
        await expect(shell.breadcrumbs).toMatchAriaSnapshot(`
          - navigation "Breadcrumb":
            - list:
              - listitem:
                - link "Home" [disabled]
        `);
        await expect(shell.breadcrumbs.getByRole("listitem")).toHaveCount(1);
        await expect(home).toHaveAttribute("aria-current", "page");

        await new AttachmentsPage(page).goto();
        await expect(shell.breadcrumbs).toMatchAriaSnapshot(`
          - navigation "Breadcrumb":
            - list:
              - listitem:
                - link "Home":
                  - /url: /
              - listitem: Platform
              - listitem:
                - link "Attachments" [disabled]
        `);
        await expect(shell.breadcrumbs.getByRole("listitem")).toHaveCount(3);
        await expect(shell.breadcrumbs.getByRole("link", { name: "Platform" })).toHaveCount(0);
        await expect(shell.breadcrumbs.getByRole("link", { name: "Attachments" })).toHaveAttribute(
          "aria-current",
          "page",
        );
        await expect(home).not.toHaveAttribute("aria-current");

        await page.goto(formKitPath);
        await expect(page.getByRole("heading", { name: "Form kit", level: 1 })).toBeVisible();
        await expect(shell.breadcrumbs).toMatchAriaSnapshot(`
          - navigation "Breadcrumb":
            - list:
              - listitem:
                - link "Home":
                  - /url: /
        `);
        await expect(shell.breadcrumbs.getByRole("listitem")).toHaveCount(1);
        await home.click();
        await expect(page).toHaveURL((url) => url.pathname === homePath);
        await expect(new StartPage(page).heading).toBeVisible();
      });
    });
  });

  test.describe("on a narrow screen", () => {
    test.skip(
      ({ viewport }) => !showsNavigationDrawer(viewport),
      "the drawer replaces the sidebar below the md breakpoint",
    );

    forEachTheme(
      "the Navigation button opens the drawer, whose links open their page and close it",
      async ({ page, capture }) => {
        const shell = new AppShell(page);
        await new SystemInfoPage(page).goto();
        await shell.waitUntilInteractive();

        await expect(page.locator("[data-slot='sidebar']")).toHaveCount(0);
        await expect(shell.primaryNavigation).toHaveCount(0);
        await expect(shell.breadcrumbs).toBeHidden();
        await expect(shell.banner).toMatchAriaSnapshot(narrowBanner);
        await expect(shell.menuButton).toHaveAttribute("aria-haspopup", "dialog");
        await expect(shell.menuButton).toHaveAttribute("aria-expanded", "false");

        await shell.menuButton.click();
        await expect(shell.drawer).toBeVisible();
        await expect(shell.menuButton).toHaveAttribute("aria-expanded", "true");
        await expect(shell.drawer).toHaveAccessibleDescription("The pages you can open.");
        await expect(shell.drawer).toMatchAriaSnapshot(drawerSnapshot);
        await expect(shell.navigationLink("System")).toHaveAttribute("aria-current", "page");
        await capture("drawer-open", shell.drawer);

        await shell.navigationLink("Attachments").click();
        await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
        await expect(shell.drawer).toBeHidden();
        await expect(new AttachmentsPage(page).heading).toBeVisible();
        await expect(shell.menuButton).toHaveAttribute("aria-expanded", "false");
        await expect(shell.menuButton).toBeFocused();
      },
    );

    test("Ctrl+B opens and closes the drawer and gives focus back to where it was", async ({ page }) => {
      const shell = new AppShell(page);
      const systemInfo = new SystemInfoPage(page);
      await systemInfo.goto();
      await shell.waitUntilInteractive();

      await page.keyboard.press("ControlOrMeta+b");
      await expect(shell.drawer).toBeVisible();
      await expect(shell.menuButton).toHaveAttribute("aria-expanded", "true");
      await page.keyboard.press("ControlOrMeta+b");
      await expect(shell.drawer).toBeHidden();
      await expect(shell.menuButton).toHaveAttribute("aria-expanded", "false");
      await expect(
        shell.menuButton,
        "the Navigation button, because the page itself had focus",
      ).toBeFocused();

      await systemInfo.refresh.focus();
      await page.keyboard.press("ControlOrMeta+b");
      await expect(shell.drawer).toBeVisible();
      await page.keyboard.press("ControlOrMeta+b");
      await expect(shell.drawer).toBeHidden();
      await expect(systemInfo.refresh).toBeFocused();
      await page.keyboard.press("ControlOrMeta+b");
      await expect(shell.drawer).toBeVisible();
      await page.keyboard.press("Escape");
      await expect(shell.drawer).toBeHidden();
      await expect(systemInfo.refresh).toBeFocused();
    });

    test("Escape closes the drawer and gives focus back to the Navigation button", async ({ page }) => {
      const shell = new AppShell(page);
      await new SystemInfoPage(page).goto();
      await shell.openNavigation();

      await page.keyboard.press("Escape");
      await expect(shell.drawer).toBeHidden();
      await expect(shell.menuButton).toHaveAttribute("aria-expanded", "false");
      await expect(shell.menuButton).toBeFocused();
    });

    test("Close navigation closes the drawer and gives focus back to the Navigation button", async ({
      page,
    }) => {
      const shell = new AppShell(page);
      await new SystemInfoPage(page).goto();
      await shell.openNavigation();

      await shell.closeNavigation.click();
      await expect(shell.drawer).toBeHidden();
      await expect(shell.menuButton).toHaveAttribute("aria-expanded", "false");
      await expect(shell.menuButton).toBeFocused();
    });

    test("the ERP-AI-Pro mark in the drawer opens the start page and closes the drawer", async ({ page }) => {
      const shell = new AppShell(page);
      await new SystemInfoPage(page).goto();
      await shell.openNavigation();

      await shell.drawer.getByRole("link", { name: "ERP-AI-Pro", exact: true }).click();
      await expect(page).toHaveURL((url) => url.pathname === homePath);
      await expect(new StartPage(page).heading).toBeVisible();
      await expect(shell.drawer).toBeHidden();
      await expect(shell.menuButton).toHaveAttribute("aria-expanded", "false");
      await expect(shell.menuButton).toBeFocused();
    });

    test("a page chosen in the drawer with the keyboard leaves focus on the Navigation button", async ({
      page,
    }) => {
      const shell = new AppShell(page);
      const systemInfo = new SystemInfoPage(page);
      await systemInfo.goto();
      await shell.waitUntilInteractive();
      await systemInfo.refresh.focus();
      await page.keyboard.press("ControlOrMeta+b");
      await expect(shell.drawer).toBeVisible();

      await tabOntoLink(page, shell.navigationLink("Attachments"));
      await page.keyboard.press("Enter");
      await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
      await expect(new AttachmentsPage(page).heading).toBeVisible();
      await expect(shell.drawer).toBeHidden();
      await expect(shell.menuButton).toBeFocused();
    });

    test("tapping beside the drawer closes it and gives focus back to the Navigation button", async ({
      hasTouch,
      page,
    }) => {
      const shell = new AppShell(page);
      await new SystemInfoPage(page).goto();
      await shell.openNavigation();

      const { width, height } = page.viewportSize() ?? { width: 0, height: 0 };
      const besideTheDrawer = { x: width - 16, y: height / 2 };
      expect(besideTheDrawer.x, "a point right of the 18rem drawer").toBeGreaterThan(300);
      if (hasTouch) await page.touchscreen.tap(besideTheDrawer.x, besideTheDrawer.y);
      else await page.mouse.click(besideTheDrawer.x, besideTheDrawer.y);
      await expect(shell.drawer).toBeHidden();
      await expect(shell.menuButton).toHaveAttribute("aria-expanded", "false");
      await expect(shell.menuButton).toBeFocused();
    });

    test("widening the screen closes the drawer, and narrowing it again leaves it closed", async ({
      page,
    }) => {
      const shell = new AppShell(page);
      const narrow = page.viewportSize() ?? { width: 0, height: 0 };
      await new SystemInfoPage(page).goto();
      await shell.openNavigation();

      await page.setViewportSize({ width: 1024, height: narrow.height });
      await expect(shell.sidebar).toBeVisible();
      await expect(shell.drawer).toHaveCount(0);
      await page.setViewportSize(narrow);
      await expect(shell.sidebar).toHaveCount(0);
      await expect(shell.menuButton).toHaveAttribute("aria-expanded", "false");
      await expect(shell.drawer).toHaveCount(0);
    });

    test("Ctrl+K closes the drawer and opens the page search, and a page chosen there opens with the drawer closed", async ({
      page,
    }) => {
      const shell = new AppShell(page);
      await new SystemInfoPage(page).goto();
      await shell.waitUntilInteractive();
      await page.keyboard.press("ControlOrMeta+b");
      await expect(shell.drawer).toBeVisible();

      await page.keyboard.press("ControlOrMeta+k");
      await expect(shell.drawer).toBeHidden();
      await expect(shell.palette).toBeVisible();
      await expect(shell.paletteSearch).toBeFocused();
      await page.keyboard.type("att");
      await expect(shell.paletteResults.getByRole("option")).toHaveCount(1);
      await page.keyboard.press("Enter");

      await expect(page).toHaveURL((url) => url.pathname === attachmentsPath);
      await expect(new AttachmentsPage(page).heading).toBeVisible();
      await expect(shell.palettePanel).toHaveCount(0);
      await expect(shell.drawerPanel).toHaveCount(0);
      await expect(shell.searchButton).toBeFocused();
    });

    test("a drawer link opened in a new tab leaves the drawer open", async ({ context, page }) => {
      const shell = new AppShell(page);
      await new SystemInfoPage(page).goto();
      await shell.openNavigation();
      const address = page.url();
      const opened: Page[] = [];
      context.on("page", (popup) => opened.push(popup));

      await shell.navigationLink("Attachments").click({ modifiers: ["ControlOrMeta"] });
      await expect(shell.drawer).toBeVisible();
      await expect(shell.menuButton).toHaveAttribute("aria-expanded", "true");
      await expect(page).toHaveURL(address);
      await expect
        .poll(
          () =>
            page.evaluate(() => {
              const focused = document.activeElement;
              return (
                focused !== null && focused.closest("[data-sidebar='sidebar'][data-mobile='true']") !== null
              );
            }),
          { message: "focus inside the drawer" },
        )
        .toBe(true);
      test.info().annotations.push({
        type: "popup",
        description: `the browser opened ${opened.length} new page(s) for the modified click`,
      });
      for (const popup of opened) await popup.close();
    });

    test("Ctrl+B pressed as the page search closes opens the drawer, and focus stays inside it", async ({
      page,
    }) => {
      const shell = new AppShell(page);
      await new SystemInfoPage(page).goto();
      await shell.waitUntilInteractive();
      await page.keyboard.press("ControlOrMeta+k");
      await expect(shell.paletteSearch).toBeFocused();
      await page.evaluate(() => {
        const landings: boolean[] = [];
        Object.assign(window, { focusLandings: landings });
        document.addEventListener(
          "focusin",
          (event) =>
            landings.push(
              event.target instanceof Element &&
                event.target.closest("[data-sidebar='sidebar'][data-mobile='true']") !== null,
            ),
          true,
        );
      });

      await page.keyboard.press("Escape");
      await page.keyboard.press("ControlOrMeta+b");
      await expect(shell.palettePanel).toHaveCount(0);
      await expect(shell.drawer).toBeVisible();
      await afterPendingTimers(page);

      const landings = await page.evaluate(
        () => (window as unknown as { focusLandings: boolean[] }).focusLandings,
      );
      const opened = landings.indexOf(true);
      expect(opened, "focus moving into the drawer").toBeGreaterThanOrEqual(0);
      expect(landings.slice(opened), "where focus landed once the drawer was open").not.toContain(false);
      await expect(shell.drawer.locator(":focus")).toHaveCount(1);
    });
  });

  test("the ERP-AI-Pro mark leads to the start page", async ({ page }) => {
    const shell = new AppShell(page);
    await new SystemInfoPage(page).goto();
    await shell.waitUntilInteractive();

    await expect(shell.wordmarkLink).toHaveCount(1);
    await expect(
      (shell.usesDrawer ? shell.banner : shell.sidebar).getByRole("link", {
        name: "ERP-AI-Pro",
        exact: true,
      }),
    ).toBeVisible();
    await shell.wordmarkLink.click();
    await expect(page).toHaveURL((url) => url.pathname === homePath);
    await expect(new StartPage(page).heading).toBeVisible();
  });

  forEachTheme("the user menu names who is signed in and offers Sign out", async ({ page, capture }) => {
    const shell = new AppShell(page);
    await new StartPage(page).goto();
    await shell.waitUntilInteractive();

    await expect(shell.banner).toMatchAriaSnapshot(
      shell.usesDrawer ? narrowBanner : wideBannerOnTheStartPage,
    );
    await expect(shell.userMenuButton).toHaveAttribute("aria-haspopup", "menu");
    await expect(shell.userMenuButton).toHaveAttribute("aria-expanded", "false");
    await capture("header-signed-in", shell.banner);

    await shell.openUserMenu();
    await expect(shell.userMenuButton).toHaveAttribute("aria-expanded", "true");
    await expect(shell.userMenu).toMatchAriaSnapshot(`
      - menu "Account":
        - text: /.+/
        - separator
        - menuitem "Sign out"
    `);
    await expect(shell.signOutItem).toBeEnabled();
    await capture("user-menu-open", shell.userMenu);
    await page.keyboard.press("Escape");
    await expect(shell.userMenu).toBeHidden();
    await expect(shell.userMenuButton).toBeFocused();
  });

  test("the user menu opens from the keyboard with the person's name and user name", async ({
    page,
    person,
  }) => {
    const shell = new AppShell(page);
    const name = person?.name ?? "";
    const initials = name
      .split(/\s+/)
      .filter((word, index, words) => index === 0 || index === words.length - 1)
      .map((word) => word.charAt(0).toUpperCase())
      .join("");
    await new StartPage(page).goto();
    await shell.waitUntilInteractive();

    await expect(shell.userMenuButton).toHaveAccessibleName(`Signed in as ${name}`);
    await expect(shell.userMenuButton.locator("[data-slot='avatar-fallback']")).toHaveText(initials);
    const nameWidth = (await shell.userMenuButton.getByText(name).boundingBox())?.width ?? 0;
    if (shell.usesDrawer) {
      expect(
        nameWidth,
        "width of the name, which a narrow screen leaves to screen readers",
      ).toBeLessThanOrEqual(1);
    } else expect(nameWidth, "width of the name beside the initials").toBeGreaterThan(1);

    for (const key of ["Enter", "ArrowDown"]) {
      await shell.userMenuButton.focus();
      await page.keyboard.press(key);
      await expect(shell.userMenu, `the menu opened with ${key}`).toBeVisible();
      await expect(shell.signOutItem).toBeFocused();
      await expect(shell.userMenu).toContainText(name);
      await expect(shell.userMenu).toContainText(person?.userName ?? "");
      await page.keyboard.press("Escape");
      await expect(shell.userMenu).toBeHidden();
      await expect(shell.userMenuButton).toBeFocused();
    }
  });

  test.describe("not found", () => {
    test.use({ expectedConsoleError: /the server responded with a status of 404/ });

    forEachTheme("unknown routes render the not-found page", async ({ page, capture }) => {
      const response = await page.goto("/does-not-exist");
      expect(response?.status()).toBe(404);
      await expect(page.getByRole("heading", { name: "This page does not exist" })).toBeVisible();
      await capture("not-found");
      await page.getByRole("link", { name: "Go to the home page" }).click();
      await expect(page).toHaveURL((url) => url.pathname === "/");
    });
  });
});
