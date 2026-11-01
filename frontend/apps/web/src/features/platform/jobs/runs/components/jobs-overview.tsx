import type { JobResponse } from "@dewiride/erp-api-client";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { ListChecksIcon } from "lucide-react";
import { redirect, unstable_rethrow } from "next/navigation";

import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { listApiParameters } from "@/shared/lists/list-api";
import { listHref, readListQuery, withPage, type SearchParameters } from "@/shared/lists/list-query";

import { jobsNavigation } from "../../nav";
import { JobsCard, type JobsRead } from "../../schedules/components/jobs-card";
import { listJobs } from "../../schedules/server/queries";
import { runsList } from "../lists/runs.list";
import { getRuns } from "../server/queries";

import { runRows } from "./run-rows";
import { RunsList } from "./runs-list";

const runsHeadingId = "job-runs-heading";

export async function JobsOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { basePath } = jobsNavigation;
  const { query, canonical } = readListQuery(searchParameters, runsList);
  if (!canonical) redirect(listHref(basePath, query, runsList));

  const session = await requireSession();
  const rights = {
    rerun: mayShow(session, "platform.jobs.runs.rerun"),
    cancel: mayShow(session, "platform.jobs.runs.cancel"),
  };

  const [jobs, runs] = await Promise.allSettled([listJobs(), getRuns(listApiParameters(query, runsList))]);
  for (const result of [jobs, runs]) {
    if (result.status === "rejected") unstable_rethrow(result.reason);
  }
  if (runs.status === "fulfilled") {
    const totalPages = runs.value.totalPages ?? 0;
    if (query.page > 1 && query.page > totalPages) {
      redirect(listHref(basePath, withPage(query, Math.max(1, totalPages)), runsList));
    }
  }
  const jobsRead: JobsRead =
    jobs.status === "fulfilled"
      ? { status: "read", jobs: jobs.value }
      : { status: "failed", reason: describeFailure(jobs.reason) };

  return (
    <div className="grid gap-section">
      <header>
        <p className="text-eyebrow text-primary uppercase">{jobsNavigation.area.title}</p>
        <h1 className="text-title">Background jobs</h1>
      </header>

      <JobsCard read={jobsRead} />

      <Card data-testid="job-runs-card">
        <CardHeader>
          <CardTitle>
            <h2 id={runsHeadingId} tabIndex={-1} className="flex items-center gap-2 outline-none">
              <ListChecksIcon className="size-4 text-muted-foreground" aria-hidden />
              Runs
            </h2>
          </CardTitle>
          <CardDescription>
            Each run is one piece of work a job does, with every try it took. Times are in India Standard
            Time.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {runs.status === "fulfilled" ? (
            <RunsList
              query={query}
              page={{
                page: runs.value.page ?? query.page,
                pageSize: runs.value.pageSize ?? query.pageSize,
                totalCount: runs.value.totalCount ?? 0,
                pageCount: runs.value.totalPages ?? 0,
              }}
              runs={runRows(runs.value.items ?? [])}
              jobOptions={jobsRead.status === "read" ? jobOptions(jobsRead.jobs) : []}
              rights={rights}
              labelledBy={runsHeadingId}
            />
          ) : (
            <div
              role="status"
              data-testid="job-runs-unavailable"
              className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
            >
              <p className="font-medium">The runs are not available.</p>
              <p className="mt-1 text-sm text-muted-foreground">{describeFailure(runs.reason)}</p>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function jobOptions(jobs: readonly JobResponse[]): { value: string; label: string }[] {
  return jobs.flatMap((job) => (job.name ? [{ value: job.name, label: job.title || job.name }] : []));
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}
