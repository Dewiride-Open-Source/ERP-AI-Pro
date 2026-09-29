import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { FilesIcon, UploadIcon } from "lucide-react";
import { redirect } from "next/navigation";

import { ApiError } from "@/shared/api/problem-details";
import { listApiParameters } from "@/shared/lists/list-api";
import { listHref, readListQuery, withPage, type SearchParameters } from "@/shared/lists/list-query";

import { attachmentsNavigation } from "../../nav";
import { attachmentsList } from "../lists/attachments.list";
import { getAttachments, getUploadPolicy } from "../server/queries";

import { AttachmentsList } from "./attachments-list";
import { UploadPanel } from "./upload-panel";

const listHeadingId = "attachments-list-heading";

export async function AttachmentsOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { basePath } = attachmentsNavigation;
  const { query, canonical } = readListQuery(searchParameters, attachmentsList);
  if (!canonical) redirect(listHref(basePath, query, attachmentsList));

  const [policy, attachments] = await Promise.allSettled([
    getUploadPolicy(),
    getAttachments(listApiParameters(query, attachmentsList)),
  ]);
  if (attachments.status === "fulfilled") {
    const totalPages = attachments.value.totalPages ?? 0;
    if (query.page > 1 && query.page > totalPages) {
      redirect(listHref(basePath, withPage(query, Math.max(1, totalPages)), attachmentsList));
    }
  }

  return (
    <div className="grid gap-section">
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
          <CardDescription>Downloads use a link that works for a few minutes.</CardDescription>
        </CardHeader>
        <CardContent>
          {attachments.status === "fulfilled" ? (
            <AttachmentsList
              query={query}
              page={{
                page: attachments.value.page ?? query.page,
                pageSize: attachments.value.pageSize ?? query.pageSize,
                totalCount: attachments.value.totalCount ?? 0,
                pageCount: attachments.value.totalPages ?? 0,
              }}
              attachments={attachments.value.items ?? []}
              allowedContentTypes={
                policy.status === "fulfilled" ? (policy.value.allowedContentTypes ?? []) : []
              }
              labelledBy={listHeadingId}
            />
          ) : (
            <Unavailable reason={describeFailure(attachments.reason)} />
          )}
        </CardContent>
      </Card>
    </div>
  );
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
