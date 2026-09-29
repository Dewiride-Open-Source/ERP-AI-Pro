"use client";

import { unstable_isUnrecognizedActionError, unstable_rethrow } from "next/navigation";
import { createContext, use } from "react";

import { rejectedActionState } from "../forms/client/rejected-action.ts";

import type { RowRemovalOutcome } from "./optimistic-rows.ts";

export type RemoveRow = (id: string, remove: () => Promise<RowRemovalOutcome>) => Promise<RowRemovalOutcome>;

export const RowRemovalContext = createContext<RemoveRow | undefined>(undefined);

export function useRemoveRow(): RemoveRow {
  const removeRow = use(RowRemovalContext);
  if (removeRow === undefined) throw new Error("useRemoveRow is called outside a ListTable.");
  return removeRow;
}

export async function settleRemoval(remove: () => Promise<RowRemovalOutcome>): Promise<RowRemovalOutcome> {
  try {
    return await remove();
  } catch (error) {
    unstable_rethrow(error);
    const failed = rejectedActionState(error, unstable_isUnrecognizedActionError);
    return { removed: false, message: failed.message, reference: failed.reference };
  }
}
