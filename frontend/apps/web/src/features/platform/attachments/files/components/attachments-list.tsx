"use client";

import type { AttachmentResponse } from "@dewiride/erp-api-client";
import type { DataTableColumn, DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import { DataTableDateRangeFilter } from "@dewiride/erp-ui/components/data-table/filters/date-range-filter";
import { DataTableOptionsFilter } from "@dewiride/erp-ui/components/data-table/filters/options-filter";
import { DataTableTextFilter } from "@dewiride/erp-ui/components/data-table/filters/text-filter";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { useMemo } from "react";

import { formatDateTimeIst } from "@/shared/format/dates";
import { formatBytes } from "@/shared/format/sizes";
import { isDateRange, type ListQuery } from "@/shared/lists/list-query";
import { ListTable } from "@/shared/lists/list-table";

import { attachmentsNavigation } from "../../nav";
import { attachmentsList } from "../lists/attachments.list";

import { AttachmentActions } from "./attachment-actions";
import { contentTypeLabel } from "./content-types";

const missing = "—";

const noValues: readonly string[] = [];

function attachmentColumns(focusAfterDeleteId: string): readonly DataTableColumn<AttachmentResponse>[] {
  return [
    {
      id: "fileName",
      header: "File",
      cell: (attachment) => (
        <span className="font-medium break-words whitespace-normal" data-testid="attachments-file-name">
          {attachment.fileName ?? missing}
        </span>
      ),
      sort: "text",
      hideable: false,
      role: "title",
      className: "max-w-64",
    },
    {
      id: "contentType",
      header: "Type",
      cell: (attachment) => contentTypeLabel(attachment.contentType),
      sort: "text",
    },
    {
      id: "sizeBytes",
      header: "Size",
      cell: (attachment) => (
        <span className="tabular-nums">
          {attachment.sizeBytes === undefined || attachment.sizeBytes === null
            ? missing
            : formatBytes(attachment.sizeBytes)}
        </span>
      ),
      sort: "number",
      align: "end",
    },
    {
      id: "createdAt",
      header: "Uploaded",
      cell: (attachment) => (attachment.createdAt ? formatDateTimeIst(attachment.createdAt) : missing),
      sort: "date",
    },
    {
      id: "scanStatus",
      header: "Virus scan",
      cell: (attachment) => (
        <Badge variant={attachment.scanStatus === "clean" ? "secondary" : "outline"}>
          {attachment.scanStatus === "clean" ? "Clean" : "Not scanned"}
        </Badge>
      ),
    },
    {
      id: "actions",
      header: "Actions",
      role: "actions",
      align: "end",
      cell: (attachment) =>
        attachment.id ? (
          <AttachmentActions
            id={attachment.id}
            fileName={attachment.fileName ?? "this attachment"}
            focusAfterDeleteId={focusAfterDeleteId}
          />
        ) : null,
    },
  ];
}

export function AttachmentsList({
  query,
  page,
  attachments,
  allowedContentTypes,
  labelledBy,
}: {
  query: ListQuery;
  page: DataTablePage;
  attachments: readonly AttachmentResponse[];
  allowedContentTypes: readonly string[];
  labelledBy: string;
}) {
  const columns = useMemo(() => attachmentColumns(labelledBy), [labelledBy]);
  const typeOptions = useMemo(
    () =>
      allowedContentTypes.map((contentType) => ({
        value: contentType,
        label: contentTypeLabel(contentType),
      })),
    [allowedContentTypes],
  );
  const name = query.filters.name;
  const type = query.filters.type;
  const uploaded = query.filters.uploaded;

  return (
    <ListTable
      basePath={attachmentsNavigation.basePath}
      definition={attachmentsList}
      query={query}
      page={page}
      label="Stored files"
      labelledBy={labelledBy}
      noun={{ one: "file", other: "files" }}
      columns={columns}
      rows={attachments}
      getRowId={(attachment) => attachment.id ?? ""}
      noMatches="No files match these filters."
      empty={
        <Empty className="border" data-testid="attachments-empty">
          <EmptyHeader>
            <EmptyTitle>No attachments yet.</EmptyTitle>
            <EmptyDescription>Upload a file to see it here.</EmptyDescription>
          </EmptyHeader>
        </Empty>
      }
      filters={
        <>
          <DataTableTextFilter
            name="name"
            label="File name"
            defaultValue={typeof name === "string" ? name : ""}
            placeholder="Search by name"
            maxLength={255}
          />
          {typeOptions.length > 0 ? (
            <DataTableOptionsFilter
              name="type"
              label="Type"
              options={typeOptions}
              defaultValues={Array.isArray(type) ? type : noValues}
            />
          ) : null}
          <DataTableDateRangeFilter
            name="uploaded"
            label="Uploaded between"
            defaultValue={uploaded !== undefined && isDateRange(uploaded) ? uploaded : { from: "", to: "" }}
          />
        </>
      }
    />
  );
}
