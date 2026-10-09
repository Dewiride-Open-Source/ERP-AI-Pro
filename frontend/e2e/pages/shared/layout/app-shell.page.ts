import { expect, type Locator, type Page, type ViewportSize } from "@playwright/test";

export type ThemeOption = "light" | "dark" | "system";

const sidebarMinWidth = 768;

const breadcrumbsMinWidth = 1024;

export function showsNavigationDrawer(viewport: ViewportSize | null): boolean {
  return (viewport?.width ?? 0) < sidebarMinWidth;
}

export function showsBreadcrumbs(viewport: ViewportSize | null): boolean {
  return (viewport?.width ?? 0) >= breadcrumbsMinWidth;
}

export class AppShell {
  readonly banner: Locator;
  readonly skipLink: Locator;
  readonly main: Locator;
  readonly notifications: Locator;
  readonly menuButton: Locator;
  readonly sidebar: Locator;
  readonly sidebarPanel: Locator;
  readonly drawer: Locator;
  readonly drawerPanel: Locator;
  readonly closeNavigation: Locator;
  readonly primaryNavigation: Locator;
  readonly breadcrumbs: Locator;
  readonly searchButton: Locator;
  readonly palette: Locator;
  readonly palettePanel: Locator;
  readonly paletteSearch: Locator;
  readonly paletteClose: Locator;
  readonly paletteResults: Locator;
  readonly paletteStatus: Locator;
  readonly paletteEmpty: Locator;
  readonly themeToggle: Locator;
  readonly account: Locator;
  readonly userMenuButton: Locator;
  readonly userMenu: Locator;
  readonly signOutItem: Locator;

  constructor(private readonly page: Page) {
    this.banner = page.getByRole("banner");
    this.skipLink = page.getByRole("link", { name: "Skip to main content" });
    this.main = page.getByRole("main");
    this.notifications = page.getByRole("region", { name: /^Notifications/ });
    this.menuButton = page.getByRole("button", { name: "Navigation", exact: true, includeHidden: true });
    this.sidebar = page.locator("[data-slot='sidebar-wrapper'] > [data-slot='sidebar']");
    this.sidebarPanel = this.sidebar.locator("[data-slot='sidebar-container']");
    this.drawer = page.getByRole("dialog", { name: "Navigation" });
    this.drawerPanel = page.locator("[data-sidebar='sidebar'][data-mobile='true']");
    this.closeNavigation = this.drawer.getByRole("button", { name: "Close navigation" });
    this.primaryNavigation = page.getByRole("navigation", { name: "Primary" });
    this.breadcrumbs = page.getByRole("navigation", { name: "Breadcrumb" });
    this.searchButton = this.banner.getByRole("button", { name: "Search", exact: true });
    this.palette = page.getByRole("dialog", { name: "Go to a page" });
    this.palettePanel = page.locator("[data-slot='dialog-content']:has(input[aria-label='Search pages'])");
    this.paletteSearch = this.palette.getByRole("combobox", { name: "Search pages" });
    this.paletteClose = this.palette.getByRole("button", { name: "Close Esc" });
    this.paletteResults = this.palette.getByRole("listbox", { name: "Pages" });
    this.paletteStatus = this.palette.getByRole("status");
    this.paletteEmpty = this.palette
      .getByRole("paragraph")
      .filter({ hasText: "No page matches your search." });
    this.themeToggle = page.getByTestId("theme-toggle");
    this.account = page.getByTestId("account-area");
    this.userMenuButton = this.account.getByRole("button", { includeHidden: true });
    this.userMenu = page.getByRole("menu", { name: "Account" });
    this.signOutItem = this.userMenu.getByRole("menuitem", { name: "Sign out" });
  }

  get usesDrawer(): boolean {
    return showsNavigationDrawer(this.page.viewportSize());
  }

  get wordmarkLink(): Locator {
    return this.page.getByRole("link", { name: "ERP-AI-Pro", exact: true });
  }

  navigationLink(name: string): Locator {
    return this.primaryNavigation.getByRole("link", { name, exact: true });
  }

  paletteOption(name: string): Locator {
    return this.paletteResults.getByRole("option", { name, exact: true });
  }

  theme(value: ThemeOption): Locator {
    return this.page.getByTestId(`theme-${value}`);
  }

  toast(title: string): Locator {
    return this.notifications.getByRole("listitem").filter({ hasText: title });
  }

  async waitUntilInteractive(): Promise<void> {
    await expect(this.menuButton).toBeEnabled();
    await expect(this.searchButton).toBeEnabled();
    await expect(this.userMenuButton).toBeEnabled();
  }

  async openNavigation(): Promise<void> {
    if (!this.usesDrawer) return;
    await expect(this.menuButton).toBeEnabled();
    await this.menuButton.click();
    await expect(this.drawer).toBeVisible();
  }

  async closeDrawer(): Promise<void> {
    if (!this.usesDrawer) return;
    await this.page.keyboard.press("Escape");
    await expect(this.drawer).toBeHidden();
  }

  async openUserMenu(): Promise<void> {
    await expect(this.userMenuButton).toBeEnabled();
    await this.userMenuButton.click();
    await expect(this.userMenu).toBeVisible();
  }

  async signOut(): Promise<void> {
    await this.openUserMenu();
    await this.signOutItem.click();
  }
}
