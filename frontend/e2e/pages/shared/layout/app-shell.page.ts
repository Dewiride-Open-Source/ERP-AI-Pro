import { type Locator, type Page } from "@playwright/test";

export type ThemeOption = "light" | "dark" | "system";

export class AppShell {
  readonly banner: Locator;
  readonly primaryNavigation: Locator;
  readonly themeToggle: Locator;
  readonly skipLink: Locator;
  readonly main: Locator;
  readonly notifications: Locator;
  readonly pageTransition: Locator;

  constructor(private readonly page: Page) {
    this.banner = page.getByRole("banner");
    this.primaryNavigation = page.getByRole("navigation", { name: "Primary" });
    this.themeToggle = page.getByTestId("theme-toggle");
    this.skipLink = page.getByRole("link", { name: "Skip to main content" });
    this.main = page.getByRole("main");
    this.notifications = page.getByRole("region", { name: /^Notifications/ });
    this.pageTransition = page.locator("[data-slot='page-transition']");
  }

  get wordmarkLink(): Locator {
    return this.banner.getByRole("link", { name: /ERP-AI-Pro/ });
  }

  navigationLink(name: string): Locator {
    return this.primaryNavigation.getByRole("link", { name });
  }

  theme(value: ThemeOption): Locator {
    return this.page.getByTestId(`theme-${value}`);
  }

  toast(title: string): Locator {
    return this.notifications.getByRole("listitem").filter({ hasText: title });
  }
}
