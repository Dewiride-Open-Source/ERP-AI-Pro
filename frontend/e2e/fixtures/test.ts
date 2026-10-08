import AxeBuilder from "@axe-core/playwright";
import {
  test as base,
  expect,
  type APIResponse,
  type BrowserContext,
  type Locator,
  type Page,
  type TestInfo,
} from "@playwright/test";
import { mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import {
  requestToken,
  requestTokenHeader,
  signIn,
  type EntraSession,
  type Persona,
  type SignIn,
  type SignedInPerson,
} from "./sign-in";

export type Theme = "light" | "dark";

export const themes: readonly Theme[] = ["light", "dark"];

type ApiRequestOptions = NonNullable<Parameters<BrowserContext["request"]["fetch"]>[1]>;

export type SignedInApi = {
  readonly get: (path: string, options?: ApiRequestOptions) => Promise<APIResponse>;
  readonly post: (path: string, options?: ApiRequestOptions) => Promise<APIResponse>;
  readonly delete: (path: string, options?: ApiRequestOptions) => Promise<APIResponse>;
};

type Fixtures = {
  theme: Theme;
  persona: Persona | null;
  person: SignedInPerson | undefined;
  entraSession: EntraSession | undefined;
  api: SignedInApi;
  expectedConsoleError: RegExp | undefined;
  consoleErrors: string[];
  capture: (name: string, target?: Locator) => Promise<void>;
};

const signIns = new WeakMap<BrowserContext, SignIn>();

const screenshotsRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "screenshots");

const captureHiddenAttribute = "data-e2e-capture-hidden";

const scanTargetAttribute = "data-e2e-scan-target";

const accessibilityTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

const blockingImpacts: ReadonlySet<string> = new Set(["serious", "critical"]);

// Radix marks everything outside an open modal layer with aria-hidden and data-aria-hidden and keeps focus inside the
// layer, so that content cannot be reached while the layer is open and is scanned in the captures taken without it. An open
// Radix navigation menu renders an aria-hidden, focusable span beside its trigger that hands focus to the open content.
const excludedFromScans = [
  "[data-aria-hidden='true']",
  "[data-slot='navigation-menu-item'] > span[aria-hidden='true']",
];

// Every page outside the sign-in page needs a session, so a test signs a new person of a persona in unless it sets persona
// to null to visit as nobody.
export const test = base.extend<Fixtures>({
  theme: ["light", { option: true }],
  persona: ["accountant", { option: true }],
  expectedConsoleError: [undefined, { option: true }],

  context: async ({ context, persona }, use) => {
    if (persona !== null) signIns.set(context, await signIn(context.request, persona));
    await use(context);
  },

  person: async ({ context }, use) => {
    await use(signIns.get(context)?.person);
  },

  entraSession: async ({ context }, use) => {
    await use(signIns.get(context)?.entraSession);
  },

  api: async ({ context }, use) => {
    await use(signedInApi(context));
  },

  consoleErrors: [
    async ({ page, expectedConsoleError }, use) => {
      const errors: string[] = [];
      const isExpected = (text: string) => expectedConsoleError?.test(text) ?? false;
      page.on("console", (message) => {
        if (message.type() === "error" && !isExpected(message.text())) errors.push(message.text());
      });
      page.on("pageerror", (error) => {
        if (!isExpected(error.message)) errors.push(error.message);
      });
      await use(errors);
      expect(errors, "console errors").toEqual([]);
    },
    { auto: true },
  ],

  page: async ({ page, theme }, use) => {
    await page.emulateMedia({ colorScheme: theme });
    await use(page);
  },

  capture: async ({ page, theme }, use, testInfo) => {
    await use(async (name: string, target?: Locator) => {
      await waitForRest(page, name);
      const file = screenshotPath(testInfo, name, theme);
      const heldMotion = await page.addStyleTag({ content: heldMotionStyle });
      try {
        await expectNoSeriousAccessibilityViolations(page, name, target);
        mkdirSync(dirname(file), { recursive: true });
        if (target === undefined) {
          const pageHeight = await page.evaluate(() => document.documentElement.scrollHeight);
          await expectOneCapture(page, pageHeight, name);
          await page.screenshot({ path: file, fullPage: true, animations: "disabled" });
        } else {
          await captureElement(target, file, name);
        }
      } finally {
        await heldMotion.evaluate((style) => {
          if (style instanceof Element) style.remove();
        });
      }
      await testInfo.attach(`${name}--${theme}`, { path: file, contentType: "image/png" });
    });
  },
});

// The browser's own session reaches the API through the web origin, and every change it sends carries the request token
// the sign-in issued, as the pages do.
export function signedInApi(context: BrowserContext): SignedInApi {
  const change = (method: string) => async (path: string, options?: ApiRequestOptions) =>
    context.request.fetch(path, {
      ...options,
      method,
      headers: { ...options?.headers, [requestTokenHeader]: await requestToken(context) },
    });
  return {
    get: (path, options) => context.request.get(path, options),
    post: change("POST"),
    delete: change("DELETE"),
  };
}

export type ThemedFixtures = {
  page: Page;
  capture: Fixtures["capture"];
  theme: Theme;
  consoleErrors: Fixtures["consoleErrors"];
};

export function forEachTheme(
  title: string,
  body: (fixtures: ThemedFixtures, isMobile: boolean) => Promise<void>,
): void {
  for (const theme of themes) {
    test.describe(theme, () => {
      test.use({ theme });
      test(title, async ({ page, capture, consoleErrors, isMobile }) => {
        await body({ page, capture, theme, consoleErrors }, isMobile);
      });
    });
  }
}

// Radix radio groups move focus in a timeout after the arrow's keydown and check the newly focused radio only while an arrow key
// is still held, so the key is released once that radio is checked, as a person's key press is, never straight after keydown.
export async function pressArrowUntilChecked(page: Page, key: string, radio: Locator): Promise<void> {
  await page.keyboard.down(key);
  try {
    await expect(radio).toBeChecked();
  } finally {
    await page.keyboard.up(key);
  }
}

export async function tabOntoLink(page: Page, link: Locator): Promise<void> {
  await link.focus();
  await page.keyboard.press("Shift+Tab");
  await expect(link).not.toBeFocused();
  // WebKit leaves links out of the Tab order unless Safari's "Press Tab to highlight each item" is on, so there the link is
  // focused from script right after a key press, which :focus-visible treats as keyboard focus.
  if (page.context().browser()?.browserType().name() === "webkit") await link.focus();
  else await page.keyboard.press("Tab");
  await expect(link).toBeFocused();
}

// A capture shows the page as a person meets it once it works, so it waits until no component still shows its server-rendered
// state (data-hydrating), no AnimatedList item is changing its height from JavaScript (data-animating), no finite CSS animation
// or transition runs, and the document has then stayed unchanged for a few frames. On a loaded runner a component hydrates
// seconds after the load event and a popover starts its entrance only once it has been positioned, so none of these alone is
// enough. Animations that repeat for ever never settle and are left running.
const restFrames = 3;

const restTimeout = 10_000;

async function waitForRest(page: Page, name: string): Promise<void> {
  const unsettled = await page.evaluate(
    ({ frames, timeout }) =>
      new Promise<string | undefined>((resolve) => {
        let quietFrames = 0;
        let checkedFrames = 0;
        let lastChange = "";
        const observer = new MutationObserver((records) => {
          quietFrames = 0;
          const record = records.at(-1);
          const target = record?.target;
          lastChange =
            target instanceof Element
              ? `${record?.type ?? ""} ${record?.attributeName ?? ""} on ${target.outerHTML.slice(0, 120)}`
              : `${record?.type ?? ""} on ${target?.nodeName ?? "a node"}`;
        });
        observer.observe(document, { subtree: true, childList: true, attributes: true, characterData: true });
        const animationName = (animation: Animation): string => {
          if (animation instanceof CSSAnimation) return animation.animationName;
          if (animation instanceof CSSTransition) return animation.transitionProperty;
          return "a script animation";
        };
        const busy = (): string | undefined => {
          const hydrating = document.querySelectorAll("[data-hydrating]").length;
          if (hydrating > 0) return `${hydrating} element(s) still render their server state`;
          const animating = document.querySelectorAll("[data-animating]").length;
          if (animating > 0) return `${animating} list item(s) still change their height`;
          const running = document
            .getAnimations()
            .filter(
              (animation) =>
                animation.playState === "running" &&
                animation.effect?.getComputedTiming().iterations !== Infinity,
            )
            .map(animationName);
          return running.length > 0 ? `still running: ${running.join(", ")}` : undefined;
        };
        const deadline = performance.now() + timeout;
        const check = () => {
          const reason = busy();
          checkedFrames += 1;
          quietFrames = reason === undefined ? quietFrames + 1 : 0;
          if (quietFrames < frames && performance.now() < deadline) {
            requestAnimationFrame(check);
            return;
          }
          observer.disconnect();
          resolve(
            quietFrames >= frames
              ? undefined
              : `${reason ?? `the document kept changing, last by ${lastChange}`} (${checkedFrames} frames in ${timeout} ms)`,
          );
        };
        requestAnimationFrame(check);
      }),
    { frames: restFrames, timeout: restTimeout },
  );
  expect(unsettled, `${name} came to rest before its capture`).toBeUndefined();
}

// Colour contrast is measured on what is painted, so while the scan and the screenshot run every CSS animation and transition
// is held at its end state: a change that starts meanwhile, such as a toast leaving, is scanned and shown at rest rather than at
// a colour it only passes through, and an animation that repeats for ever shows its base style.
const heldMotionStyle = `*, *::before, *::after {
  animation-delay: 0s !important;
  animation-duration: 0s !important;
  transition-delay: 0s !important;
  transition-duration: 0s !important;
}`;

// Every captured screen is a key screen, so each one is scanned, in the theme it is captured in.
async function expectNoSeriousAccessibilityViolations(
  page: Page,
  name: string,
  target?: Locator,
): Promise<void> {
  const scan = new AxeBuilder({ page }).withTags(accessibilityTags);
  for (const selector of excludedFromScans) scan.exclude(selector);
  if (target !== undefined) {
    await target.evaluate((element, attribute) => element.setAttribute(attribute, ""), scanTargetAttribute);
    scan.include(`[${scanTargetAttribute}]`);
  }
  let violations: Awaited<ReturnType<AxeBuilder["analyze"]>>["violations"];
  try {
    ({ violations } = await scan.analyze());
  } finally {
    if (target !== undefined) {
      await target.evaluate((element, attribute) => element.removeAttribute(attribute), scanTargetAttribute);
    }
  }
  const blocking = violations
    .filter((violation) => blockingImpacts.has(violation.impact ?? ""))
    .map(
      (violation) =>
        `${violation.id} (${violation.impact ?? "unknown"}): ${violation.help}; ${violation.nodes
          .map((node) => `${node.target.join(" ")} (${node.failureSummary ?? "no summary"})`)
          .join(", ")}`,
    );
  expect(blocking, `serious or critical accessibility violations on ${name}`).toEqual([]);
}

// Chromium paints at most 16,384 device pixels of a capture's height and leaves the rest blank, and WebKit refuses a capture
// taller than 32,767, so a capture that would be cut fails here instead of being kept as incomplete evidence.
const maxCaptureDevicePixels = 16_384;

async function expectOneCapture(page: Page, cssHeight: number, name: string): Promise<void> {
  const devicePixelRatio = await page.evaluate(() => window.devicePixelRatio);
  expect(Math.ceil(cssHeight * devicePixelRatio), `height of ${name} in device pixels`).toBeLessThanOrEqual(
    maxCaptureDevicePixels,
  );
}

// An element taller than the viewport is captured from a scrolled page, where a sticky element outside it (the app header)
// would be painted across the middle of the image; hiding those for the capture keeps the image to the element alone.
async function captureElement(target: Locator, file: string, name: string): Promise<void> {
  const bounds = await target.boundingBox();
  expect(bounds, `bounding box of ${name}`).not.toBeNull();
  await expectOneCapture(target.page(), bounds?.height ?? 0, name);
  await target.evaluate((element, attribute) => {
    for (const candidate of element.ownerDocument.body.querySelectorAll("*")) {
      if (candidate.contains(element) || element.contains(candidate)) continue;
      if (getComputedStyle(candidate).position === "sticky") candidate.setAttribute(attribute, "");
    }
  }, captureHiddenAttribute);
  try {
    await target.screenshot({
      path: file,
      animations: "disabled",
      style: `[${captureHiddenAttribute}] { visibility: hidden !important; }`,
    });
  } finally {
    await target.page().evaluate((attribute) => {
      for (const hidden of document.querySelectorAll(`[${attribute}]`)) hidden.removeAttribute(attribute);
    }, captureHiddenAttribute);
  }
}

function screenshotPath(testInfo: TestInfo, name: string, theme: Theme): string {
  const spec = (testInfo.titlePath[0] ?? "unknown").replaceAll("\\", "/").replace(/\.spec\.ts$/, "");
  // --repeat-each runs the copies of a test side by side, and two of them writing one file fail with EBUSY on Windows.
  const repeat = testInfo.repeatEachIndex > 0 ? `--repeat${testInfo.repeatEachIndex}` : "";
  return join(screenshotsRoot, spec, `${slug(name)}--${testInfo.project.name}--${theme}${repeat}.png`);
}

function slug(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+/, "")
    .replace(/-+$/, "");
}

export { expect };
