import assert from "node:assert/strict";
import { test } from "node:test";

import { ApiError } from "@/shared/api/problem-details";

import { exportFailureMessage, exportPath, exportRefusal, maxExportLines } from "./change-export.ts";

test("exportPath_NoFilter_ExportsEveryChange", () => {
  assert.equal(exportPath(undefined), "/platform/audit/changes/export");
});

test("exportPath_TheListsFilter_IsSentAsOneEncodedParameter", () => {
  const filter =
    "entityType:in:identity.users.people|identity.users.positions;occurredAt:gte:2026-09-30T18%3A30%3A00.000Z";

  const path = exportPath(filter);

  assert.equal(
    path,
    "/platform/audit/changes/export?filter=entityType%3Ain%3Aidentity.users.people%7Cidentity.users.positions%3BoccurredAt%3Agte%3A2026-09-30T18%253A30%253A00.000Z",
  );
  assert.equal(new URL(path, "https://erp.example").searchParams.get("filter"), filter);
});

test("exportRefusal_UpToTheCap_OffersTheDownload", () => {
  assert.equal(exportRefusal(0), undefined);
  assert.equal(exportRefusal(maxExportLines), undefined);
});

test("exportRefusal_MoreChangesThanTheCap_SaysWhyTheDownloadIsNotOffered", () => {
  assert.equal(
    exportRefusal(maxExportLines + 1),
    "More than 1,00,000 changes match these filters, too many for one download. Narrow the filters, for example to fewer days.",
  );
});

test("exportFailureMessage_TooManyLines_SaysSoWithTheCountGroupedTheIndianWay", () => {
  const tooLarge = new ApiError({
    status: 422,
    code: "audit.export-too-large",
    title: "The request could not be completed.",
    detail: "The export would hold more than 100,000 lines; narrow the filter, for example to fewer days.",
  });

  assert.equal(
    exportFailureMessage(tooLarge),
    "These changes hold more than 1,00,000 values, too many for one download. Narrow the filters, for example to fewer days.",
  );
});

test("exportFailureMessage_AnyOtherRefusal_IsTheApisOwnExplanation", () => {
  const invalid = new ApiError({
    status: 400,
    code: "request.invalid",
    title: "The request is not valid.",
    detail: "The filter names a field the export does not allow.",
  });

  assert.equal(exportFailureMessage(invalid), "The filter names a field the export does not allow.");
});

test("exportFailureMessage_TooManyExportsAtOnce_AsksToTryAgainLater", () => {
  const busy = new ApiError({ status: 429, code: "rate-limit.exceeded", title: "Too Many Requests" });

  assert.equal(
    exportFailureMessage(busy),
    "Other downloads of the history are being prepared. Try again in a minute.",
  );
});

test("exportFailureMessage_AnUnreachableServer_IsTheTitleOfItsError", () => {
  const unreachable = new ApiError({ status: 0, title: "The download could not reach the server." });

  assert.equal(exportFailureMessage(unreachable), "The download could not reach the server.");
});

test("exportFailureMessage_AnyOtherFailure_AsksToTryAgain", () => {
  assert.equal(exportFailureMessage(new TypeError("failed")), "The download did not finish. Try again.");
});
