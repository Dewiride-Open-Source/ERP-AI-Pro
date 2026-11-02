import type { JobResponse, JobScheduleResponse } from "@dewiride/erp-api-client";
import { Alert, AlertDescription, AlertTitle } from "@dewiride/erp-ui/components/ui/alert";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { CalendarClockIcon, TriangleAlertIcon } from "lucide-react";
import Link from "next/link";
import type { ReactNode } from "react";

import { RunStatusBadge } from "../../runs/components/run-status-badge";

import {
  attemptsText,
  lastRunText,
  nextRunText,
  scheduleProblem,
  scheduleProblemAdvice,
  scheduleProblemTitle,
  scheduleText,
} from "./schedule-text";

export type JobsRead =
  | { readonly status: "read"; readonly jobs: readonly JobResponse[] }
  | { readonly status: "failed"; readonly reason: string };

const jobsHeadingId = "jobs-heading";

export function JobsCard({ read }: { read: JobsRead }) {
  return (
    <Card data-testid="jobs-card">
      <CardHeader>
        <CardTitle>
          <h2 id={jobsHeadingId} className="flex items-center gap-2">
            <CalendarClockIcon className="size-4 text-muted-foreground" aria-hidden />
            Jobs
          </h2>
        </CardTitle>
        <CardDescription>The work the ERP does on its own. Times are in India Standard Time.</CardDescription>
      </CardHeader>
      <CardContent>
        <JobsContent read={read} />
      </CardContent>
    </Card>
  );
}

function JobsContent({ read }: { read: JobsRead }) {
  if (read.status === "failed") {
    return (
      <div
        role="status"
        data-testid="jobs-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">The jobs are not available.</p>
        <p className="mt-1 text-sm text-muted-foreground">{read.reason}</p>
      </div>
    );
  }
  if (read.jobs.length === 0) {
    return (
      <Empty className="border" data-testid="jobs-empty">
        <EmptyHeader>
          <EmptyTitle>The ERP has no jobs of its own yet.</EmptyTitle>
          <EmptyDescription>
            Work the ERP does on its own, such as nightly checks, appears here.
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    );
  }
  return (
    <ul role="list" aria-labelledby={jobsHeadingId} className="grid gap-4">
      {read.jobs.map((job, index) => (
        <JobItem key={job.name ?? index} job={job} />
      ))}
    </ul>
  );
}

function JobItem({ job }: { job: JobResponse }) {
  const title = job.title || job.name || "A job";
  const schedule = job.schedule ?? undefined;
  const problem = schedule === undefined ? undefined : scheduleProblem(schedule);

  return (
    <li className="grid min-w-0 gap-4 rounded-lg border p-4" data-testid="job">
      <div className="grid gap-1">
        <h3 className="font-medium wrap-anywhere">{title}</h3>
        {job.description ? <p className="text-muted-foreground">{job.description}</p> : null}
      </div>
      <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
        <Detail label="Schedule" testId="job-schedule">
          {scheduleText(schedule)}
        </Detail>
        {schedule === undefined ? null : (
          <>
            <Detail label="Next run" testId="job-next-run">
              {nextRunText(schedule)}
            </Detail>
            <Detail label="Last run" testId="job-last-run">
              <LastRun schedule={schedule} title={title} />
            </Detail>
          </>
        )}
        <Detail label="Tries" testId="job-tries">
          {attemptsText(job.maxAttempts)}
        </Detail>
      </dl>
      {problem === undefined ? null : (
        <Alert variant="destructive" data-testid="job-problem">
          <TriangleAlertIcon aria-hidden />
          <AlertTitle>{scheduleProblemTitle}</AlertTitle>
          <AlertDescription>
            <p>{problem}</p>
            <p>{scheduleProblemAdvice}</p>
          </AlertDescription>
        </Alert>
      )}
    </li>
  );
}

// A run is kept for a limited time, so the last occurrence can outlive the record of its run, which then has no status to
// show or page to open.
function LastRun({ schedule, title }: { schedule: JobScheduleResponse; title: string }) {
  const { lastRunId, lastRunStatus } = schedule;

  return (
    <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
      {lastRunText(schedule)}
      {lastRunStatus ? (
        <>
          {" "}
          <RunStatusBadge status={lastRunStatus} />
        </>
      ) : null}
      {lastRunId && lastRunStatus ? (
        <>
          {" "}
          <Link
            href={`/platform/jobs/runs/${lastRunId}`}
            prefetch={false}
            aria-label={`Open the last run of ${title}`}
            className="rounded-sm text-primary underline-offset-4 focus-ring hover:underline"
          >
            Open the last run
          </Link>
        </>
      ) : null}
    </span>
  );
}

function Detail({ label, testId, children }: { label: string; testId: string; children: ReactNode }) {
  return (
    <div className="grid min-w-0 gap-0.5" data-testid={testId}>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="min-w-0 wrap-anywhere">{children}</dd>
    </div>
  );
}
