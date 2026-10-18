import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

import { byName } from "../../organisation/components/disclosure-tree.ts";
import { organisationBusy } from "../../positions/forms/record-move.schema.ts";

export interface DesignationRecord {
  readonly id: string;
  readonly name: string;
  readonly version: string | undefined;
}

export interface DesignationResponseLike {
  readonly id?: string | null | undefined;
  readonly name?: string | null | undefined;
  readonly version?: string | null | undefined;
}

export interface DesignationDeletion {
  readonly title: string;
  readonly description: string;
  readonly confirm: string;
  readonly cancel: string;
  readonly done: string;
  readonly gone: string;
}

export const designationAlreadyDeletedCode = "designation.not-found";

export const designationAlreadyDeleted = "It had already been deleted.";

export const deleteDesignationProblemMessages: ProblemMessages = {
  "designation.in-use":
    "Someone holds this designation today or from a later date. Record another designation for them first.",
  "concurrency.conflict": organisationBusy,
};

export function designationRecords(designations: readonly DesignationResponseLike[]): DesignationRecord[] {
  return designations
    .flatMap((designation) =>
      designation.id && designation.name
        ? [{ id: designation.id, name: designation.name, version: designation.version ?? undefined }]
        : [],
    )
    .sort(byName);
}

export function renameLabel(name: string): string {
  return `Rename ${name}`;
}

export function deleteLabel(name: string): string {
  return `Delete ${name}`;
}

export function historyLabel(name: string): string {
  return `Change history of ${name}`;
}

export function addedMessage(name: string): string {
  return `Added ${name}.`;
}

export function renamedMessage(from: string, to: string): string {
  return from === to ? `Saved ${to}.` : `Renamed ${from} to ${to}.`;
}

export function designationDeletion(name: string): DesignationDeletion {
  return {
    title: `Delete ${name}?`,
    description: `${name} is removed from the designations. The moves that named it keep its name, so people's history still shows it.`,
    confirm: "Delete",
    cancel: "Keep it",
    done: `Deleted ${name}.`,
    gone: `${name} is no longer recorded.`,
  };
}
