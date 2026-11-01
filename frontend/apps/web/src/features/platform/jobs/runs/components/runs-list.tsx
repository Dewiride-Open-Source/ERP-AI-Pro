"use client";

import type { DataTableColumn, DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import { DataTableDateRangeFilter } from "@dewiride/erp-ui/components/data-table/filters/date-range-filter";
import {
  DataTableOptionsFilter,
  type DataTableFilterOption,
} from "@dewiride/erp-ui/components/data-table/filters/options-filter";
import { DataTableTextFilter } from "@dewiride/erp-ui/components/data-table/filters/text-filter";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import Link from "next/link";
import { useMemo } from "react";

import { formatDateTimeIst } from "@/shared/format/dates";
import { isDateRange, type ListFilterValue, type ListQuery } from "@/shared/lists/list-query";
import { ListTable } from "@/shared/lists/list-table";

import { jobsNavigation } from "../../nav";
import { runKeyMaxLength, runKeyPattern, runStatuses, runsList } from "../lists/runs.list";

import { RunActions, type RunActionRights } from "./run-actions";
import type { RunRow } from "./run-rows";
import { RunStatus } from "./run-status-badge";
import { noError, notFinished, runStatusLabel, triesText } from "./run-text";

const keyPattern = {
  source: runKeyPattern,
  message: "A key holds only letters, digits and the characters . _ : + -, without spaces.",
};

const statusOptions = runStatuses.map((status) => ({ value: status, label: runStatusLabel(status) }));

const missing = "—";

const noChoices: readonly string[] = [];

function dateText(value: Date | undefined, fallback: string): string {
  return value === undefined ? fallback : formatDateTimeIst(value);
}

function runColumns(focusAfterId: string, rights: RunActionRights): readonly DataTableColumn<RunRow>[] {
  return [
    {
      id: "job",
      header: "Job",
      cell: (run) => (
        <Link
          href={`/platform/jobs/runs/${run.id}`}
          prefetch={false}
          className="rounded-sm font-medium break-words whitespace-normal text-primary underline-offset-4 focus-ring hover:underline"
          data-testid="job-run-link"
        >
          {run.jobLabel}
        </Link>
      ),
      hideable: false,
      role: "title",
      className: "min-w-40 max-w-64",
    },
    {
      id: "status",
      header: "Status",
      cell: (run) => <RunStatus run={run} testId="job-run-status" />,
      hideable: false,
      className: "min-w-40 whitespace-normal",
    },
    {
      id: "idempotencyKey",
      header: "Key",
      cell: (run) => (
        <span className="font-mono text-xs" data-testid="job-run-key">
          {run.key}
        </span>
      ),
    },
    { id: "createdAt", header: "Created", cell: (run) => dateText(run.createdAt, missing), sort: "date" },
    { id: "dueAt", header: "Due", cell: (run) => dateText(run.dueAt, missing), sort: "date" },
    {
      id: "finishedAt",
      header: "Finished",
      cell: (run) => dateText(run.finishedAt, notFinished),
      sort: "date",
    },
    { id: "tries", header: "Tries", cell: (run) => triesText(run) },
    {
      id: "lastError",
      header: "Last error",
      cell: (run) => run.lastError ?? noError,
      className: "max-w-64 whitespace-normal break-words",
    },
    {
      id: "actions",
      header: "Actions",
      role: "actions",
      align: "end",
      cell: (run) => <RunActions run={run} rights={rights} focusAfterId={focusAfterId} align="end" />,
    },
  ];
}

export function RunsList({
  query,
  page,
  runs,
  jobOptions,
  rights,
  labelledBy,
}: {
  query: ListQuery;
  page: DataTablePage;
  runs: readonly RunRow[];
  jobOptions: readonly DataTableFilterOption[];
  rights: RunActionRights;
  labelledBy: string;
}) {
  const { rerun, cancel } = rights;
  const columns = useMemo(() => runColumns(labelledBy, { rerun, cancel }), [labelledBy, rerun, cancel]);
  const { job, status, key, created } = query.filters;
  const chosenJobs = choices(job);
  const jobChoices = useMemo(() => {
    const known = new Set(jobOptions.map((option) => option.value));
    return [
      ...jobOptions,
      ...chosenJobs.filter((name) => !known.has(name)).map((name) => ({ value: name, label: name })),
    ];
  }, [jobOptions, chosenJobs]);

  return (
    <ListTable
      basePath={jobsNavigation.basePath}
      definition={runsList}
      query={query}
      page={page}
      label="Runs"
      labelledBy={labelledBy}
      noun={{ one: "run", other: "runs" }}
      columns={columns}
      rows={runs}
      getRowId={(run) => run.id}
      noMatches="No runs match these filters."
      empty={
        <Empty className="border" data-testid="job-runs-empty">
          <EmptyHeader>
            <EmptyTitle>No runs yet.</EmptyTitle>
            <EmptyDescription>Each time the ERP runs a job, the run appears here.</EmptyDescription>
          </EmptyHeader>
        </Empty>
      }
      filters={
        <>
          {jobChoices.length > 0 ? (
            <DataTableOptionsFilter name="job" label="Job" options={jobChoices} defaultValues={chosenJobs} />
          ) : null}
          <DataTableOptionsFilter
            name="status"
            label="Status"
            options={statusOptions}
            defaultValues={choices(status)}
          />
          <DataTableTextFilter
            name="key"
            label="Key"
            defaultValue={typeof key === "string" ? key : ""}
            placeholder="Search by key"
            maxLength={runKeyMaxLength}
            pattern={keyPattern}
          />
          <DataTableDateRangeFilter
            name="created"
            label="Created between"
            defaultValue={created !== undefined && isDateRange(created) ? created : { from: "", to: "" }}
          />
        </>
      }
    />
  );
}

function choices(value: ListFilterValue | undefined): readonly string[] {
  return Array.isArray(value) ? value : noChoices;
}
