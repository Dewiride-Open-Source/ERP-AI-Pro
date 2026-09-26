# Design system

`@dewiride/erp-ui` (`frontend/packages/ui`) is the design system of the web app: the design tokens, the shadcn/ui primitives generated from the `radix-nova` registry style on Radix, and the composites built on them (`components/theme/`, `components/upload/`). Every token and primitive is rendered in both themes at `/design/kitchen-sink`. The decisions behind this page are in [ADR-0023](../adr/0023-design-tokens-shared-theme-and-generated-primitives.md).

## Stylesheets

```
frontend/packages/ui/src/styles/
├─ globals.css         @import "tailwindcss"; @import "./theme.css"; @source "../**/*.{ts,tsx}";
├─ theme.css           @import "tw-animate-css"; @import "shadcn/tailwind.css"; the five token files; "./base.css";
│                      @custom-variant dark (&:is(.dark *));
│                      @theme inline { --color-* (38), --radius-sm..4xl }   :root { --radius, colours }   .dark { colours }
├─ base.css            @layer base { * border-border outline-ring/50; html color-scheme light; html.dark color-scheme dark;
│                                    body bg-background text-foreground antialiased }
└─ tokens/
   ├─ typography.css   @theme inline { --font-sans, --font-sans--font-feature-settings, --font-mono, --font-heading }
   │                   @theme { --text-title|heading|body|caption|eyebrow and their companions }
   ├─ layout.css       @theme { --spacing-section, --spacing-header, --container-page }   @theme inline { --spacing-gutter }
   │                   :root { --layout-gutter (@variant sm), --layer-sticky, --layer-overlay }
   ├─ elevation.css    @theme { --shadow-2xs..2xl }   :root / .dark { --elevation-color-faint, --elevation-color, --elevation-color-strong }
   ├─ focus.css        :root { --focus-ring-width, --focus-ring-offset }   @utility focus-ring
   └─ motion.css       :root { --motion-duration-*, --motion-ease-* }   @media (prefers-reduced-motion: reduce) :root { … }
                       @theme inline { transition defaults, --ease-*, --animate-*, @keyframes }   @utility glow-offset
                       @layer base { the reduced-motion safety net }
```

- `@import "tailwindcss"` appears only in `globals.css`, and first. Importing it a second time, in `theme.css` or anywhere a consumer also imports `globals.css`, emits a second preflight.
- In every file the `@import` lines come first; an `@variant` block comes last inside its `:root`.
- `theme.css` is the file `components.json` names in `tailwind.css`, so anything the shadcn CLI ever writes into a stylesheet lands next to `:root`, `.dark` and `@theme inline`.
- `globals.css`'s `@source` resolves relative to `globals.css` itself, so it covers `packages/ui/src`. The web app's own sources are found by Tailwind's automatic detection, which starts at the app's working directory.
- CSS files do not count toward the folder cap; `scripts/checks/comment-policy.ts` scans their block comments.

## Import contract

| Export of `@dewiride/erp-ui` | Points to | Use |
|---|---|---|
| `./globals.css` | `src/styles/globals.css` | the stylesheet an app loads once: `import "@dewiride/erp-ui/globals.css"` in `apps/web/src/app/layout.tsx` |
| `./theme.css` | `src/styles/theme.css` | the tokens without Tailwind, for an app that owns its Tailwind entry |
| `./postcss.config` | `postcss.config.mjs` | re-exported by `apps/web/postcss.config.mjs` |
| `./lib/*` | `src/lib/*.ts` | `cn` (`createCn` from `cn/config`, extended with the typography roles; see [Typography](#typography)) |
| `./components/*` | `src/components/*.tsx` | primitives (`components/ui/<name>`) and composites (`components/<concern>/<name>`) |

- **A new app** imports `@dewiride/erp-ui/globals.css` from its root layout, or writes its own entry in this order: `@import "tailwindcss"; @import "@dewiride/erp-ui/theme.css"; @source "<relative path to frontend/packages/ui/src>";`. `theme.css` carries no `@source`, so the entry must name the ui sources or the primitives' classes are never generated.
- **A package or CSS Module** that needs `@apply`, `@variant` or `--theme()` writes `@reference "@dewiride/erp-ui/globals.css";`, which makes the theme known without emitting any CSS.
- **A new workspace package that holds components** needs an `@source` line for its sources in the stylesheet the app loads (today `globals.css`): automatic detection skips `node_modules`, through which pnpm links workspace packages.
- Tailwind resolves CSS imports with the `style` condition; the two CSS exports are plain strings, which it accepts. A conditional CSS export would need a `style` condition.
- Prettier's `tailwindStylesheet` (`packages/config/prettier.mjs`) stays on `globals.css`, the entry that imports Tailwind, so class sorting sees every token.

## Where a token goes

- A value that changes under `.dark` or under a media query is a plain custom property on `:root`/`.dark` (or inside the media query), mapped to a utility in `@theme inline` (`--color-card: var(--card)`). `@theme` cannot be nested under a selector or a media query.
- A literal, theme-independent scale lives in `@theme` (`--text-title`, `--shadow-md`, `--container-page`).
- A value that must not produce a utility stays a plain custom property (`--layer-sticky`, `--motion-duration-normal`) and is used through the `utility-(--name)` form (`z-(--layer-sticky)`, `duration-(--motion-duration-normal)`).
- Never a `--tw-*` name (Tailwind's internal namespace), and never a name Tailwind reads by accident (`--font-size-*` falls under `--font-*` and would create a font-family utility).
- A typography role never reuses a colour name: when `--color-x` and `--text-x` both exist, `text-x` resolves to the colour.

### Adding a token

1. Put the value in the file of its group under `src/styles/` by the rules above. A colour gets its light value in `theme.css` `:root`, its dark value in `.dark`, and `--color-<name>: var(--<name>)` in the `@theme inline` block; a colour that carries text gets a `<name>-foreground` partner.
2. Add it to the tables on this page, and a colour pair or status colour to the contrast test (`src/styles/contrast.test.ts`).
3. Show it on the kitchen sink (`features/platform/design/kitchen-sink/tokens/token-catalogue.ts`); the kitchen-sink spec fails when a raw `oklch(` colour on `:root` has no swatch.
4. Run `pnpm test:unit`, `pnpm format:check` and `pnpm build` from `frontend/`.

## Colour

OKLCH values, light on `:root` and dark on `.dark`, each mapped as `--color-<name>` so `bg-<name>`, `text-<name>`, `border-<name>`, `ring-<name>` and their opacity forms (`bg-<name>/10`) exist.

| Group | Token | Light | Dark |
|---|---|---|---|
| Surfaces | `background` | `oklch(0.985 0.004 260)` | `oklch(0.16 0.02 268)` |
| | `foreground` | `oklch(0.2 0.03 265)` | `oklch(0.95 0.01 265)` |
| | `card` | `oklch(1 0 0)` | `oklch(0.2 0.022 268)` |
| | `card-foreground` | `oklch(0.2 0.03 265)` | `oklch(0.95 0.01 265)` |
| | `popover` | `oklch(1 0 0)` | `oklch(0.2 0.022 268)` |
| | `popover-foreground` | `oklch(0.2 0.03 265)` | `oklch(0.95 0.01 265)` |
| Intents | `primary` | `oklch(0.5 0.22 275)` | `oklch(0.72 0.17 275)` |
| | `primary-foreground` | `oklch(0.99 0.005 275)` | `oklch(0.16 0.04 275)` |
| | `secondary` | `oklch(0.955 0.012 265)` | `oklch(0.26 0.025 268)` |
| | `secondary-foreground` | `oklch(0.3 0.04 270)` | `oklch(0.93 0.01 265)` |
| | `muted` | `oklch(0.955 0.012 265)` | `oklch(0.26 0.025 268)` |
| | `muted-foreground` | `oklch(0.5 0.03 265)` | `oklch(0.7 0.02 265)` |
| | `accent` | `oklch(0.94 0.04 275)` | `oklch(0.3 0.06 275)` |
| | `accent-foreground` | `oklch(0.32 0.12 275)` | `oklch(0.93 0.03 275)` |
| Status | `destructive` | `oklch(0.53 0.2 27)` | `oklch(0.7 0.19 25)` |
| | `destructive-foreground` | `oklch(0.99 0.01 27)` | `oklch(0.16 0.03 25)` |
| | `success` | `oklch(0.5 0.12 155)` | `oklch(0.72 0.16 155)` |
| | `success-foreground` | `oklch(0.99 0.01 155)` | `oklch(0.16 0.03 155)` |
| | `warning` | `oklch(0.52 0.12 60)` | `oklch(0.82 0.15 80)` |
| | `warning-foreground` | `oklch(0.99 0.01 60)` | `oklch(0.2 0.05 80)` |
| | `info` | `oklch(0.51 0.12 245)` | `oklch(0.74 0.12 235)` |
| | `info-foreground` | `oklch(0.99 0.01 245)` | `oklch(0.16 0.03 235)` |
| Lines | `border` | `oklch(0.91 0.012 265)` | `oklch(1 0 0 / 10%)` |
| | `input` | `oklch(0.64 0.015 265)` | `oklch(1 0 0 / 35%)` |
| | `ring` | `oklch(0.5 0.22 275)` | `oklch(0.72 0.17 275)` |
| Charts | `chart-1` | `oklch(0.5 0.22 275)` | `oklch(0.72 0.17 275)` |
| | `chart-2` | `oklch(0.7 0.16 200)` | `oklch(0.75 0.14 200)` |
| | `chart-3` | `oklch(0.78 0.16 75)` | `oklch(0.82 0.15 80)` |
| | `chart-4` | `oklch(0.62 0.17 155)` | `oklch(0.72 0.16 155)` |
| | `chart-5` | `oklch(0.62 0.2 330)` | `oklch(0.7 0.18 330)` |
| Sidebar | `sidebar` | `oklch(0.97 0.008 262)` | `oklch(0.19 0.022 268)` |
| | `sidebar-foreground` | `oklch(0.25 0.03 265)` | `oklch(0.93 0.01 265)` |
| | `sidebar-primary` | `oklch(0.5 0.22 275)` | `oklch(0.72 0.17 275)` |
| | `sidebar-primary-foreground` | `oklch(0.99 0.005 275)` | `oklch(0.16 0.04 275)` |
| | `sidebar-accent` | `oklch(0.93 0.03 275)` | `oklch(0.28 0.05 275)` |
| | `sidebar-accent-foreground` | `oklch(0.3 0.1 275)` | `oklch(0.93 0.03 275)` |
| | `sidebar-border` | `oklch(0.9 0.012 265)` | `oklch(1 0 0 / 10%)` |
| | `sidebar-ring` | `oklch(0.5 0.22 275)` | `oklch(0.72 0.17 275)` |

- The four status colours (`destructive`, `success`, `warning`, `info`, the set sonner's toast types use) are text-strength in both themes. Use them as text (`text-success`), as a tint behind their own text (`bg-success/10 text-success dark:bg-success/20`, the form the generated primitives use for `destructive`), or as a solid fill with their foreground (`bg-success text-success-foreground`).
- `input` draws the border that identifies a control (input, textarea, select, native select, input group, checkbox, radio) and the unchecked switch track; the generated primitives also use it for outline and dark-theme field tints (`dark:bg-input/30`). `border` is decorative and carries no contrast requirement.
- `ring` is the focus colour; the generated primitives also draw `ring-ring/50` around a full-strength `border-ring`.
- The chart and sidebar tokens are defined for the chart and sidebar primitives, which are not installed yet (see [Excluded primitives](#excluded-primitives)).

### Contrast gates

Ratios are WCAG 2.2 contrast ratios computed from the OKLCH values (OKLab to linear sRGB, clipped to the sRGB gamut, then relative luminance). A token with alpha, and a tint, is composited over its surface in encoded sRGB. The tint is the colour at 10 % over `background` in light and at 20 % in dark, matching `bg-<colour>/10` and `dark:bg-<colour>/20`. `packages/ui/src/styles/contrast.test.ts` reads `theme.css` and fails when a gate is missed in either theme; `src/lib/oklch.ts` holds the conversion.

| Gate | Minimum | Lowest light | Lowest dark |
|---|---|---|---|
| every `X-foreground` on `X` (including `card`, `popover`, `sidebar-*`) | 4.5 | 5.27 (`muted-foreground` on `muted`) | 5.82 (`muted-foreground` on `muted`) |
| `foreground` and `muted-foreground` on `background`, `card` and `muted` | 4.5 | 5.27 (`muted-foreground` on `muted`) | 5.82 (`muted-foreground` on `muted`) |
| `primary`, `destructive`, `success`, `warning`, `info` as text on `background` and on `card` | 4.5 | 5.42 (`success` on `background`) | 6.23 (`destructive` on `card`) |
| the same five as text on their own tint | 4.5 | 4.71 (`success`) | 5.16 (`destructive`) |
| `input` against `background` and `card` (WCAG 2.2 SC 1.4.11, the boundary that identifies a control) | 3 | 3.22 (`background`) | 3.17 (`background`) |
| `ring` against `background` (SC 1.4.11, the focus indicator) | 3 | 6.24 | 7.39 |

The status colour pairs as solid fills: light `destructive` 5.63, `success` 5.52, `warning` 5.52, `info` 5.53; dark 6.70, 8.32, 10.24, 8.58. In light, `chart-2` (2.27) and `chart-3` (1.96) against `background` are below the 3:1 that SC 1.4.11 asks of a graphical object; the charts are decorative today, and the palette is validated by `reporting-web-dashboards-and-reports`, which brings the first chart.

## Typography

| Token | Value |
|---|---|
| `--font-sans` (`font-sans`) | `var(--font-geist-sans), ui-sans-serif, system-ui, sans-serif` |
| `--font-sans--font-feature-settings` | `"cv11", "ss01", "tnum"`: applied by `font-sans` and by Tailwind's preflight on `html`, so every number is tabular |
| `--font-mono` (`font-mono`) | `var(--font-geist-mono), ui-monospace, SFMono-Regular, monospace` |
| `--font-heading` (`font-heading`) | `var(--font-sans)`, used by the generated card and dialog titles |

`--font-geist-sans` and `--font-geist-mono` are set on `<html>` by the approved `geist` package (`GeistSans.variable`, `GeistMono.variable` in the root layout).

| Utility | Size | Line height | Letter spacing | Weight | Use |
|---|---|---|---|---|---|
| `text-title` | 1.875rem | 2.25rem (`calc(2.25 / 1.875)`) | -0.025em | 600 | the page `h1` |
| `text-heading` | 1.25rem | 1.75rem (`calc(1.75 / 1.25)`) | -0.025em | 600 | a section `h2` outside a card |
| `text-body` | 0.875rem | 1.25rem (`calc(1.25 / 0.875)`) | | | dense body copy |
| `text-caption` | 0.75rem | 1rem (`calc(1 / 0.75)`) | | | captions and metadata |
| `text-eyebrow` | 0.75rem | 1rem | 0.1em | 600 | the label above a page title, always with `uppercase` (`text-eyebrow text-primary uppercase`) |

The companions (`--text-<role>--line-height`, `--letter-spacing`, `--font-weight`) apply only when no explicit `leading-*`, `tracking-*` or `font-*` utility is present. Tailwind's `text-xs`…`text-9xl`, weights, tracking and leading keep their defaults.

The roles and class merging: the `cn` package's default merge tables know only Tailwind's own text sizes and read any other `text-<name>` as a colour. `@dewiride/erp-ui/lib/utils` therefore exports a `cn` built with `createCn` from `cn/config` whose `font-size` group also holds the five roles, so in application code `cn("text-base", "text-title")` keeps `text-title` and `cn("text-eyebrow text-primary")` keeps both. The generated primitives import `cn` from the `cn` package itself, with the default tables: a role passed in a primitive's `className` would stay beside the primitive's own size (which Tailwind emits later, so it wins) and could drop the primitive's text colour. A role therefore goes on a plain element, never on a component: the ESLint rule `no-restricted-syntax` with the selectors of `@dewiride/erp-config/eslint/type-roles` (in both the web app's and the ui package's config) fails on a role class in the `className` of any capitalised JSX element, including inside `cn(...)` and template literals. A new role is added in three places, which `src/lib/utils.test.ts` keeps together: `tokens/typography.css`, the `createCn` extension in `src/lib/utils.ts`, and `typeRoles` in `packages/config/eslint/type-roles.mjs`.

## Spacing and layout

| Utility | Token | Value | Use |
|---|---|---|---|
| numeric steps (`p-4`, `gap-6`) | `--spacing` (Tailwind default) | 0.25rem per step | inside components; the generated primitives use them |
| `px-gutter` (every spacing utility) | `--spacing-gutter: var(--layout-gutter)` | 1rem, 1.5rem from `sm` (40rem) | page side padding |
| `gap-section` | `--spacing-section` | 1.5rem | between the blocks of a page |
| `h-header`, `scroll-mt-header` | `--spacing-header` | 3.5rem | the sticky shell header and in-page anchor targets below it |
| `max-w-page` | `--container-page` | 72rem | the shell's content width |

Breakpoints are Tailwind's defaults (`sm` 40rem, `md` 48rem, `lg` 64rem, `xl` 80rem, `2xl` 96rem); every page works from 360 px.

## Radius

`--radius: 0.75rem` on `:root`; the scale is shadcn's formula.

| Utility | Token | Value |
|---|---|---|
| `rounded-sm` | `calc(var(--radius) * 0.6)` | 0.45rem |
| `rounded-md` | `calc(var(--radius) * 0.8)` | 0.6rem |
| `rounded-lg` | `var(--radius)` | 0.75rem |
| `rounded-xl` | `calc(var(--radius) * 1.4)` | 1.05rem |
| `rounded-2xl` | `calc(var(--radius) * 1.8)` | 1.35rem |
| `rounded-3xl` | `calc(var(--radius) * 2.2)` | 1.65rem |
| `rounded-4xl` | `calc(var(--radius) * 2.6)` | 1.95rem |

`rounded-xs`, `rounded` and `rounded-full` keep Tailwind's values.

## Elevation

Tailwind's default shadow geometry, tinted by three plain variables so shadows read in both themes. `shadow-<colour>` still recolours a shadow.

| Utility | Geometry | Colour variable |
|---|---|---|
| `shadow-2xs` | `0 1px` | `--elevation-color-faint` |
| `shadow-xs` | `0 1px 2px 0` | `--elevation-color-faint` |
| `shadow-sm` | `0 1px 3px 0`, `0 1px 2px -1px` | `--elevation-color` |
| `shadow-md` | `0 4px 6px -1px`, `0 2px 4px -2px` | `--elevation-color` |
| `shadow-lg` | `0 10px 15px -3px`, `0 4px 6px -4px` | `--elevation-color` |
| `shadow-xl` | `0 20px 25px -5px`, `0 8px 10px -6px` | `--elevation-color` |
| `shadow-2xl` | `0 25px 50px -12px` | `--elevation-color-strong` |

| Variable | Light | Dark |
|---|---|---|
| `--elevation-color-faint` | `oklch(0.2 0.03 265 / 5%)` | `oklch(0 0 0 / 20%)` |
| `--elevation-color` | `oklch(0.2 0.03 265 / 10%)` | `oklch(0 0 0 / 40%)` |
| `--elevation-color-strong` | `oklch(0.2 0.03 265 / 25%)` | `oklch(0 0 0 / 60%)` |

## Layers

| Variable | Value | Use |
|---|---|---|
| `--layer-sticky` | 40 | the sticky shell header (`z-(--layer-sticky)`) |
| `--layer-overlay` | 50 | the value of the literal `z-50` the generated overlays and popups use (alert dialog, dialog, sheet, popover, hover card, tooltip, select, the three menus, navigation menu), which cannot be edited |

Sonner's toaster sets its own `z-index` (999999999) in the style it injects, above every layer. A negative `z-index` inside an `isolate` context (the glow of the `(auth)` layout) stays local and is not a layer.

## Focus ring

`focus-ring` is the focus indicator of every interactive element the design system does not generate: `outline-offset: var(--focus-ring-offset)` always, and `outline: var(--focus-ring-width) solid var(--ring)` on `:focus-visible` (2px each). An outline survives forced-colours mode, and full-strength `ring` passes 3:1 in both themes. Generated primitives keep their own `focus-visible:ring-3 focus-visible:ring-ring/50` with a full-strength `border-ring`.

## Motion

| Token | Value | Under `prefers-reduced-motion: reduce` | Use |
|---|---|---|---|
| `--motion-duration-fast` | 150ms | 0.01ms | the default of every `transition*` utility without a `duration-*` |
| `--motion-duration-normal` | 200ms | 0.01ms | state changes of a control (`duration-(--motion-duration-normal)`) |
| `--motion-duration-slow` | 300ms | 0.01ms | elements entering (`animate-fade-up`) |
| `--motion-duration-ambient` | 6s | unchanged; the safety net runs it once for 0.01ms | looping decoration only (`animate-glow`) |
| `--motion-ease-standard` | `cubic-bezier(0.2, 0, 0, 1)` | | the default transition curve; `ease-standard` |
| `--motion-ease-enter` | `cubic-bezier(0.16, 1, 0.3, 1)` | | an exponential ease-out for entrances; `ease-enter` |

`@theme inline` maps them: `--default-transition-duration` and `--default-transition-timing-function` (so `transition`, `transition-colors` and the rest follow the tokens), `--ease-standard`, `--ease-enter`, `--animate-fade-up: fade-up var(--motion-duration-slow) var(--motion-ease-enter) both` and `--animate-glow: glow var(--motion-duration-ambient) ease-in-out infinite`, with their `@keyframes`. `glow-offset` starts a second glow half a cycle later (`animation-delay: calc(var(--motion-duration-ambient) / -2)`). tw-animate-css takes the enter and exit duration of `animate-in`/`animate-out` from `--tw-duration`, so `duration-(--motion-duration-*)` drives Radix enter and exit animations too.

- Interface feedback runs between 150 and 300 ms: write `duration-(--motion-duration-normal) ease-standard`, never a literal `duration-200` or `ease-out`.
- **Reduced motion is handled once, in `tokens/motion.css`.** One `@media (prefers-reduced-motion: reduce)` block collapses the three interface durations to 0.01ms, and a global safety net in `@layer base` sets `animation-duration` and `transition-duration` to `0.01ms !important`, `animation-iteration-count: 1 !important` and `scroll-behavior: auto !important` on every element and pseudo-element. The net covers what the tokens cannot reach: the literal `duration-100` of the generated overlays and popups, `animate-spin` (the spinner and sonner's loading icon), `animate-pulse` (the skeleton) and tw-animate-css's `.15s` fallback. Components add no `motion-reduce:` variants of their own. Under the preference a spinner is a still icon, so a loading state is also announced (`role="status"`, `aria-busy`).
- **0.01ms, not 0.** MDN's `transitionend` reference: when the duration and the delay are both 0s there is no transition and none of the transition events fire, so code that waits for `transitionend` would never continue. MDN's `animation-duration` reference: an animation at 0s still fires `animationstart` and `animationend`. 0.01ms is imperceptible and keeps both kinds of event firing.
- `motion` (approved in ADR-0009) is not installed; `web-foundation-feedback-motion-and-accessibility-baseline` decides whether orchestrated transitions need it.

## Theme wiring

- `ThemeProvider` (`components/theme/theme-provider.tsx`) wraps `next-themes` with `attribute="class"`, `defaultTheme="system"`, `enableSystem`, `disableTransitionOnChange` and the CSP nonce, which the root layout reads from the `x-nonce` request header set by `proxy.ts`; `<html>` carries `suppressHydrationWarning` because the theme script sets its class before hydration.
- `@custom-variant dark (&:is(.dark *))` is shadcn's form: `dark:` matches descendants of the `.dark` element, not `<html>` itself, so `base.css` sets `html.dark { color-scheme: dark }` with a plain selector.
- The root layout exports `viewport = { colorScheme: "light dark" }`, which renders `<meta name="color-scheme" content="light dark">`: the browser may then draw its canvas and built-in controls in the system's scheme before any stylesheet applies, which MDN recommends against a white flash on a dark system. `html { color-scheme: light }` / `html.dark { color-scheme: dark }` in `base.css` set the scheme of the chosen theme, also for a page without JavaScript; next-themes additionally sets `style.color-scheme` on `<html>` (`enableColorScheme`, on by default).
- `ThemeToggle` (`components/theme/theme-toggle.tsx`) is a Radix `RadioGroup` (`aria-label="Colour theme"`, `data-testid="theme-toggle"`, items `theme-light`, `theme-dark`, `theme-system`): one tab stop, arrow keys move and select, and no item is checked until the component has mounted (`useSyncExternalStore`), because the stored theme is unknown on the server.
- The content security policy allows `style-src 'self' 'unsafe-inline'`, which covers Radix's inline positioning styles, the next-themes transition blocker and the style sonner injects.

## Root providers

Inside `ThemeProvider`, `apps/web/src/app/layout.tsx` wraps the page in `TooltipProvider` (the radix-nova `Tooltip` does not wrap a provider of its own; the generated provider's `delayDuration` is 0) and renders one `<Toaster />` from `@dewiride/erp-ui/components/ui/sonner`, which follows the active theme through `useTheme()`. Client code shows a toast with `toast()` from `sonner`, and it renders in that one toaster; no feature mounts a second `Toaster` or `TooltipProvider`.

## Primitives

The primitives live in `packages/ui/src/components/ui/` and are generated by the shadcn CLI from the `radix-nova` style (`packages/ui/components.json`: `radix-nova`, `rsc`, `tsx`, `cssVariables`, `baseColor` `neutral`, `iconLibrary` `lucide`, `rtl` false). Installed: accordion, alert, alert-dialog, aspect-ratio, avatar, badge, breadcrumb, button, button-group, card, checkbox, collapsible, context-menu, dialog, dropdown-menu, empty, field, hover-card, input, input-group, item, kbd, label, menubar, native-select, navigation-menu, pagination, popover, progress, radio-group, scroll-area, select, separator, sheet, skeleton, slider, sonner, spinner, switch, table, tabs, textarea, toggle, toggle-group, tooltip.

- The folder is exempt from the folder cap and the comment policy; ESLint, Prettier and `tsc` check it like any other code.
- Never run `shadcn init`, `apply`, `migrate base-color` or `add font-*` here: they rewrite the stylesheet (and `init` the components), and the font items conflict with the approved `geist` package. No `@base-ui/*` package enters the lockfile.
- Primitives are imported as `@dewiride/erp-ui/components/ui/<name>`. There is no `./hooks/*` export yet; it arrives with the sidebar.

### Adding a primitive

Run everything from `frontend/`.

1. If the registry item needs a package, it must be approved (CLAUDE.md §6). Pin it in the `catalog` of `pnpm-workspace.yaml`, add `"<package>": "catalog:"` to `packages/ui/package.json` and the package to the Dependabot `ui` group first: `catalogMode: strict` refuses a version the catalog does not hold.
2. `pnpm --filter @dewiride/erp-ui exec shadcn add <item> --dry-run`: check the files, the dependencies, and that no stylesheet appears in the plan. `--view <path>` prints a file.
3. `pnpm --filter @dewiride/erp-ui exec shadcn add <item>` without `--overwrite`, so an existing primitive is never replaced. `--overwrite` is used only to refresh a primitive to the registry's version, and then every register entry for that file is applied again.
4. `pnpm format`: the CLI writes its own formatting.
5. `pnpm --filter @dewiride/erp-ui exec shadcn add <item> --dry-run --diff`: apart from line wrapping, the only differences from the registry may be the register entries below. `shadcn diff` is deprecated in CLI 4.21.0 and reports "No updates found" even for a patched file, so it proves nothing.
6. `pnpm lint`, `pnpm typecheck` and `pnpm build`. The web app typechecks the ui sources as its own (they are consumed as source through the package exports, and `skipLibCheck` only skips `.d.ts` files), so a registry file that does not compile under `exactOptionalPropertyTypes` fails here.
7. Show the primitive on the kitchen sink in every state it supports and extend the kitchen-sink spec.

### Register of local changes to generated primitives

The only permitted edits to a generated primitive are the smallest change that makes the registry file compile or behave correctly, recorded here in the same change (ADR-0023). A patch carries no code comment; this table is its record. Every entry is guarded, so losing it on a refresh fails the build or a test.

| File | Change against the registry | Why | Guard |
|---|---|---|---|
| `progress.tsx` | `Progress` passes `value={value}` to `ProgressPrimitive.Root` as well as using it for the indicator transform. | The registry destructures `value` for the indicator and never hands it to the Radix root, so the progress bar has no `aria-valuenow`. | `e2e/tests/platform/attachments/files.spec.ts` asserts `aria-valuenow` on the upload progress. |
| `context-menu.tsx`, `dropdown-menu.tsx`, `menubar.tsx` | `ContextMenuCheckboxItem`, `DropdownMenuCheckboxItem` and `MenubarCheckboxItem` leave `checked` in `props`, so it reaches the Radix `CheckboxItem` through `{...props}`; the registry destructures it and passes `checked={checked}`. | Under `exactOptionalPropertyTypes` (`packages/config/tsconfig/base.json`) an explicitly passed `checked` of type `CheckedState \| undefined` does not fit Radix's optional `checked?: CheckedState` (TS2375). | `pnpm typecheck` |
| `slider.tsx` | `Slider` leaves `value` and `defaultValue` in `props`: it reads them with `const { value, defaultValue } = props` for the thumb count, and they reach `SliderPrimitive.Root` through `{...props}`; the registry destructures both and passes `defaultValue={defaultValue} value={value}`. | The same TS2375 for `value?: number[]` and `defaultValue?: number[]`. | `pnpm typecheck` |
| `sonner.tsx` | `Toaster` passes `theme={theme as NonNullable<ToasterProps["theme"]>}` instead of `theme as ToasterProps["theme"]`. | `ToasterProps["theme"]` includes `undefined`, which `exactOptionalPropertyTypes` refuses for sonner's optional `theme`; `useTheme()` defaults it to `"system"`, so the value is never `undefined`. | `pnpm typecheck` |

### Excluded primitives

The `radix-nova` registry items that are not installed, each with the roadmap item that decides it. The kitchen sink lists them with the same reasons.

| Item | Why it is not installed | Decided in |
|---|---|---|
| `sidebar` | its generated `use-mobile` hook sets state inside an effect and fails `react-hooks/set-state-in-effect`, and primitives are not hand-edited beyond the register | `authentication-authenticated-application-shell` |
| `combobox` | the radix-nova combobox imports `@base-ui/react` | `web-foundation-forms-and-validation-kit` (an in-house Radix popover combobox, or `cmdk`) |
| `calendar`, `date-picker` | `react-day-picker` and `date-fns` (not approved); the date picker is a calendar-plus-popover recipe | `web-foundation-forms-and-validation-kit` |
| `command` | `cmdk` (not approved) | `authentication-authenticated-application-shell` (Ctrl+K palette) |
| `drawer` | `vaul` (not approved); the mobile navigation uses `sheet` | `authentication-authenticated-application-shell` |
| `chart` | `recharts` (not approved) | `reporting-web-dashboards-and-reports` |
| `carousel` | `embla-carousel-react` (not approved) | no roadmap item needs it |
| `input-otp` | `input-otp` (not approved) | no roadmap item needs it; Entra ID owns sign-in |
| `resizable` | `react-resizable-panels` (not approved) | `finance-banking-web-reconciliation-and-remittances`, if its workbench needs split panes |
| `direction` | right-to-left support only; the UI is English-only (`rtl: false`) | not planned |
| `attachment`, `bubble`, `marker`, `message` | the chat set, unused until the assistant exists; `attachment` would also clash in name with the attachments module | `ai-platform-assistant-ux` |
| `message-scroller`, `questionnaire` | `@shadcn/react` (not approved) | `ai-platform-assistant-ux` |
| `form` | an empty registry item; forms are built from `field` | `web-foundation-forms-and-validation-kit` |
| `toast` | Base UI only; the Radix style uses `sonner` | not planned |

## Kitchen sink

`/design/kitchen-sink` is the living reference of the design system: every token group, every raw colour token as a swatch, every installed primitive in its default, disabled, invalid and loading states where it has them, the composites, and the excluded primitives with their reasons, in whichever theme is active.

- **Where.** `apps/web/src/app/(app)/design/kitchen-sink/page.tsx` renders `KitchenSink` from `features/platform/design` (a web-only module of the Platform domain, like `features/identity/auth`) with the title "Design system". It sits inside the `(app)` shell and has no `nav.ts`, so it appears in no navigation and no other page links to it.
- **Available in every environment.** Playwright runs against the production build, which must serve the page it screenshots; the page is static and exposes nothing beyond the CSS and JavaScript already shipped; it inherits `robots: noindex` from the root metadata. Being inside `(app)`, it requires a signed-in user from the authentication phase on.
- **Sections**, in order, each a `<section>` with an `id` the index links to: tokens `colours`, `typography`, `spacing`, `radius`, `elevation`, `layers`, `motion`; primitives `actions`, `inputs`, `selection`, `feedback`, `overlays`, `menus`, `navigation`, `data-display`, `composites`, `excluded`. The motion section includes a demo that reports the reduced-motion preference and the live duration tokens, replays `animate-fade-up` and slides an element with a token-driven transition. The primitive sections include an example form whose empty submit shows a field error and whose valid submit shows a toast.
- **Files.** `features/platform/design/kitchen-sink/components/` (the page, the index, the section frame and `sections.ts`, the ordered catalogue of sections), `tokens/` (`token-catalogue.ts` and one component per token group) and `primitives/` (one showcase per primitive section).
- **Extending it.** A new colour token gets an entry in `token-catalogue.ts`, written as full class strings (`bg-<name>`, `text-<name>`) so Tailwind sees them; other tokens join their group's component. A new primitive joins the showcase of its section in every state it supports; a new section is added to `sections.ts` and to the `kitchenSinkSections` mirror in `frontend/e2e/pages/platform/design/kitchen-sink.page.ts`. The spec (`frontend/e2e/tests/platform/design/kitchen-sink.spec.ts`) is extended in the same change; the [testing guide](../guides/testing.md) describes it.
