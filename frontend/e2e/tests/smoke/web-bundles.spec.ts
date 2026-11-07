import type { APIRequestContext } from "@playwright/test";

import { retiredJob, runKeyPrefix, seedJobRun } from "../../fixtures/job-runs";
import { ownPersonRecordId } from "../../fixtures/person-records";
import { signIn, type SignedInPerson } from "../../fixtures/sign-in";
import { expect, test } from "../../fixtures/test";
import { personPath } from "../../pages/identity/users/person.page";
import { jobRunPath } from "../../pages/platform/jobs/runs.page";

const chunkPath = /\/_next\/static\/chunks\/[^"'\s<>\\]+\.js/g;

type WebRoute = {
  readonly path: string;
  readonly address?: (request: APIRequestContext, administrator: SignedInPerson) => Promise<string>;
  readonly ownMarkers: readonly string[];
  readonly foreignMarkers: readonly string[];
};

const departmentsMarker = "departments-add";

const designationsMarker = "designations-add";

const orgChartMarker = "org-chart-expand-all";

const organisationMarkers = [departmentsMarker, designationsMarker, orgChartMarker];

const jobRunsMarker = "No runs match these filters.";

const runActionsMarker = "job-run-rerun";

const referenceListMarker = "reference-list-no-matches";

const financialYearChooserMarker = "financial-year-chooser";

const exchangeRatesMarker = "No exchange rates match these filters.";

const exchangeRateImportMarker = "exchange-rate-template";

const referencePickersMarker = "reference-pickers-form";

const referenceDataMarkers = [
  referenceListMarker,
  financialYearChooserMarker,
  exchangeRatesMarker,
  exchangeRateImportMarker,
  referencePickersMarker,
];

const otherModuleMarkers = [
  "supplier-example-form",
  "motion-demo-preference",
  "upload-progress",
  "system-info-refresh",
  "audit-export",
  "No people match these filters.",
  ...organisationMarkers,
  jobRunsMarker,
];

function otherReferenceDataMarkers(own: string): string[] {
  return referenceDataMarkers.filter((marker) => marker !== own);
}

const routes: readonly WebRoute[] = [
  {
    path: "/design/form-kit",
    ownMarkers: ["supplier-example-form"],
    foreignMarkers: [
      ...referenceDataMarkers,
      "motion-demo-preference",
      "overlay-hover-card",
      "upload-progress",
      "system-info-refresh",
      "No bills match these filters.",
      "animated-list-item",
      "audit-export",
      "No people match these filters.",
      ...organisationMarkers,
      jobRunsMarker,
    ],
  },
  {
    path: "/design/data-table",
    ownMarkers: ["No bills match these filters."],
    foreignMarkers: [
      ...referenceDataMarkers,
      "supplier-example-form",
      "motion-demo-preference",
      "upload-progress",
      "system-info-refresh",
      "animated-list-item",
      "audit-export",
      "No people match these filters.",
      ...organisationMarkers,
      jobRunsMarker,
    ],
  },
  {
    path: "/design/feedback",
    ownMarkers: ["animated-list-item", "Enter a reminder.", "Restore the requests"],
    foreignMarkers: [
      ...referenceDataMarkers,
      "supplier-example-form",
      "motion-demo-preference",
      "upload-progress",
      "system-info-refresh",
      "No bills match these filters.",
      "audit-export",
      "No people match these filters.",
      ...organisationMarkers,
      jobRunsMarker,
    ],
  },
  {
    path: "/design/kitchen-sink",
    ownMarkers: ["motion-demo-preference"],
    foreignMarkers: [
      ...referenceDataMarkers,
      "supplier-example-form",
      "upload-progress",
      "system-info-refresh",
      "No bills match these filters.",
      "animated-list-item",
      "audit-export",
      "No people match these filters.",
      ...organisationMarkers,
      jobRunsMarker,
    ],
  },
  {
    path: "/platform/system-info",
    ownMarkers: ["system-info-refresh"],
    foreignMarkers: [
      ...referenceDataMarkers,
      "upload-progress",
      "Delete this attachment?",
      "supplier-example-form",
      "animated-list-item",
      "audit-export",
      "No people match these filters.",
      ...organisationMarkers,
      jobRunsMarker,
    ],
  },
  {
    path: "/platform/attachments",
    ownMarkers: ["upload-progress", "No files match these filters."],
    foreignMarkers: [
      ...referenceDataMarkers,
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "No bills match these filters.",
      "animated-list-item",
      "audit-export",
      "No people match these filters.",
      ...organisationMarkers,
      jobRunsMarker,
    ],
  },
  {
    path: "/platform/audit",
    ownMarkers: ["audit-export"],
    foreignMarkers: [
      ...referenceDataMarkers,
      "upload-progress",
      "Delete this attachment?",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "No bills match these filters.",
      "animated-list-item",
      "No people match these filters.",
      ...organisationMarkers,
      jobRunsMarker,
    ],
  },
  {
    path: "/platform/jobs",
    ownMarkers: [jobRunsMarker, runActionsMarker],
    foreignMarkers: [
      ...referenceDataMarkers,
      "upload-progress",
      "Delete this attachment?",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "No bills match these filters.",
      "animated-list-item",
      "audit-export",
      "No people match these filters.",
      ...organisationMarkers,
    ],
  },
  {
    path: "/platform/jobs/runs/[runId]",
    address: async (request) =>
      jobRunPath(
        await seedJobRun(request, {
          jobName: retiredJob,
          idempotencyKey: `${runKeyPrefix("bundles")}-failed`,
          status: "failed",
        }),
      ),
    ownMarkers: [runActionsMarker],
    foreignMarkers: [
      ...referenceDataMarkers,
      jobRunsMarker,
      "audit-export",
      "No people match these filters.",
      "upload-progress",
      "Delete this attachment?",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "No bills match these filters.",
      "animated-list-item",
      ...organisationMarkers,
    ],
  },
  {
    path: "/identity/users",
    ownMarkers: ["No people match these filters."],
    foreignMarkers: [
      ...referenceDataMarkers,
      "upload-progress",
      "Delete this attachment?",
      "No files match these filters.",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "No bills match these filters.",
      "animated-list-item",
      "audit-export",
      ...organisationMarkers,
      jobRunsMarker,
    ],
  },
  {
    path: "/identity/users/[userId]",
    address: async (request, administrator) => personPath(await ownPersonRecordId(request, administrator)),
    ownMarkers: ["record-move", "assign-roles"],
    foreignMarkers: [
      ...referenceDataMarkers,
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
      ...organisationMarkers,
      jobRunsMarker,
    ],
  },
  {
    path: "/identity/users/departments",
    ownMarkers: [departmentsMarker],
    foreignMarkers: [
      ...referenceDataMarkers,
      designationsMarker,
      orgChartMarker,
      "record-move",
      "audit-export",
      "No people match these filters.",
      "upload-progress",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "animated-list-item",
      jobRunsMarker,
    ],
  },
  {
    path: "/identity/users/designations",
    ownMarkers: [designationsMarker],
    foreignMarkers: [
      ...referenceDataMarkers,
      departmentsMarker,
      orgChartMarker,
      "record-move",
      "audit-export",
      "No people match these filters.",
      "upload-progress",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "animated-list-item",
      jobRunsMarker,
    ],
  },
  {
    path: "/identity/users/org-chart",
    ownMarkers: [orgChartMarker],
    foreignMarkers: [
      ...referenceDataMarkers,
      departmentsMarker,
      designationsMarker,
      "record-move",
      "audit-export",
      "No people match these filters.",
      "upload-progress",
      "system-info-refresh",
      "supplier-example-form",
      "motion-demo-preference",
      "animated-list-item",
      jobRunsMarker,
    ],
  },
  {
    path: "/platform/reference-data",
    ownMarkers: [referenceListMarker],
    foreignMarkers: [...otherReferenceDataMarkers(referenceListMarker), ...otherModuleMarkers],
  },
  {
    path: "/platform/reference-data/financial-years",
    ownMarkers: [financialYearChooserMarker],
    foreignMarkers: [...otherReferenceDataMarkers(financialYearChooserMarker), ...otherModuleMarkers],
  },
  {
    path: "/platform/reference-data/exchange-rates",
    ownMarkers: [exchangeRatesMarker],
    foreignMarkers: [...otherReferenceDataMarkers(exchangeRatesMarker), ...otherModuleMarkers],
  },
  {
    path: "/platform/reference-data/exchange-rates/import",
    ownMarkers: [exchangeRateImportMarker],
    foreignMarkers: [...otherReferenceDataMarkers(exchangeRateImportMarker), ...otherModuleMarkers],
  },
  {
    path: "/design/reference-pickers",
    ownMarkers: [referencePickersMarker],
    foreignMarkers: [...otherReferenceDataMarkers(referencePickersMarker), ...otherModuleMarkers],
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
