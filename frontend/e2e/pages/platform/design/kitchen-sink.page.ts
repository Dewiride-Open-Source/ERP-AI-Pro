import { expect, type Locator, type Page } from "@playwright/test";

export const kitchenSinkPath = "/design/kitchen-sink";

export const kitchenSinkSections = [
  { id: "colours", group: "tokens", title: "Colours" },
  { id: "typography", group: "tokens", title: "Typography" },
  { id: "spacing", group: "tokens", title: "Spacing" },
  { id: "radius", group: "tokens", title: "Radius" },
  { id: "elevation", group: "tokens", title: "Elevation" },
  { id: "layers", group: "tokens", title: "Layers" },
  { id: "focus", group: "tokens", title: "Focus" },
  { id: "motion", group: "tokens", title: "Motion" },
  { id: "actions", group: "primitives", title: "Actions" },
  { id: "inputs", group: "primitives", title: "Inputs" },
  { id: "selection", group: "primitives", title: "Selection" },
  { id: "feedback", group: "primitives", title: "Feedback" },
  { id: "overlays", group: "primitives", title: "Overlays" },
  { id: "menus", group: "primitives", title: "Menus" },
  { id: "navigation", group: "primitives", title: "Navigation" },
  { id: "data-display", group: "primitives", title: "Data display" },
  { id: "composites", group: "primitives", title: "Composites" },
  { id: "excluded", group: "primitives", title: "Excluded primitives" },
] as const;

export type KitchenSinkSectionId = (typeof kitchenSinkSections)[number]["id"];

export type MotionDuration = "fast" | "normal" | "slow";

export class KitchenSinkPage {
  readonly root: Locator;
  readonly heading: Locator;
  readonly index: Locator;
  readonly swatches: Locator;
  readonly motionPreference: Locator;
  readonly motionReplay: Locator;
  readonly motionAnimated: Locator;
  readonly motionTransitionToggle: Locator;
  readonly motionTransitioned: Locator;
  readonly exampleForm: Locator;
  readonly exampleFormName: Locator;
  readonly exampleFormSubmit: Locator;
  readonly excludedPrimitives: Locator;
  readonly notifications: Locator;
  readonly invoiceActions: Locator;
  readonly invoiceActionsMenu: Locator;
  readonly contextMenuArea: Locator;
  readonly contextMenu: Locator;
  readonly menubar: Locator;
  readonly focusSample: Locator;

  constructor(private readonly page: Page) {
    this.root = page.getByTestId("kitchen-sink");
    this.heading = page.getByRole("heading", { name: "Design system", level: 1 });
    this.index = page.getByRole("navigation", { name: "Design system sections" });
    this.swatches = page.getByTestId(/^token-swatch-/);
    this.motionPreference = page.getByTestId("motion-demo-preference");
    this.motionReplay = page.getByRole("button", { name: "Replay entrance" });
    this.motionAnimated = page.getByTestId("motion-demo-animated");
    this.motionTransitionToggle = page.getByRole("switch", { name: "Slide the sample" });
    this.motionTransitioned = page.getByTestId("motion-demo-transitioned");
    this.exampleForm = page.getByTestId("example-form");
    this.exampleFormName = this.exampleForm.getByRole("textbox", { name: "Name" });
    this.exampleFormSubmit = this.exampleForm.getByRole("button", { name: "Save" });
    this.excludedPrimitives = page.getByRole("table", {
      name: "Registry items and recipes that are not installed in the design system.",
    });
    this.notifications = page.getByRole("region", { name: /^Notifications/ });
    this.invoiceActions = this.section("menus").getByRole("button", { name: "Invoice actions", exact: true });
    this.invoiceActionsMenu = this.menu("Invoice actions");
    this.contextMenuArea = page.getByTestId("menus-context-trigger");
    this.contextMenu = page.getByTestId("menus-context");
    this.menubar = page.getByRole("menubar");
    this.focusSample = this.section("focus").getByRole("link", { name: "Link with the focus ring" });
  }

  async goto(): Promise<void> {
    await this.page.goto(kitchenSinkPath);
    await expect(this.heading).toBeVisible();
    // A pointer move before React hydrates the page opens nothing; the motion demo sets data-reduced-motion only in the
    // browser, so the attribute marks the page as interactive.
    await expect(this.motionPreference).toHaveAttribute("data-reduced-motion", /^(true|false)$/);
  }

  section(id: KitchenSinkSectionId): Locator {
    return this.page.getByRole("region", { name: sectionTitle(id), exact: true });
  }

  sectionHeading(id: KitchenSinkSectionId): Locator {
    return this.section(id).getByRole("heading", { level: 2 });
  }

  menubarTrigger(name: string): Locator {
    return this.menubar.getByRole("menuitem", { name, exact: true });
  }

  menu(name: string): Locator {
    return this.page.getByRole("menu", { name, exact: true });
  }

  indexLink(id: KitchenSinkSectionId): Locator {
    return this.index.getByRole("link", { name: sectionTitle(id), exact: true });
  }

  specimen(title: string): Locator {
    return this.root.getByRole("heading", { name: title, exact: true, level: 3 }).locator("xpath=../..");
  }

  typeRole(role: string): Locator {
    return this.page.getByTestId(`type-role-${role}`);
  }

  motionDuration(name: MotionDuration): Locator {
    return this.page.getByTestId(`motion-demo-duration-${name}`);
  }

  toast(title: string): Locator {
    return this.notifications.getByRole("listitem").filter({ hasText: title });
  }

  async swatchTokenNames(): Promise<string[]> {
    const names = await this.swatches.evaluateAll((figures) =>
      figures.map((figure) => figure.dataset.token ?? ""),
    );
    return names.sort();
  }

  async transparentSwatches(): Promise<string[]> {
    return this.swatches.evaluateAll((figures) =>
      figures
        .filter((figure) => {
          const fill = figure.querySelector("[data-slot='token-swatch-fill']");
          const colour = fill === null ? "" : getComputedStyle(fill).backgroundColor;
          return colour === "" || colour === "transparent" || colour === "rgba(0, 0, 0, 0)";
        })
        .map((figure) => figure.dataset.token ?? ""),
    );
  }

  async colourTokenNames(): Promise<string[]> {
    const names = await this.page.evaluate(() => {
      const found = new Set<string>();
      const visit = (rules: CSSRuleList) => {
        for (const rule of rules) {
          if (rule instanceof CSSStyleRule && rule.selectorText === ":root") {
            for (const property of rule.style) {
              const value = rule.style.getPropertyValue(property).trim();
              const isColour = !value.startsWith("var(") && CSS.supports("color", value);
              if (property.startsWith("--") && !property.startsWith("--elevation-color") && isColour) {
                found.add(property.slice(2));
              }
            }
          }
          if (rule instanceof CSSGroupingRule) visit(rule.cssRules);
        }
      };
      for (const sheet of document.styleSheets) visit(sheet.cssRules);
      return [...found];
    });
    return names.sort();
  }
}

function sectionTitle(id: KitchenSinkSectionId): string {
  const section = kitchenSinkSections.find((candidate) => candidate.id === id);
  if (section === undefined) throw new Error(`Unknown kitchen sink section ${id}`);
  return section.title;
}
