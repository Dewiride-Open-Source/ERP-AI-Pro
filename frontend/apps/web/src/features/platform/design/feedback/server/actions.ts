"use server";

import { refresh } from "next/cache";
import { cookies, headers } from "next/headers";

import type { RowRemovalOutcome } from "@/shared/lists/optimistic-rows";

import { approvalRequests, dismissedApprovalsCookie, readDismissedApprovals } from "./queries";

async function writeDismissed(ids: ReadonlySet<string>): Promise<void> {
  const origin = (await headers()).get("origin") ?? "";
  (await cookies()).set(dismissedApprovalsCookie, [...ids].join(","), {
    httpOnly: true,
    sameSite: "lax",
    secure: origin.startsWith("https://"),
    path: "/design/feedback",
    ...(ids.size === 0 ? { maxAge: 0 } : {}),
  });
  refresh();
}

export async function dismissApproval(id: string): Promise<RowRemovalOutcome> {
  const request = approvalRequests.find((candidate) => candidate.id === id);
  if (request === undefined) return { removed: false, message: "That request does not exist." };
  if (request.locked) return { removed: false, message: "This request is locked while finance reviews it." };

  const dismissed = new Set(readDismissedApprovals((await cookies()).get(dismissedApprovalsCookie)?.value));
  dismissed.add(request.id);
  await writeDismissed(dismissed);
  return { removed: true };
}

export async function restoreApprovals(): Promise<void> {
  await writeDismissed(new Set());
}

export async function reconnectBankFeed(): Promise<{ readonly connected: true }> {
  return { connected: true };
}
