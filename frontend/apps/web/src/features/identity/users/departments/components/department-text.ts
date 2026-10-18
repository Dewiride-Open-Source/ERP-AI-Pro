import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";

import { countLabel } from "../../organisation/components/disclosure-tree.ts";
import { organisationBusy } from "../../positions/forms/record-move.schema.ts";

export interface DepartmentDeletion {
  readonly title: string;
  readonly description: string;
  readonly confirm: string;
  readonly cancel: string;
  readonly done: string;
  readonly gone: string;
}

export const departmentAlreadyDeletedCode = "department.not-found";

export const departmentAlreadyDeleted = "It had already been deleted.";

export const deleteDepartmentProblemMessages: ProblemMessages = {
  "department.in-use":
    "This department still has departments under it, or someone is in it today or from a later date. Move them elsewhere first.",
  "concurrency.conflict": organisationBusy,
};

export function childCountLabel(count: number): string {
  return countLabel(count, "department under it", "departments under it");
}

export function showChildrenLabel(name: string): string {
  return `Show the departments under ${name}`;
}

export function childListLabel(name: string): string {
  return `Departments under ${name}`;
}

export function addUnderLabel(name: string): string {
  return `Add a department under ${name}`;
}

export function editLabel(name: string): string {
  return `Rename or move ${name}`;
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

export function savedMessage(name: string): string {
  return `Saved ${name}.`;
}

export function departmentDeletion(name: string): DepartmentDeletion {
  return {
    title: `Delete ${name}?`,
    description: `${name} is removed from the departments. The moves that named it keep its name, so people's history still shows it.`,
    confirm: "Delete",
    cancel: "Keep it",
    done: `Deleted ${name}.`,
    gone: `${name} is no longer recorded.`,
  };
}
