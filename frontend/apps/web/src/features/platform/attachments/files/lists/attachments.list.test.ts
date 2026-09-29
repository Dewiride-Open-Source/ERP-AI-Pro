import assert from "node:assert/strict";
import { test } from "node:test";

import { listApiParameters } from "@/shared/lists/list-api";
import { readListQuery } from "@/shared/lists/list-query";

import { attachmentsList } from "./attachments.list.ts";

test("attachmentsList_EveryFilterAndSort_MapsOntoTheApiListFields", () => {
  const { query, canonical } = readListQuery(
    {
      name: "invoice",
      type: ["image/png", "application/pdf"],
      uploadedFrom: "2026-09-01",
      uploadedTo: "2026-09-30",
      sort: "sizeBytes:asc",
      size: "50",
      page: "2",
      hide: "contentType,scanStatus",
    },
    attachmentsList,
  );

  assert.equal(canonical, true);
  assert.deepEqual(query.hiddenColumns, ["contentType", "scanStatus"]);
  assert.deepEqual(listApiParameters(query, attachmentsList), {
    page: 2,
    pageSize: 50,
    sort: "sizeBytes:asc",
    filter:
      "fileName:contains:invoice;contentType:in:application%2Fpdf|image%2Fpng;" +
      "createdAt:gte:2026-08-31T18%3A30%3A00.000Z;createdAt:lt:2026-09-30T18%3A30%3A00.000Z",
  });
});

test("attachmentsList_NoParameters_ListsTheNewestFilesFirst", () => {
  const { query } = readListQuery({}, attachmentsList);
  assert.deepEqual(listApiParameters(query, attachmentsList), {
    page: 1,
    pageSize: 20,
    sort: "createdAt:desc",
    filter: undefined,
  });
});
