import type { APIRequestContext } from "@playwright/test";

import { ownPersonRecordId } from "../../fixtures/person-records";
import { signIn, type SignedInPerson } from "../../fixtures/sign-in";
import { expect, test } from "../../fixtures/test";
import { personPath } from "../../pages/identity/users/person.page";

const chunkPath = /\/_next\/static\/chunks\/[^"'\s<>\\]+\.js/g;

type WebRoute = {
  readonly path: string;
  readonly address?: (request: APIRequestContext, administrator: SignedInPerson) => Promise<string>;
  readonly ownMarkers: readonly string[];
  readonly foreignMarkers: readonly string[];
};

const routes: readonly WebRoute[] = [
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
      "audit-export",
      "No people match these filters.",
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
      "audit-export",
      "No people match these filters.",
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
      "audit-export",
      "No people match these filters.",
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
      "audit-export",
      "No people match these filters.",
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
      "audit-export",
      "No people match these filters.",
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
      "audit-export",
      "No people match these filters.",
    ],
  },
  {
    path: "/platform/audit",
    ownMarkers: ["audit-export"],
    foreignMarkers: [
      "upload-progress",
      "Delete this attachment?",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "No bills match these filters.",
      "animated-list-item",
      "No people match these filters.",
    ],
  },
  {
    path: "/identity/users",
    ownMarkers: ["No people match these filters."],
    foreignMarkers: [
      "upload-progress",
      "Delete this attachment?",
      "No files match these filters.",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "No bills match these filters.",
      "animated-list-item",
      "audit-export",
    ],
  },
  {
    path: "/identity/users/[userId]",
    address: async (request, administrator) => personPath(await ownPersonRecordId(request, administrator)),
    ownMarkers: ["record-move", "assign-roles"],
    foreignMarkers: [
      "audit-export",
      "No people match these filters.",
      "upload-progress",
      "Delete this attachment?",
      "No files match these filters.",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "No bills match these filters.",
      "animated-list-item",
    ],
  },
];

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
  for (const { path, address, ownMarkers, foreignMarkers } of routes) {
    test(`${path} ships its own client components and no other module's`, async ({ request }) => {
      const { person } = await signIn(request, "administrator");
      const scripts = await scriptsOf(request, address === undefined ? path : await address(request, person));

      for (const marker of ownMarkers) expect(scripts, `${path} carries ${marker}`).toContain(`"${marker}"`);
      for (const marker of foreignMarkers) {
        expect(scripts, `${path} leaves out ${marker}`).not.toContain(`"${marker}"`);
      }
    });
  }
});
