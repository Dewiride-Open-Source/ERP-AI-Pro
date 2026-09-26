"use server";

import { z } from "zod";

import { callApi, sendApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";

const attachmentId = z.uuid();

export type DownloadLinkResult = { readonly url: string } | { readonly error: string };

export type DeleteResult = { readonly error?: string };

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

export async function deleteAttachment(id: string): Promise<DeleteResult> {
  const parsed = attachmentId.safeParse(id);
  if (!parsed.success) return { error: "That attachment does not exist." };

  try {
    await sendApi((client) => client.api.platform.attachments.byId(parsed.data).delete());
    return {};
  } catch (error) {
    return { error: describe(error) };
  }
}

function describe(error: unknown): string {
  if (error instanceof ApiError) return error.problem.detail ?? error.message;
  return "The API did not respond.";
}
