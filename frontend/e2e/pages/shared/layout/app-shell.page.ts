import { type Locator, type Page } from "@playwright/test";

export type ThemeOption = "light" | "dark" | "system";

export class AppShell {
  readonly banner: Locator;
  readonly primaryNavigation: Locator;
  readonly themeToggle: Locator;

  constructor(private readonly page: Page) {
    this.banner = page.getByRole("banner");
    this.primaryNavigation = page.getByRole("navigation", { name: "Primary" });
    this.themeToggle = page.getByTestId("theme-toggle");
  }

  get wordmarkLink(): Locator {
    return this.banner.getByRole("link", { name: /ERP-AI-Pro/ });
  }

  theme(value: ThemeOption): Locator {
    return this.page.getByTestId(`theme-${value}`);
  }
}
