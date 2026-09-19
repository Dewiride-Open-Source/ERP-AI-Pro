# ADR-0007: Next.js App Router with feature folders that mirror the backend

Status: accepted
Date: 2026-09-19

## Context

The owner wants a pure web application with a world-class light/dark UI, no landing page, English only, and the same domain → module → feature hierarchy as the backend. Next.js 16 is the current major: App Router, Turbopack by default, `proxy.ts` instead of `middleware.ts`, `output: 'standalone'` for containers, and a formal security release cadence.

## Decision

- `frontend/` is a pnpm 12 workspace on Node 24 LTS with `apps/web` (Next.js 16 App Router, `src/`), `packages/config` (tsconfig, ESLint flat config, Prettier), `packages/ui` (Tailwind 4 CSS-first tokens, shadcn/ui on Radix, theme provider and toggle) and `e2e` (Playwright).
- `src/app/` is routing only. Every page imports one component from `src/features/<domain>/<module>/index.ts`. Feature folders mirror backend modules: `features/<domain>/<module>/<feature>/{components, server/{actions,queries}.ts, forms/*.schema.ts, hooks, ai/}`. Each module exposes `index.ts` (public surface) and `nav.ts` (navigation manifest aggregated by `features/registry.ts`).
- Server Components by default; mutations are Server Functions that re-check the session and validate with Zod; reads happen server-side through one HTTP client (`shared/api/http.ts`) that forwards the cookie and maps ProblemDetails.
- `proxy.ts` sets a nonce-based Content Security Policy with `strict-dynamic`, so every page is dynamically rendered and Cache Components stay off. `/api/*` and `/openapi/*` are rewritten to `API_INTERNAL_URL` by `proxy.ts` at request time (never by `next.config.ts`, which is frozen at build time) so the browser always talks to the web origin and one image serves every environment.
- Theme: `next-themes` (class strategy, system default) with the CSP nonce; Tailwind `@custom-variant dark`.
- Root route redirects to `/login`; authenticated users land on `/dashboard` once authentication exists.
- Import boundaries are enforced by `scripts/checks/feature-boundaries.ts`.

## Consequences

- The UI structure is discoverable from a URL: route path == feature folder == API route == Playwright spec path.
- A generated API client (Kiota vs openapi-typescript) and the data/forms libraries (TanStack Query/Table, react-hook-form) are introduced in the web foundation phase with their own ADR.
- Cache Components / partial prerendering are unavailable while the nonce CSP is in place; acceptable for an authenticated, data-driven ERP.
