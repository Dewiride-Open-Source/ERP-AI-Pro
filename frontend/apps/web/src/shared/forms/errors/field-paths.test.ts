import assert from "node:assert/strict";
import { test } from "node:test";

import { apiKeyToFormPath, camelCaseMemberName, issuePath, issuesToFieldErrors } from "./field-paths.ts";

const capitalEAcute = String.fromCharCode(0xc9);
const smallEAcute = String.fromCharCode(0xe9);
const capitalIWithDot = String.fromCharCode(0x130);
const capitalSigma = String.fromCharCode(0x3a3);
const smallSigma = String.fromCharCode(0x3c3);
const capitalAlpha = String.fromCharCode(0x391);
const smallAlpha = String.fromCharCode(0x3b1);
const deseretCapitalLongI = String.fromCharCode(0xd801, 0xdc00);

test("issuePath_NamesAndIndices_JoinsThemWithDots", () => {
  assert.equal(issuePath(["lines", 0, "amount"]), "lines.0.amount");
  assert.equal(issuePath(["gstin"]), "gstin");
});

test("issuePath_SymbolSegments_AreSkipped", () => {
  assert.equal(issuePath([Symbol("meta"), "validity", "to"]), "validity.to");
  assert.equal(issuePath([Symbol("meta")]), "");
});

test("issuePath_NoSegments_IsTheWholeForm", () => {
  assert.equal(issuePath([]), "");
});

test("issuesToFieldErrors_IssuesWithPaths_GroupsTheMessagesPerPathInOrder", () => {
  const errors = issuesToFieldErrors([
    { path: ["gstin"], message: "Enter the GSTIN." },
    { path: ["lines", 0, "amount"], message: "Enter an amount." },
    { path: ["gstin"], message: "The GSTIN does not match the PAN." },
  ]);

  assert.deepEqual(errors, {
    fieldErrors: {
      gstin: ["Enter the GSTIN.", "The GSTIN does not match the PAN."],
      "lines.0.amount": ["Enter an amount."],
    },
    formErrors: [],
  });
});

test("issuesToFieldErrors_IssueWithoutAFieldPath_IsAFormError", () => {
  const errors = issuesToFieldErrors([
    { path: [], message: "Add at least one line." },
    { path: [Symbol("meta")], message: "The supplier is blocked." },
  ]);

  assert.deepEqual(errors, {
    fieldErrors: {},
    formErrors: ["Add at least one line.", "The supplier is blocked."],
  });
});

test("issuesToFieldErrors_SameMessageTwiceOnOnePath_IsKeptOnce", () => {
  const errors = issuesToFieldErrors([
    { path: ["pan"], message: "Enter the PAN." },
    { path: ["pan"], message: "Enter the PAN." },
    { path: [], message: "Try again." },
    { path: [], message: "Try again." },
  ]);

  assert.deepEqual(errors, { fieldErrors: { pan: ["Enter the PAN."] }, formErrors: ["Try again."] });
});

test("issuesToFieldErrors_PathNamedLikeThePrototype_IsAnOwnKey", () => {
  const errors = issuesToFieldErrors([{ path: ["__proto__"], message: "Refused." }]);

  assert.ok(Object.hasOwn(errors.fieldErrors, "__proto__"));
  assert.equal(Object.getPrototypeOf(errors.fieldErrors), Object.prototype);
});

test("camelCaseMemberName_NamesConvertedByDotNet_MatchJsonNamingPolicyCamelCase", () => {
  const table: readonly (readonly [string, string])[] = [
    ["", ""],
    ["a", "a"],
    ["A", "a"],
    ["AB", "ab"],
    ["ABC", "abc"],
    ["Ab", "ab"],
    ["aB", "aB"],
    ["GSTIN", "gstin"],
    ["IFSCCode", "ifscCode"],
    ["PANNumber", "panNumber"],
    ["URLValue", "urlValue"],
    ["ShippingAddress", "shippingAddress"],
    ["Id", "id"],
    ["ID", "id"],
    ["IDs", "iDs"],
    ["IOStream", "ioStream"],
    ["A1", "a1"],
    ["AB1", "aB1"],
    ["ABc", "aBc"],
    ["ABCDe", "abcDe"],
    ["A B", "a B"],
    ["AB C", "ab C"],
    ["ABC D", "abc D"],
    ["Ab Cd", "ab Cd"],
    ["A  B", "a  B"],
    ["A_B", "a_B"],
    ["AB_C", "aB_C"],
    ["_Name", "_Name"],
    ["0", "0"],
    ["10", "10"],
    ["XMLHttpRequest", "xmlHttpRequest"],
    [`${capitalEAcute}COLE`, `${smallEAcute}cole`],
    [`${capitalEAcute}lan`, `${smallEAcute}lan`],
    [`${capitalIWithDot}X`, `${capitalIWithDot}x`],
    [capitalIWithDot, capitalIWithDot],
    [`${capitalSigma}${capitalAlpha}`, `${smallSigma}${smallAlpha}`],
    [`${deseretCapitalLongI}X`, `${deseretCapitalLongI}X`],
  ];

  for (const [name, expected] of table) {
    assert.equal(camelCaseMemberName(name), expected, JSON.stringify(name));
  }
});

test("apiKeyToFormPath_WireKey_IsTheFormPathOfTheSameMember", () => {
  const table: readonly (readonly [string, string])[] = [
    ["take", "take"],
    ["gstin", "gstin"],
    ["orderItems[0].description", "orderItems.0.description"],
    ["customer.shippingAddress.street", "customer.shippingAddress.street"],
    ["lines[12].taxLines[3].rate", "lines.12.taxLines.3.rate"],
    ["matrix[0][1]", "matrix.0.1"],
    ["lines.0.amount", "lines.0.amount"],
  ];

  for (const [key, expected] of table) {
    assert.equal(apiKeyToFormPath(key), expected, key);
  }
});

test("apiKeyToFormPath_PascalCaseSegments_AreConvertedLikeTheApiConvertsThem", () => {
  const table: readonly (readonly [string, string])[] = [
    ["customer.ShippingAddress.Street", "customer.shippingAddress.street"],
    ["OrderItems[0].Description", "orderItems.0.description"],
    ["GSTIN", "gstin"],
    ["BankAccount.IFSCCode", "bankAccount.ifscCode"],
    ["Take", "take"],
  ];

  for (const [key, expected] of table) {
    assert.equal(apiKeyToFormPath(key), expected, key);
  }
});

test("apiKeyToFormPath_RequestAsAWhole_HasNoFieldPath", () => {
  assert.equal(apiKeyToFormPath(""), undefined);
});

test("apiKeyToFormPath_KeyOutsideTheGrammar_HasNoFieldPath", () => {
  for (const key of [".a", "a.", "a..b", "a[", "a[x]", "a[01]", "a[-1]", "[0].a", "a]b", "a[0]b", "a[0]."]) {
    assert.equal(apiKeyToFormPath(key), undefined, key);
  }
});

test("apiKeyToFormPath_SegmentReservedByTheFormLibrary_HasNoFieldPath", () => {
  for (const key of ["type", "Message", "address.root", "lines[0].ref", "form", "errors.types"]) {
    assert.equal(apiKeyToFormPath(key), undefined, key);
  }
});

test("apiKeyToFormPath_AliasOfTheWholePath_ReplacesIt", () => {
  assert.equal(apiKeyToFormPath("bankAccount.Ifsc", { "bankAccount.ifsc": "ifsc" }), "ifsc");
});

test("apiKeyToFormPath_AliasOfAPrefix_KeepsTheRestOfThePath", () => {
  const aliases = { lines: "items" };

  assert.equal(apiKeyToFormPath("lines[2].amount", aliases), "items.2.amount");
  assert.equal(apiKeyToFormPath("lines", aliases), "items");
  assert.equal(apiKeyToFormPath("linesTotal", aliases), "linesTotal");
});

test("apiKeyToFormPath_SeveralMatchingAliases_UsesTheLongest", () => {
  const aliases = { bankAccount: "bank", "bankAccount.ifsc": "ifsc" };

  assert.equal(apiKeyToFormPath("bankAccount.ifsc", aliases), "ifsc");
  assert.equal(apiKeyToFormPath("bankAccount.number", aliases), "bank.number");
});

test("apiKeyToFormPath_AliasWrittenInAnotherSpellingOfTheGrammar_StillMatches", () => {
  assert.equal(apiKeyToFormPath("bankAccount.ifscCode", { "BankAccount.IFSCCode": "ifsc" }), "ifsc");
  assert.equal(apiKeyToFormPath("lines[0].amount", { "lines[0].amount": "firstAmount" }), "firstAmount");
});

test("apiKeyToFormPath_AliasToTheWholeForm_HasNoFieldPath", () => {
  assert.equal(apiKeyToFormPath("legacyCode", { legacyCode: "" }), undefined);
});

test("apiKeyToFormPath_AliasAwayFromAReservedName_IsThatAlias", () => {
  assert.equal(apiKeyToFormPath("type", { type: "supplierType" }), "supplierType");
});
