import type { PagedResponseOfAttachmentResponse, UploadPolicyResponse } from "@dewiride/erp-api-client";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { FilesIcon, UploadIcon } from "lucide-react";

import { ApiError } from "@/shared/api/problem-details";

import { getAttachments, getUploadPolicy } from "../server/queries";

import { AttachmentsPagination } from "./attachments-pagination";
import { AttachmentsTable } from "./attachments-table";
import { UploadPanel } from "./upload-panel";

export async function AttachmentsOverview({ page }: { page: number }) {
  let policy: UploadPolicyResponse | undefined;
  let attachments: PagedResponseOfAttachmentResponse | undefined;
  let failure: string | undefined;

  try {
    [policy, attachments] = await Promise.all([getUploadPolicy(), getAttachments(page)]);
  } catch (error) {
    failure = error instanceof ApiError ? error.message : "The API did not respond.";
  }

  return (
    <div className="grid animate-fade-up gap-6">
      <header>
        <p className="text-xs font-semibold tracking-widest text-primary uppercase">Platform</p>
        <h1 className="text-3xl font-semibold tracking-tight">Attachments</h1>
      </header>

      <Card data-testid="attachments-upload-card">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <UploadIcon className="size-4 text-muted-foreground" aria-hidden />
            Upload
          </CardTitle>
          <CardDescription>Files are encrypted before they are stored.</CardDescription>
        </CardHeader>
        <CardContent>
          {policy ? (
            <UploadPanel
              maxSizeBytes={policy.maxSizeBytes ?? 0}
              allowedContentTypes={policy.allowedContentTypes ?? []}
            />
          ) : (
            <Unavailable reason={failure} />
          )}
        </CardContent>
      </Card>

      <Card data-testid="attachments-list-card">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FilesIcon className="size-4 text-muted-foreground" aria-hidden />
            Stored files
          </CardTitle>
          <CardDescription>Newest first. Downloads use a link that works for a few minutes.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          {attachments ? (
            <>
              <AttachmentsTable attachments={attachments.items ?? []} />
              <AttachmentsPagination
                page={attachments.page ?? page}
                totalPages={attachments.totalPages ?? 1}
              />
            </>
          ) : (
            <Unavailable reason={failure} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function Unavailable({ reason }: { reason: string | undefined }) {
  return (
    <div
      role="status"
      data-testid="attachments-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">Attachments are not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason ?? "The API did not respond."}</p>
    </div>
  );
}
