import assert from "node:assert/strict";
import { test } from "node:test";

import {
  defaultSupplierExampleValues,
  gstinPanMismatch,
  legalNameMaxLength,
  supplierExampleFieldAliases,
  supplierExampleSchema,
  type SupplierExampleValues,
} from "./supplier-example.schema.ts";

const complete: SupplierExampleValues = {
  legalName: "  Globex Cloud Services Private Limited ",
  gstin: "29 aabcg 1234k 1z5",
  pan: "aabcg1234k",
  ifsc: "hdfc0001234",
  state: "karnataka",
  category: "cloud-hosting",
  openingBalance: "₹ 1,25,000.5",
  agreementStart: "2026-04-01",
  validity: { from: "2026-04-01", to: "2027-03-31" },
  agreementDocumentId: "0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e",
  serverAnswer: "accept",
};

function reported(values: unknown): string[] {
  const result = supplierExampleSchema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`);
}

test("supplierExampleSchema_DefaultValues_AsksForEveryRequiredFieldOnce", () => {
  assert.deepEqual(reported(defaultSupplierExampleValues), [
    "legalName: Enter the supplier's legal name.",
    "gstin: Enter the GSTIN.",
    "pan: Enter the PAN.",
    "ifsc: Enter the IFSC.",
    "state: Choose the state of the registered office.",
    "category: Choose a category from the list.",
    "agreementStart: Enter the date the agreement starts.",
    "validity.from: Enter the start date.",
    "validity.to: Enter the end date.",
  ]);
});

test("supplierExampleSchema_CompleteValues_ParsesTheCanonicalSupplier", () => {
  assert.deepEqual(supplierExampleSchema.parse(complete), {
    legalName: "Globex Cloud Services Private Limited",
    gstin: "29AABCG1234K1Z5",
    pan: "AABCG1234K",
    ifsc: "HDFC0001234",
    state: "karnataka",
    category: "cloud-hosting",
    openingBalance: "125000.5",
    agreementStart: "2026-04-01",
    validity: { from: "2026-04-01", to: "2027-03-31" },
    agreementDocumentId: "0199a3f2-7c1e-7b4a-9d2e-3f5a6b7c8d9e",
    serverAnswer: "accept",
  });
});

test("supplierExampleSchema_OptionalFieldsLeftEmpty_AreAbsent", () => {
  const parsed = supplierExampleSchema.parse({ ...complete, openingBalance: " ", agreementDocumentId: "" });

  assert.equal(parsed.openingBalance, undefined);
  assert.equal(parsed.agreementDocumentId, undefined);
});

const refusals: readonly {
  readonly change: Partial<SupplierExampleValues>;
  readonly expected: string;
}[] = [
  {
    change: { legalName: "G".repeat(legalNameMaxLength + 1) },
    expected: `legalName: Use ${legalNameMaxLength} characters or fewer.`,
  },
  {
    change: { gstin: "29AABCG1234K1Z" },
    expected: "gstin: Enter a 15-character GSTIN: two digits, then 13 letters or digits.",
  },
  {
    change: { pan: "AABC1234K" },
    expected: "pan: Enter a 10-character PAN: five letters, four digits, then a letter.",
  },
  {
    change: { ifsc: "HDFC1001234" },
    expected: "ifsc: Enter an 11-character IFSC: four letters, a zero, then six letters or digits.",
  },
  { change: { state: "goa" }, expected: "state: Choose the state of the registered office." },
  { change: { category: "imports" }, expected: "category: Choose a category from the list." },
  {
    change: { openingBalance: "100000000.01" },
    expected: "openingBalance: Enter an amount of at most ₹10,00,00,000.00.",
  },
  { change: { openingBalance: "-5" }, expected: "openingBalance: Enter an amount of zero or more." },
  {
    change: { agreementStart: "2020-03-31" },
    expected: "agreementStart: Enter a date on or after 01-04-2020.",
  },
  {
    change: { agreementStart: "2035-04-01" },
    expected: "agreementStart: Enter a date on or before 31-03-2035.",
  },
  {
    change: { agreementStart: "31-02-2026" },
    expected: "agreementStart: Enter a real date as day-month-year, for example 31-03-2026.",
  },
  {
    change: { validity: { from: "2027-03-31", to: "2026-04-01" } },
    expected: "validity.to: The end date must be on or after the start date.",
  },
  {
    change: { agreementDocumentId: "agreement.pdf" },
    expected: "agreementDocumentId: Upload the agreement document again.",
  },
  { change: { serverAnswer: "teapot" }, expected: "serverAnswer: Choose how the server answers." },
];

test("supplierExampleSchema_OneFieldRefused_ReportsOnlyThatField", () => {
  for (const { change, expected } of refusals) {
    assert.deepEqual(reported({ ...complete, ...change }), [expected], JSON.stringify(change));
  }
});

test("supplierExampleSchema_PanOutsideTheGstin_ReportsOneIssueWithoutAPath", () => {
  assert.deepEqual(reported({ ...complete, pan: "AABCH1234K" }), [`: ${gstinPanMismatch}`]);
  assert.deepEqual(reported({ ...complete, gstin: "29AABCH1234K1Z5" }), [`: ${gstinPanMismatch}`]);
});

test("supplierExampleSchema_GstinOrPanRefusedOnItsOwn_LeavesOutTheMatchCheck", () => {
  assert.deepEqual(reported({ ...complete, gstin: "", pan: "AABCH1234K" }), ["gstin: Enter the GSTIN."]);
  assert.deepEqual(reported({ ...complete, pan: "" }), ["pan: Enter the PAN."]);
  assert.deepEqual(reported({ ...complete, gstin: "29AABCG1234K1Z", pan: "AABCH1234K" }), [
    "gstin: Enter a 15-character GSTIN: two digits, then 13 letters or digits.",
  ]);
});

test("supplierExampleFieldAliases_EveryTarget_IsAFieldOfTheForm", () => {
  for (const [apiKey, formPath] of Object.entries(supplierExampleFieldAliases)) {
    assert.ok(Object.hasOwn(defaultSupplierExampleValues, formPath), `${apiKey} maps to ${formPath}`);
  }
});
