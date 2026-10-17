import type {
  ChangeActorKind,
  ChangedValueResponse,
  ChangeOperation,
  ChangeRecordResponse,
  ChangeValueKind,
} from "@dewiride/erp-api-client";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";

import { formatDateTimeIst } from "@/shared/format/dates";

interface RecordType {
  readonly label: string;
  readonly noun: string;
  readonly createdVerb?: string;
}

// A change key the API records but this table does not know yet shows as the key itself, so a new kind of record is never
// left out of the history.
const recordTypes = new Map<string, RecordType>([
  ["identity.users.people", { label: "Person", noun: "person" }],
  ["identity.users.departments", { label: "Department", noun: "department" }],
  ["identity.users.designations", { label: "Designation", noun: "designation" }],
  ["identity.users.positions", { label: "Move", noun: "move" }],
  ["platform.attachments.files", { label: "File", noun: "file", createdVerb: "uploaded" }],
]);

const operationLabels: Readonly<Record<ChangeOperation, string>> = {
  created: "Created",
  updated: "Changed",
  deleted: "Deleted",
  restored: "Restored",
};

const operationVerbs: Readonly<Record<ChangeOperation, string>> = {
  created: "created",
  updated: "changed",
  deleted: "deleted",
  restored: "restored",
};

const wordPattern = /[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+/g;

const wholeNumber = /^-?\d+$/;

const byteCount = new Intl.NumberFormat("en-IN");

export const notSet = "Not set";

export interface ChangeSentence {
  readonly actor: string;
  readonly action: string;
  readonly subject: string | undefined;
}

export interface ShownValue {
  readonly label: string;
  readonly value: string;
  readonly was?: string | undefined;
}

// A change reads as the values it changed, each beside the value the field held before; a creation as the values it was
// created with and a removal as the values it had, both without the fields that were empty.
export type ValueReading = "changed" | "created" | "removed";

export interface ShownValues {
  readonly reading: ValueReading;
  readonly values: readonly ShownValue[];
}

export function recordTypeLabel(changeKey: string): string {
  return recordTypes.get(changeKey)?.label ?? changeKey;
}

export function operationLabel(operation: ChangeOperation): string {
  return operationLabels[operation];
}

export function actorText(kind: ChangeActorKind | null | undefined, name: string | null | undefined): string {
  switch (kind) {
    case "system":
      return "The ERP";
    case "anonymous":
      return "Someone not signed in";
    default:
      return nonEmpty(name) ?? "Unknown person";
  }
}

export function actorChangesLinkText(
  kind: ChangeActorKind | null | undefined,
  name: string | null | undefined,
): string {
  switch (kind) {
    case "system":
      return "Show changes by the ERP";
    case "anonymous":
      return "Show changes made without signing in";
    default:
      return `Show changes by ${nonEmpty(name) ?? "this person"}`;
  }
}

export function recordHistoryLinkText(subjectName: string | null | undefined): string {
  const name = nonEmpty(subjectName);
  return name === undefined ? "Show the history of this record" : `Show the history of ${name}`;
}

// A record filed under another, such as a move under its person, is named through the record it is filed under.
export function changeSentence(change: ChangeRecordResponse): ChangeSentence {
  const operation = change.operation ?? "updated";
  const entityType = change.entityType ?? "";
  const subjectType = change.subjectType ?? entityType;
  const verb =
    (operation === "created" ? recordTypes.get(entityType)?.createdVerb : undefined) ??
    operationVerbs[operation];
  const noun = recordNoun(entityType);
  const actor = actorText(change.actorKind, change.actorName);
  const subject = nonEmpty(change.subjectName);

  if (subjectType === entityType) {
    return {
      actor,
      action: subject === undefined ? `${verb} ${withArticle(noun)}` : `${verb} the ${noun}`,
      subject,
    };
  }
  const filedUnder = subject === undefined ? ` ${withArticle(recordNoun(subjectType))}` : "";
  return { actor, action: `${verb} ${withArticle(noun)} of${filedUnder}`, subject };
}

export function shownValues(
  operation: ChangeOperation | null | undefined,
  changes: readonly ChangedValueResponse[],
): ShownValues {
  if (operation === "created") return { reading: "created", values: snapshot(changes, "after") };
  if (operation === "deleted" && changes.every((change) => isEmpty(change.after))) {
    return { reading: "removed", values: snapshot(changes, "before") };
  }
  return {
    reading: "changed",
    values: changes.map((change) => ({
      label: propertyLabel(change.property ?? "", change.reference),
      value: valueText(change.kind, change.after, change.afterName),
      was: valueText(change.kind, change.before, change.beforeName),
    })),
  };
}

export function snapshotSummary(reading: Exclude<ValueReading, "changed">, count: number): string {
  const values = count === 1 ? "1 value" : `${count} values`;
  return reading === "created" ? `Created with ${values}` : `Had ${values} when deleted`;
}

// A reference names the record it points at, so its field reads "Department" rather than "Department id".
export function propertyLabel(property: string, reference?: string | null): string {
  const words = property
    .split(".")
    .flatMap((segment) => segment.match(wordPattern) ?? [])
    .map((word) => word.toLowerCase());
  if (nonEmpty(reference) !== undefined && words.length > 1 && words.at(-1) === "id") words.pop();
  return words.length === 0 ? property : sentenceCase(words.join(" "));
}

// Each value is read by the kind the API recorded for its field, never by its look: "true" is Yes only in a yes-or-no field.
export function valueText(
  kind: ChangeValueKind | null | undefined,
  value: string | null | undefined,
  name?: string | null,
): string {
  if (value === null || value === undefined || value === "") return notSet;
  switch (kind) {
    case "boolean":
      return booleanText(value);
    case "date":
      return formatDisplayDate(value);
    case "instant":
      return instantText(value);
    case "enum":
      return enumText(value);
    case "list":
      return listText(value);
    case "reference":
      return nonEmpty(name) ?? value;
    case "binary":
      return binaryText(value);
    default:
      return value;
  }
}

function snapshot(changes: readonly ChangedValueResponse[], side: "before" | "after"): ShownValue[] {
  return changes.flatMap((change) => {
    const value = side === "after" ? change.after : change.before;
    if (isEmpty(value)) return [];
    return [
      {
        label: propertyLabel(change.property ?? "", change.reference),
        value: valueText(change.kind, value, side === "after" ? change.afterName : change.beforeName),
      },
    ];
  });
}

function recordNoun(changeKey: string): string {
  return recordTypes.get(changeKey)?.noun ?? changeKey;
}

function withArticle(noun: string): string {
  return `${/^[aeiou]/i.test(noun) ? "an" : "a"} ${noun}`;
}

function booleanText(value: string): string {
  if (value === "true") return "Yes";
  return value === "false" ? "No" : value;
}

function instantText(value: string): string {
  const instant = new Date(value);
  return Number.isNaN(instant.getTime()) ? value : formatDateTimeIst(instant);
}

function enumText(value: string): string {
  const words = value.match(wordPattern);
  return wholeNumber.test(value) || words === null
    ? value
    : sentenceCase(words.map((word) => word.toLowerCase()).join(" "));
}

function listText(value: string): string {
  let elements: unknown;
  try {
    elements = JSON.parse(value);
  } catch {
    return value;
  }
  if (!Array.isArray(elements)) return value;
  if (elements.length === 0) return "None";
  return elements
    .map((element: unknown) => {
      if (element === null) return notSet;
      return typeof element === "string" ? element : JSON.stringify(element);
    })
    .join(", ");
}

function binaryText(value: string): string {
  const bytes = Math.max(0, Math.floor((value.length * 3) / 4) - base64Padding(value));
  return bytes === 1 ? "Binary data, 1 byte" : `Binary data, ${byteCount.format(bytes)} bytes`;
}

function base64Padding(value: string): number {
  if (value.endsWith("==")) return 2;
  return value.endsWith("=") ? 1 : 0;
}

function sentenceCase(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function isEmpty(value: string | null | undefined): boolean {
  return value === null || value === undefined || value === "";
}

function nonEmpty(text: string | null | undefined): string | undefined {
  return text === null || text === undefined || text === "" ? undefined : text;
}
