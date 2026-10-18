import assert from "node:assert/strict";
import { test } from "node:test";

import type { ChangedValueResponse, ChangeRecordResponse } from "@dewiride/erp-api-client";

import {
  actorChangesLinkText,
  actorText,
  changeSentence,
  notSet,
  operationLabel,
  propertyLabel,
  recordHistoryLinkText,
  recordTypeLabel,
  shownValues,
  snapshotSummary,
  valueText,
} from "./change-text.ts";

const department = "identity.users.departments";

const people = "identity.users.people";

function change(values: Partial<ChangeRecordResponse>): ChangeRecordResponse {
  return {
    operation: "created",
    entityType: department,
    subjectType: department,
    subjectName: "Payroll operations",
    actorKind: "person",
    actorName: "Asha Rao",
    ...values,
  };
}

function value(values: Partial<ChangedValueResponse>): ChangedValueResponse {
  return { property: "name", kind: "text", before: null, after: null, reference: null, ...values };
}

test("recordTypeLabel_EveryChangeKeyTheHistoryRecords_HasAName", () => {
  assert.equal(recordTypeLabel(people), "Person");
  assert.equal(recordTypeLabel(department), "Department");
  assert.equal(recordTypeLabel("identity.users.designations"), "Designation");
  assert.equal(recordTypeLabel("identity.users.positions"), "Move");
  assert.equal(recordTypeLabel("identity.users.themes"), "Colour theme");
  assert.equal(recordTypeLabel("platform.attachments.files"), "File");
});

test("recordTypeLabel_AChangeKeyItDoesNotKnow_IsTheKeyItself", () => {
  assert.equal(recordTypeLabel("finance.sales.invoices"), "finance.sales.invoices");
  assert.equal(recordTypeLabel("constructor"), "constructor");
});

test("operationLabel_EachOperation_IsTheWordOfTheFilter", () => {
  assert.deepEqual((["created", "updated", "deleted", "restored"] as const).map(operationLabel), [
    "Created",
    "Changed",
    "Deleted",
    "Restored",
  ]);
});

test("actorText_EachKindOfActor_NamesWhoMadeTheChange", () => {
  assert.equal(actorText("person", "Asha Rao"), "Asha Rao");
  assert.equal(actorText("person", null), "Unknown person");
  assert.equal(actorText("person", ""), "Unknown person");
  assert.equal(actorText("system", "ignored"), "The ERP");
  assert.equal(actorText("anonymous", undefined), "Someone not signed in");
  assert.equal(actorText(undefined, undefined), "Unknown person");
});

test("actorChangesLinkText_EachKindOfActor_SaysWhoseChangesItShows", () => {
  assert.equal(actorChangesLinkText("person", "Asha Rao"), "Show changes by Asha Rao");
  assert.equal(actorChangesLinkText("person", null), "Show changes by this person");
  assert.equal(actorChangesLinkText("system", null), "Show changes by the ERP");
  assert.equal(actorChangesLinkText("anonymous", null), "Show changes made without signing in");
});

test("recordHistoryLinkText_NamedOrUnnamedRecord_SaysWhoseHistoryItShows", () => {
  assert.equal(recordHistoryLinkText("Payroll operations"), "Show the history of Payroll operations");
  assert.equal(recordHistoryLinkText(null), "Show the history of this record");
});

test("changeSentence_ARecordFiledUnderItself_NamesItWithTheVerbOfTheOperation", () => {
  assert.deepEqual(changeSentence(change({})), {
    actor: "Asha Rao",
    action: "created the department",
    subject: "Payroll operations",
  });
  assert.deepEqual(changeSentence(change({ operation: "updated", actorKind: "system" })), {
    actor: "The ERP",
    action: "changed the department",
    subject: "Payroll operations",
  });
  assert.deepEqual(changeSentence(change({ operation: "deleted" })).action, "deleted the department");
  assert.deepEqual(changeSentence(change({ operation: "restored" })).action, "restored the department");
});

test("changeSentence_ARecordWithoutAName_SaysWhichKindOfRecordItWas", () => {
  assert.deepEqual(changeSentence(change({ subjectName: null })), {
    actor: "Asha Rao",
    action: "created a department",
    subject: undefined,
  });
});

test("changeSentence_AFile_IsUploadedRatherThanCreated", () => {
  const files = "platform.attachments.files";
  const sentence = changeSentence(change({ entityType: files, subjectType: files, subjectName: "q2.pdf" }));

  assert.equal(sentence.action, "uploaded the file");
  assert.equal(
    changeSentence(change({ entityType: files, subjectType: files, operation: "deleted" })).action,
    "deleted the file",
  );
});

test("changeSentence_ARecordFiledUnderAnother_NamesTheRecordItIsFiledUnder", () => {
  const move = change({
    entityType: "identity.users.positions",
    subjectType: people,
    subjectName: "Vikram Iyer",
  });

  assert.deepEqual(changeSentence(move), {
    actor: "Asha Rao",
    action: "created a move of",
    subject: "Vikram Iyer",
  });
  assert.deepEqual(changeSentence({ ...move, operation: "deleted", subjectName: null }), {
    actor: "Asha Rao",
    action: "deleted a move of a person",
    subject: undefined,
  });
});

test("changeSentence_ARecordItsSubjectHoldsOneOf_IsTheOneOfThatSubject", () => {
  const theme = change({
    entityType: "identity.users.themes",
    subjectType: people,
    subjectName: "Vikram Iyer",
  });

  assert.deepEqual(changeSentence(theme), {
    actor: "Asha Rao",
    action: "set the colour theme of",
    subject: "Vikram Iyer",
  });
  assert.deepEqual(changeSentence({ ...theme, operation: "updated", subjectName: null }), {
    actor: "Asha Rao",
    action: "changed the colour theme of a person",
    subject: undefined,
  });
});

test("changeSentence_AKindOfRecordItDoesNotKnow_UsesTheChangeKey", () => {
  const key = "inventory.stock.items";
  const sentence = changeSentence(change({ entityType: key, subjectType: key, subjectName: null }));

  assert.equal(sentence.action, "created an inventory.stock.items");
});

test("valueText_EachKind_ReadsTheValueAsItsKindMeansIt", () => {
  assert.equal(valueText("text", "true"), "true");
  assert.equal(valueText("number", "12.5"), "12.5");
  assert.equal(valueText("boolean", "true"), "Yes");
  assert.equal(valueText("boolean", "false"), "No");
  assert.equal(valueText("date", "2026-04-01"), "01-04-2026");
  assert.equal(valueText("instant", "2026-10-08T09:30:00.0000000+00:00"), "8 Oct 2026, 3:00 pm");
  assert.equal(valueText("enum", "notScanned"), "Not scanned");
  assert.equal(valueText("enum", "active"), "Active");
  assert.equal(valueText("enum", "7"), "7");
  assert.equal(valueText("list", '["finance","hr"]'), "finance, hr");
  assert.equal(valueText("list", "[]"), "None");
  assert.equal(valueText("list", "[3,true,null]"), `3, true, ${notSet}`);
  assert.equal(
    valueText("id", "0192f3c4-5b6a-7d8e-9f01-23456789abcd"),
    "0192f3c4-5b6a-7d8e-9f01-23456789abcd",
  );
  assert.equal(valueText("binary", "AQID"), "Binary data, 3 bytes");
  assert.equal(valueText("binary", "AQ=="), "Binary data, 1 byte");
});

test("valueText_AReference_ShowsTheNameOfTheRecordItPointsAt", () => {
  const id = "0192f3c4-5b6a-7d8e-9f01-23456789abcd";

  assert.equal(valueText("reference", id, "Payroll operations"), "Payroll operations");
  assert.equal(valueText("reference", id, null), id);
});

test("valueText_AnEmptyValue_IsNotSetWhateverItsKind", () => {
  for (const kind of [
    "text",
    "number",
    "boolean",
    "date",
    "instant",
    "enum",
    "list",
    "reference",
    "id",
  ] as const) {
    assert.equal(valueText(kind, null), notSet, kind);
    assert.equal(valueText(kind, undefined), notSet, kind);
    assert.equal(valueText(kind, ""), notSet, kind);
  }
});

test("valueText_AValueItsKindCannotRead_IsShownAsRecorded", () => {
  assert.equal(valueText("instant", "not a time"), "not a time");
  assert.equal(valueText("list", "not json"), "not json");
  assert.equal(valueText("list", '{"a":1}'), '{"a":1}');
  assert.equal(valueText("boolean", "maybe"), "maybe");
  assert.equal(valueText("date", "16-10-2026"), "16-10-2026");
});

test("propertyLabel_ACamelCasePath_ReadsAsWords", () => {
  assert.equal(propertyLabel("displayName"), "Display name");
  assert.equal(propertyLabel("entraObjectId"), "Entra object id");
  assert.equal(propertyLabel("price.amount"), "Price amount");
  assert.equal(propertyLabel("fileURL"), "File url");
  assert.equal(propertyLabel("addressLine2"), "Address line 2");
  assert.equal(propertyLabel("status"), "Status");
});

test("propertyLabel_AReference_DropsTheIdOfItsName", () => {
  assert.equal(propertyLabel("departmentId", department), "Department");
  assert.equal(propertyLabel("managerId", people), "Manager");
  assert.equal(propertyLabel("departmentId", null), "Department id");
  assert.equal(propertyLabel("id", department), "Id");
});

test("shownValues_ACreation_ShowsTheValuesItWasCreatedWithLeavingOutEmptyFields", () => {
  const shown = shownValues("created", [
    value({ property: "displayName", after: "Vikram Iyer" }),
    value({ property: "phoneNumber", after: null }),
    value({ property: "status", kind: "enum", after: "active" }),
    value({
      property: "departmentId",
      kind: "reference",
      reference: department,
      after: "0192",
      afterName: "Payroll",
    }),
  ]);

  assert.deepEqual(shown, {
    reading: "created",
    values: [
      { label: "Display name", value: "Vikram Iyer" },
      { label: "Status", value: "Active" },
      { label: "Department", value: "Payroll" },
    ],
  });
});

test("shownValues_ARemovalWithoutAfterValues_ShowsTheValuesTheRecordHad", () => {
  const shown = shownValues("deleted", [
    value({ property: "effectiveFrom", kind: "date", before: "2026-04-01" }),
    value({ property: "managerId", kind: "reference", reference: people, before: null }),
  ]);

  assert.deepEqual(shown, {
    reading: "removed",
    values: [{ label: "Effective from", value: "01-04-2026" }],
  });
});

test("shownValues_AChange_ShowsEachValueBesideTheOneBefore", () => {
  const values = [
    value({ property: "name", before: "Payroll", after: "Payroll operations" }),
    value({
      property: "parentId",
      kind: "reference",
      reference: department,
      before: null,
      after: "0192",
      afterName: "Finance",
    }),
  ];

  for (const operation of ["updated", "restored"] as const) {
    assert.deepEqual(shownValues(operation, values), {
      reading: "changed",
      values: [
        { label: "Name", value: "Payroll operations", was: "Payroll" },
        { label: "Parent", value: "Finance", was: notSet },
      ],
    });
  }
});

test("shownValues_ARemovalThatAlsoChangedAValue_ShowsItAsAChange", () => {
  const shown = shownValues("deleted", [
    value({ property: "name", before: "Payroll", after: "Payroll (closed)" }),
  ]);

  assert.deepEqual(shown, {
    reading: "changed",
    values: [{ label: "Name", value: "Payroll (closed)", was: "Payroll" }],
  });
});

test("shownValues_NoValues_ShowsNone", () => {
  assert.deepEqual(shownValues("deleted", []), { reading: "removed", values: [] });
  assert.deepEqual(shownValues("updated", []), { reading: "changed", values: [] });
});

test("snapshotSummary_ACreationOrARemoval_CountsItsValues", () => {
  assert.equal(snapshotSummary("created", 1), "Created with 1 value");
  assert.equal(snapshotSummary("created", 5), "Created with 5 values");
  assert.equal(snapshotSummary("removed", 2), "Had 2 values when deleted");
});
