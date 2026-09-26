import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

import {
  composite,
  contrastRatio,
  parseOklch,
  toSrgb,
  type OklchColour,
  type SrgbColour,
} from "../lib/oklch.ts";

interface Theme {
  readonly name: "light" | "dark";
  readonly tokens: ReadonlyMap<string, OklchColour>;
  readonly tintAlpha: number;
  readonly hoverTintAlpha: number;
}

interface Backdrop {
  readonly name: string;
  readonly colour: SrgbColour;
}

interface ContrastCheck {
  readonly label: string;
  readonly foreground: SrgbColour;
  readonly background: SrgbColour;
  readonly minimum: number;
}

const textMinimum = 4.5;
const nonTextMinimum = 3;

const expectedForegroundPairs = [
  "primary",
  "secondary",
  "muted",
  "accent",
  "destructive",
  "success",
  "warning",
  "info",
  "card",
  "popover",
  "sidebar",
  "sidebar-primary",
  "sidebar-accent",
] as const;

const bodyTexts = ["foreground", "muted-foreground"] as const;
const bodySurfaces = ["background", "card", "muted"] as const;
const intents = ["primary", "destructive", "success", "warning", "info"] as const;
const intentSurfaces = ["background", "card"] as const;
const inputSurfaces = ["background", "card"] as const;

const stylesheet = readFileSync(new URL("./theme.css", import.meta.url), "utf8").replace(
  /\/\*[\s\S]*?\*\//g,
  "",
);
const lightTokens = oklchCustomProperties(topLevelRuleBodies(stylesheet, ":root"));
const darkTokens = new Map([
  ...lightTokens,
  ...oklchCustomProperties(topLevelRuleBodies(stylesheet, ".dark")),
]);

// The generated primitives tint a status surface with bg-<intent>/10 (dark:bg-<intent>/20) and deepen the destructive
// button to hover:bg-destructive/20 (dark:hover:bg-destructive/30); the default button hovers at bg-primary/80.
const themes: readonly Theme[] = [
  { name: "light", tokens: lightTokens, tintAlpha: 0.1, hoverTintAlpha: 0.2 },
  { name: "dark", tokens: darkTokens, tintAlpha: 0.2, hoverTintAlpha: 0.3 },
];

const primaryHoverAlpha = 0.8;

const footerMutedAlpha = 0.5;

function topLevelRuleBodies(css: string, selector: string): string[] {
  const bodies: string[] = [];
  let depth = 0;
  let preludeStart = 0;
  let bodyStart = -1;
  for (let index = 0; index < css.length; index += 1) {
    const character = css[index];
    if (character === "{") {
      if (depth === 0 && css.slice(preludeStart, index).trim() === selector) {
        bodyStart = index + 1;
      }
      depth += 1;
    } else if (character === "}") {
      depth -= 1;
      if (depth === 0) {
        if (bodyStart >= 0) {
          bodies.push(css.slice(bodyStart, index));
          bodyStart = -1;
        }
        preludeStart = index + 1;
      }
    } else if (character === ";" && depth === 0) {
      preludeStart = index + 1;
    }
  }
  assert.ok(bodies.length > 0, `theme.css has no top-level ${selector} rule`);
  return bodies;
}

function oklchCustomProperties(bodies: readonly string[]): Map<string, OklchColour> {
  const tokens = new Map<string, OklchColour>();
  for (const declaration of bodies.flatMap((body) => unconditionalDeclarations(body))) {
    const match = /^\s*--([a-z0-9-]+)\s*:\s*(oklch\(.*\))\s*$/i.exec(declaration);
    if (match?.[1] !== undefined && match[2] !== undefined) {
      tokens.set(match[1], parseOklch(match[2]));
    }
  }
  return tokens;
}

function unconditionalDeclarations(body: string): string[] {
  let depth = 0;
  let flat = "";
  for (const character of body) {
    if (character === "{") {
      depth += 1;
    } else if (character === "}") {
      depth -= 1;
      flat += ";";
    } else if (depth === 0) {
      flat += character;
    }
  }
  return flat.split(";");
}

function token(theme: Theme, name: string): OklchColour {
  const colour = theme.tokens.get(name);
  assert.ok(colour, `the ${theme.name} theme declares no oklch() value for --${name}`);
  return colour;
}

function page(theme: Theme): SrgbColour {
  const background = token(theme, "background");
  assert.equal(background.alpha, 1, `the ${theme.name} theme's --background must be opaque`);
  return toSrgb(background).colour;
}

function paint(theme: Theme, name: string, backdrop: SrgbColour): SrgbColour {
  const colour = token(theme, name);
  return composite(toSrgb(colour).colour, colour.alpha, backdrop);
}

function surface(theme: Theme, name: string): SrgbColour {
  return paint(theme, name, page(theme));
}

function over(theme: Theme, name: string, alpha: number, backdrop: SrgbColour): SrgbColour {
  const colour = token(theme, name);
  return composite(toSrgb(colour).colour, colour.alpha * alpha, backdrop);
}

// Every surface a control sits on: the page, cards, popovers and dialogs, and the muted/50 footers of cards and dialogs.
function backdrops(theme: Theme): Backdrop[] {
  const card = surface(theme, "card");
  const popover = surface(theme, "popover");
  return [
    { name: "background", colour: page(theme) },
    { name: "card", colour: card },
    { name: "popover", colour: popover },
    { name: "card footer", colour: over(theme, "muted", footerMutedAlpha, card) },
    { name: "dialog footer", colour: over(theme, "muted", footerMutedAlpha, popover) },
  ];
}

function foregroundPairs(theme: Theme): string[] {
  return [...theme.tokens.keys()]
    .filter((name) => name.endsWith("-foreground"))
    .map((name) => name.slice(0, -"-foreground".length))
    .filter((base) => theme.tokens.has(base));
}

function assertMinimums(checks: readonly ContrastCheck[]): void {
  assert.ok(checks.length > 0, "no contrast checks were built");
  const failures = checks
    .map((check) => ({ ...check, ratio: contrastRatio(check.foreground, check.background) }))
    .filter(({ ratio, minimum }) => ratio < minimum)
    .map(
      ({ label, ratio, minimum }) =>
        `${label}: ${(Math.floor(ratio * 100) / 100).toFixed(2)}:1, needs ${minimum}:1`,
    );
  assert.equal(failures.length, 0, `contrast below the minimum:\n${failures.join("\n")}`);
}

test("ForegroundPairs_BothThemes_DeclareEveryExpectedPair", () => {
  for (const theme of themes) {
    const pairs = foregroundPairs(theme);
    const missing = expectedForegroundPairs.filter((base) => !pairs.includes(base));
    assert.deepEqual(missing, [], `the ${theme.name} theme lacks these <name>/<name>-foreground pairs`);
  }
});

test("ForegroundPairs_BothThemes_MeetAa", () => {
  assertMinimums(
    themes.flatMap((theme) =>
      foregroundPairs(theme).map((base) => {
        const background = surface(theme, base);
        return {
          label: `${theme.name}: ${base}-foreground on ${base}`,
          foreground: paint(theme, `${base}-foreground`, background),
          background,
          minimum: textMinimum,
        };
      }),
    ),
  );
});

test("BodyText_OnBackgroundCardAndMuted_MeetsAa", () => {
  assertMinimums(
    themes.flatMap((theme) =>
      bodyTexts.flatMap((text) =>
        bodySurfaces.map((name) => {
          const background = surface(theme, name);
          return {
            label: `${theme.name}: ${text} on ${name}`,
            foreground: paint(theme, text, background),
            background,
            minimum: textMinimum,
          };
        }),
      ),
    ),
  );
});

test("IntentText_OnBackgroundAndCard_MeetsAa", () => {
  assertMinimums(
    themes.flatMap((theme) =>
      intents.flatMap((intent) =>
        intentSurfaces.map((name) => {
          const background = surface(theme, name);
          return {
            label: `${theme.name}: ${intent} text on ${name}`,
            foreground: paint(theme, intent, background),
            background,
            minimum: textMinimum,
          };
        }),
      ),
    ),
  );
});

test("IntentText_OnItsOwnTintOverEveryBackdrop_MeetsAa", () => {
  assertMinimums(
    themes.flatMap((theme) =>
      intents.flatMap((intent) =>
        backdrops(theme).map((backdrop) => {
          const background = over(theme, intent, theme.tintAlpha, backdrop.colour);
          return {
            label: `${theme.name}: ${intent} text on ${intent} at ${Math.round(theme.tintAlpha * 100)}% over ${backdrop.name}`,
            foreground: paint(theme, intent, background),
            background,
            minimum: textMinimum,
          };
        }),
      ),
    ),
  );
});

test("DestructiveText_OnItsHoverTintOverEveryBackdrop_MeetsAa", () => {
  assertMinimums(
    themes.flatMap((theme) =>
      backdrops(theme).map((backdrop) => {
        const background = over(theme, "destructive", theme.hoverTintAlpha, backdrop.colour);
        return {
          label: `${theme.name}: destructive text on destructive at ${Math.round(theme.hoverTintAlpha * 100)}% over ${backdrop.name}`,
          foreground: paint(theme, "destructive", background),
          background,
          minimum: textMinimum,
        };
      }),
    ),
  );
});

test("PrimaryForeground_OnTheHoveredPrimaryOverEveryBackdrop_MeetsAa", () => {
  assertMinimums(
    themes.flatMap((theme) =>
      backdrops(theme).map((backdrop) => {
        const background = over(theme, "primary", primaryHoverAlpha, backdrop.colour);
        return {
          label: `${theme.name}: primary-foreground on primary at ${primaryHoverAlpha * 100}% over ${backdrop.name}`,
          foreground: paint(theme, "primary-foreground", background),
          background,
          minimum: textMinimum,
        };
      }),
    ),
  );
});

test("Input_AgainstBackgroundAndCard_MeetsNonTextContrast", () => {
  assertMinimums(
    themes.flatMap((theme) =>
      inputSurfaces.map((name) => {
        const background = surface(theme, name);
        return {
          label: `${theme.name}: input against ${name}`,
          foreground: paint(theme, "input", background),
          background,
          minimum: nonTextMinimum,
        };
      }),
    ),
  );
});

test("Ring_AgainstBackground_MeetsNonTextContrast", () => {
  assertMinimums(
    themes.map((theme) => {
      const background = page(theme);
      return {
        label: `${theme.name}: ring against background`,
        foreground: paint(theme, "ring", background),
        background,
        minimum: nonTextMinimum,
      };
    }),
  );
});
