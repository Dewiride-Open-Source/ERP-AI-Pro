"use server";

import { refresh } from "next/cache";
import { z } from "zod";

import { callApi, sendApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";
import type { RowRemovalOutcome } from "@/shared/lists/optimistic-rows";

const attachmentId = z.uuid();

export type DownloadLinkResult = { readonly url: string } | { readonly error: string };

export async function createDownloadLink(id: string): Promise<DownloadLinkResult> {
  const parsed = attachmentId.safeParse(id);
  if (!parsed.success) return { error: "That attachment does not exist." };

  try {
    const link = await callApi((client) =>
      client.api.platform.attachments.byId(parsed.data).downloadLinks.post(),
    );
    return link.url ? { url: link.url } : { error: "The API returned no download link." };
  } catch (error) {
    return { error: describe(error) };
  }
}

export async function deleteAttachment(id: string): Promise<RowRemovalOutcome> {
  const parsed = attachmentId.safeParse(id);
  if (!parsed.success) return { removed: false, message: "That attachment does not exist." };

  let outcome: RowRemovalOutcome;
  try {
    await sendApi((client) => client.api.platform.attachments.byId(parsed.data).delete());
    outcome = { removed: true };
  } catch (error) {
    outcome = { removed: false, message: describe(error) };
  }
  refresh();
  return outcome;
}

function describe(error: unknown): string {
  if (error instanceof ApiError) return error.problem.detail ?? error.message;
  return "The API did not respond.";
}
