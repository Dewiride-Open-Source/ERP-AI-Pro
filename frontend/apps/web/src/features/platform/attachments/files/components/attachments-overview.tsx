import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { FilesIcon, UploadIcon } from "lucide-react";
import { redirect } from "next/navigation";

import { readPageParameter } from "@/shared/api/paging";
import { ApiError } from "@/shared/api/problem-details";

import { attachmentsNavigation } from "../../nav";
import { attachmentsPageSize, getAttachments, getUploadPolicy } from "../server/queries";

import { AttachmentsPagination } from "./attachments-pagination";
import { AttachmentsTable } from "./attachments-table";
import { UploadPanel } from "./upload-panel";

const listHeadingId = "attachments-list-heading";

export async function AttachmentsOverview({
  pageParameter,
}: {
  pageParameter: string | readonly string[] | undefined;
}) {
  const page = readPageParameter(pageParameter, attachmentsPageSize);
  if (page === undefined) redirect(listPage(1));

  const [policy, attachments] = await Promise.allSettled([getUploadPolicy(), getAttachments(page)]);
  if (attachments.status === "fulfilled") {
    const totalPages = attachments.value.totalPages ?? 0;
    if (page > 1 && page > totalPages) redirect(listPage(totalPages));
  }

  return (
    <div className="grid animate-fade-up gap-section">
      <header>
        <p className="text-eyebrow text-primary uppercase">Platform</p>
        <h1 className="text-title">Attachments</h1>
      </header>

      <Card data-testid="attachments-upload-card">
        <CardHeader>
          <CardTitle>
            <h2 className="flex items-center gap-2">
              <UploadIcon className="size-4 text-muted-foreground" aria-hidden />
              Upload
            </h2>
          </CardTitle>
          <CardDescription>Files are encrypted before they are stored.</CardDescription>
        </CardHeader>
        <CardContent>
          {policy.status === "fulfilled" ? (
            <UploadPanel
              maxSizeBytes={policy.value.maxSizeBytes ?? 0}
              allowedContentTypes={policy.value.allowedContentTypes ?? []}
            />
          ) : (
            <Unavailable reason={describeFailure(policy.reason)} />
          )}
        </CardContent>
      </Card>

      <Card data-testid="attachments-list-card">
        <CardHeader>
          <CardTitle>
            <h2 id={listHeadingId} tabIndex={-1} className="flex items-center gap-2 outline-none">
              <FilesIcon className="size-4 text-muted-foreground" aria-hidden />
              Stored files
            </h2>
          </CardTitle>
          <CardDescription>Newest first. Downloads use a link that works for a few minutes.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          {attachments.status === "fulfilled" ? (
            <>
              <AttachmentsTable
                attachments={attachments.value.items ?? []}
                focusAfterDeleteId={listHeadingId}
              />
              <AttachmentsPagination
                page={attachments.value.page ?? page}
                totalPages={attachments.value.totalPages ?? 1}
              />
            </>
          ) : (
            <Unavailable reason={describeFailure(attachments.reason)} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function listPage(page: number) {
  const { basePath } = attachmentsNavigation;
  return page > 1 ? (`${basePath}?page=${page}` as const) : basePath;
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function Unavailable({ reason }: { reason: string }) {
  return (
    <div
      role="status"
      data-testid="attachments-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">Attachments are not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
    </div>
  );
}
