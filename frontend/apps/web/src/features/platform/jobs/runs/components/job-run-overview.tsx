import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { HistoryIcon, ListChecksIcon } from "lucide-react";
import { notFound } from "next/navigation";
import type { ReactNode } from "react";

import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { formatDateTimeIst } from "@/shared/format/dates";

import { jobsNavigation } from "../../nav";
import { getRun } from "../server/queries";

import { RunActions } from "./run-actions";
import { RunEvents } from "./run-events";
import { runRow, type RunRow } from "./run-rows";
import { RunStatus } from "./run-status-badge";
import { givesUpText, noError, notFinished, notStarted, runTitle, triesText } from "./run-text";

const missing = "—";

const statusLineId = "job-run-status-line";

const historyHeadingId = "job-run-history-heading";

export async function JobRunOverview({ runId }: { runId: string }) {
  const read = await getRun(runId);
  if (read.status === "missing") notFound();
  if (read.status === "failed") return <RunUnavailable reason={describeFailure(read.reason)} />;
  const run = read.history.run ? runRow(read.history.run) : undefined;
  if (run === undefined) return <RunUnavailable reason="The API sent this run without its details." />;

  const session = await requireSession();
  const rights = {
    rerun: mayShow(session, "platform.jobs.runs.rerun"),
    cancel: mayShow(session, "platform.jobs.runs.cancel"),
  };

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-4">
        <div className="grid gap-1">
          <p className="text-eyebrow text-primary uppercase">{jobsNavigation.title}</p>
          <h1 className="text-title wrap-anywhere">{run.jobLabel}</h1>
          <p id={statusLineId} tabIndex={-1} className="outline-none" data-testid="job-run-status-line">
            <RunStatus run={run} />
          </p>
        </div>
        <RunActions run={run} rights={rights} focusAfterId={statusLineId} align="start" />
      </header>

      <RunDetails run={run} />

      <Card data-testid="job-run-history">
        <CardHeader>
          <CardTitle>
            <h2 id={historyHeadingId} className="flex items-center gap-2">
              <HistoryIcon className="size-4 text-muted-foreground" aria-hidden />
              History
            </h2>
          </CardTitle>
          <CardDescription>
            Everything that happened to this run, oldest first, in India Standard Time.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <RunEvents events={read.history.events ?? []} labelledBy={historyHeadingId} />
        </CardContent>
      </Card>
    </div>
  );
}

function RunDetails({ run }: { run: RunRow }) {
  const unfinished = run.status === "pending" || run.status === "running" || run.status === "failed";

  return (
    <Card data-testid="job-run-details">
      <CardHeader>
        <CardTitle>
          <h2 className="flex items-center gap-2">
            <ListChecksIcon className="size-4 text-muted-foreground" aria-hidden />
            Details
          </h2>
        </CardTitle>
        <CardDescription>
          The key makes the run unique among the runs of its job. A scheduled run&apos;s key holds the time it
          was scheduled for.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <dl className="grid gap-4 sm:grid-cols-2">
          <Detail label="Job">
            {run.jobDefined ? run.jobLabel : `${run.jobName} (not in this version of the ERP)`}
          </Detail>
          <Detail label="Key">
            <span className="font-mono text-sm break-all">{run.key}</span>
          </Detail>
          <Detail label="Requested by">{run.requestedBy}</Detail>
          <Detail label="Created">{dateText(run.createdAt, missing)}</Detail>
          {unfinished ? <Detail label="Due">{dateText(run.dueAt, missing)}</Detail> : null}
          <Detail label="Latest try started">{dateText(run.startedAt, notStarted)}</Detail>
          <Detail label="Finished">{dateText(run.finishedAt, notFinished)}</Detail>
          <Detail label="Tries">{triesText(run)}</Detail>
          <Detail label="Gives up">{givesUpText(run.maxAttempts)}</Detail>
          <Detail label="Last error">{run.lastError ?? noError}</Detail>
        </dl>
      </CardContent>
    </Card>
  );
}

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0 rounded-lg border bg-muted/40 p-4">
      <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</dt>
      <dd className="mt-1 text-base font-medium wrap-anywhere">{children}</dd>
    </div>
  );
}

function dateText(value: Date | undefined, fallback: string): string {
  return value === undefined ? fallback : formatDateTimeIst(value);
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function RunUnavailable({ reason }: { reason: string }) {
  return (
    <div className="grid gap-section">
      <header className="grid gap-1">
        <p className="text-eyebrow text-primary uppercase">{jobsNavigation.title}</p>
        <h1 className="text-title">{runTitle}</h1>
      </header>
      <div
        role="status"
        data-testid="job-run-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">This run&apos;s details are not available.</p>
        <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
      </div>
    </div>
  );
}
