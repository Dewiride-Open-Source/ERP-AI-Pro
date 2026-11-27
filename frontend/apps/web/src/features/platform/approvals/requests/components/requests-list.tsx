"use client";

import type { DataTableColumn, DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import {
  DataTableOptionsFilter,
  type DataTableFilterOption,
} from "@dewiride/erp-ui/components/data-table/filters/options-filter";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import Link from "next/link";
import { useMemo } from "react";

import { formatDateTimeIst } from "@/shared/format/dates";
import type { ListFilterValue, ListQuery } from "@/shared/lists/list-query";
import { ListTable } from "@/shared/lists/list-table";

import { approvalStates, requestLists, type RequestBox } from "../lists/requests.list";

import { requestBoxPages } from "./approval-pages";
import { LateBadge, RequestStanding } from "./request-badges";
import type { RequestRow } from "./request-rows";
import { approvalStateLabel, dueDateText, notRecorded } from "./request-text";

const stateOptions = approvalStates.map((state) => ({ value: state, label: approvalStateLabel(state) }));

const noChoices: readonly string[] = [];

const requestColumn: DataTableColumn<RequestRow> = {
  id: "request",
  header: "Request",
  cell: (request) => (
    <span className="grid gap-0.5">
      <Link
        href={`/platform/approvals/requests/${request.id}`}
        prefetch={false}
        aria-label={request.linkName}
        className="w-fit rounded-sm font-medium break-words whitespace-normal text-primary underline-offset-4 focus-ring hover:underline"
        data-testid="approval-request-link"
        data-request-link={request.id}
      >
        {request.kindTitle}
      </Link>
      {request.subjectName === undefined ? null : (
        <span
          className="text-sm break-words whitespace-normal text-muted-foreground"
          data-testid="approval-request-record"
        >
          {request.subjectName}
        </span>
      )}
    </span>
  ),
  hideable: false,
  role: "title",
  className: "min-w-40 max-w-72",
};

const askedByColumn: DataTableColumn<RequestRow> = {
  id: "requestedFor",
  header: "Asked by",
  cell: (request) =>
    request.submittedBy === undefined ? (
      request.askedBy
    ) : (
      <span className="grid gap-0.5">
        <span>{request.askedBy}</span>
        <span className="text-sm text-muted-foreground">Submitted by {request.submittedBy}</span>
      </span>
    ),
  className: "min-w-32 max-w-56 whitespace-normal break-words",
};

const askedOnColumn: DataTableColumn<RequestRow> = {
  id: "submittedAt",
  header: "Asked on",
  cell: (request) => formatDateTimeIst(request.submittedAt),
  sort: "date",
};

const stepColumn: DataTableColumn<RequestRow> = {
  id: "step",
  header: "Step",
  cell: (request) => request.waiting ?? notRecorded,
  className: "min-w-40 max-w-64 whitespace-normal break-words",
};

const stateColumn: DataTableColumn<RequestRow> = {
  id: "state",
  header: "Status",
  cell: (request) => (
    <span className="grid gap-1">
      <RequestStanding state={request.state} late={request.late} testId="approval-request-state" />
      {request.waiting === undefined ? null : (
        <span className="text-sm text-muted-foreground">{request.waiting}</span>
      )}
    </span>
  ),
  hideable: false,
  className: "min-w-40 max-w-64 whitespace-normal break-words",
};

const closedColumn: DataTableColumn<RequestRow> = {
  id: "closedAt",
  header: "Closed",
  cell: (request) => (request.closedAt === undefined ? notRecorded : formatDateTimeIst(request.closedAt)),
  sort: "date",
};

// The waiting box has no status column, so its due dates carry the late mark; elsewhere the status shows it.
function dueColumn(marksLate: boolean): DataTableColumn<RequestRow> {
  return {
    id: "dueAt",
    header: "Due",
    cell: (request) =>
      request.dueAt === undefined ? (
        notRecorded
      ) : (
        <span
          className="inline-flex flex-wrap items-center gap-x-2 gap-y-1"
          data-testid="approval-request-due"
        >
          {dueDateText(request.dueAt)}
          {marksLate && request.late ? (
            <>
              {" "}
              <LateBadge />
            </>
          ) : null}
        </span>
      ),
    sort: "date",
    hideable: !marksLate,
  };
}

const waitingColumns: readonly DataTableColumn<RequestRow>[] = [
  requestColumn,
  askedByColumn,
  askedOnColumn,
  stepColumn,
  dueColumn(true),
];

const answeredColumns: readonly DataTableColumn<RequestRow>[] = [
  requestColumn,
  askedByColumn,
  askedOnColumn,
  stateColumn,
  dueColumn(false),
  closedColumn,
];

export function RequestsList({
  box,
  query,
  page,
  requests,
  kindOptions,
  labelledBy,
}: {
  box: RequestBox;
  query: ListQuery;
  page: DataTablePage;
  requests: readonly RequestRow[];
  kindOptions: readonly DataTableFilterOption[];
  labelledBy: string;
}) {
  const boxPage = requestBoxPages[box];
  const { kind, state } = query.filters;
  const chosenKinds = choices(kind);
  const kindChoices = useMemo(() => {
    const known = new Set(kindOptions.map((option) => option.value));
    return [
      ...kindOptions,
      ...chosenKinds.filter((key) => !known.has(key)).map((key) => ({ value: key, label: key })),
    ];
  }, [kindOptions, chosenKinds]);
  const filtersState = box !== "waiting";
  const hasFilters = kindChoices.length > 0 || filtersState;

  return (
    <ListTable
      basePath={boxPage.path}
      definition={requestLists[box]}
      query={query}
      page={page}
      label={boxPage.listHeading}
      labelledBy={labelledBy}
      noun={{ one: "request", other: "requests" }}
      columns={box === "waiting" ? waitingColumns : answeredColumns}
      rows={requests}
      getRowId={(request) => request.id}
      noMatches="No requests match these filters."
      empty={
        <Empty className="border" data-testid="approval-requests-empty">
          <EmptyHeader>
            <EmptyTitle>{boxPage.empty.title}</EmptyTitle>
            <EmptyDescription>{boxPage.empty.description}</EmptyDescription>
          </EmptyHeader>
        </Empty>
      }
      filters={
        hasFilters ? (
          <>
            {kindChoices.length > 0 ? (
              <DataTableOptionsFilter
                name="kind"
                label="Kind of request"
                options={kindChoices}
                defaultValues={chosenKinds}
              />
            ) : null}
            {filtersState ? (
              <DataTableOptionsFilter
                name="state"
                label="Status"
                options={stateOptions}
                defaultValues={choices(state)}
              />
            ) : null}
          </>
        ) : undefined
      }
    />
  );
}

function choices(value: ListFilterValue | undefined): readonly string[] {
  return Array.isArray(value) ? value : noChoices;
}
