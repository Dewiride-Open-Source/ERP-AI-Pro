import type { APIRequestContext } from "@playwright/test";

import { expect, test } from "../../fixtures/test";

const chunkPath = /\/_next\/static\/chunks\/[^"'\s<>\\]+\.js/g;

const routes = [
  {
    path: "/design/form-kit",
    ownMarkers: ["supplier-example-form"],
    foreignMarkers: [
      "motion-demo-preference",
      "overlay-hover-card",
      "upload-progress",
      "system-info-refresh",
      "No bills match these filters.",
      "animated-list-item",
    ],
  },
  {
    path: "/design/data-table",
    ownMarkers: ["No bills match these filters."],
    foreignMarkers: [
      "supplier-example-form",
      "motion-demo-preference",
      "upload-progress",
      "system-info-refresh",
      "animated-list-item",
    ],
  },
  {
    path: "/design/feedback",
    ownMarkers: ["animated-list-item", "Enter a reminder.", "Restore the requests"],
    foreignMarkers: [
      "supplier-example-form",
      "motion-demo-preference",
      "upload-progress",
      "system-info-refresh",
      "No bills match these filters.",
    ],
  },
  {
    path: "/design/kitchen-sink",
    ownMarkers: ["motion-demo-preference"],
    foreignMarkers: [
      "supplier-example-form",
      "upload-progress",
      "system-info-refresh",
      "No bills match these filters.",
      "animated-list-item",
    ],
  },
  {
    path: "/platform/system-info",
    ownMarkers: ["system-info-refresh"],
    foreignMarkers: [
      "upload-progress",
      "Delete this attachment?",
      "supplier-example-form",
      "animated-list-item",
    ],
  },
  {
    path: "/platform/attachments",
    ownMarkers: ["upload-progress", "No files match these filters."],
    foreignMarkers: [
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "No bills match these filters.",
      "animated-list-item",
    ],
  },
] as const;

// The scripts are read from the page's own HTML, never from the responses a browser loads, because a browser also prefetches
// the chunks of the pages its navigation links to.
async function scriptsOf(request: APIRequestContext, path: string): Promise<string> {
  const page = await request.get(path);
  expect(page.status(), `${path} status`).toBe(200);
  const chunks = [...new Set((await page.text()).match(chunkPath) ?? [])];
  expect(chunks.length, `scripts referenced by ${path}`).toBeGreaterThan(0);
  const bodies = await Promise.all(
    chunks.map(async (chunk) => {
      const script = await request.get(chunk);
      expect(script.status(), `${chunk} status`).toBe(200);
      return script.text();
    }),
  );
  return bodies.join("\n");
}

test.describe("web bundles", () => {
  for (const { path, ownMarkers, foreignMarkers } of routes) {
    test(`${path} ships its own client components and no other module's`, async ({ request }) => {
      const scripts = await scriptsOf(request, path);

      for (const marker of ownMarkers) expect(scripts, `${path} carries ${marker}`).toContain(`"${marker}"`);
      for (const marker of foreignMarkers) {
        expect(scripts, `${path} leaves out ${marker}`).not.toContain(`"${marker}"`);
      }
    });
  }
});
