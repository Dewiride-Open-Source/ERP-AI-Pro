import type { PagedResponseOfChangeRecordResponse } from "@dewiride/erp-api-client";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { HistoryIcon } from "lucide-react";
import { redirect, unstable_rethrow } from "next/navigation";

import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { listApiParameters, type ListApiParameters } from "@/shared/lists/list-api";
import { listHref, readListQuery, withPage, type SearchParameters } from "@/shared/lists/list-query";

import { auditNavigation } from "../../nav";
import { changesList } from "../lists/changes.list";
import { getChanges } from "../server/queries";

import { ChangeFeed } from "./change-feed";
import { ExportButton } from "./export-button";

const changesHeadingId = "audit-changes-heading";

type ChangesRead =
  | { readonly status: "read"; readonly changes: PagedResponseOfChangeRecordResponse }
  | { readonly status: "failed"; readonly reason: unknown };

export async function AuditTrailOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { basePath } = auditNavigation;
  const { query, canonical } = readListQuery(searchParameters, changesList);
  if (!canonical) redirect(listHref(basePath, query, changesList));

  const session = await requireSession();
  const canExport = mayShow(session, "platform.audit.changes.export");
  const parameters = listApiParameters(query, changesList);
  const read = await readChanges(parameters);
  if (read.status === "read") {
    const totalPages = read.changes.totalPages ?? 0;
    if (query.page > 1 && query.page > totalPages) {
      redirect(listHref(basePath, withPage(query, Math.max(1, totalPages)), changesList));
    }
  }

  return (
    <div className="grid gap-section">
      <header>
        <p className="text-eyebrow text-primary uppercase">Platform</p>
        <h1 className="text-title">Audit trail</h1>
      </header>

      <Card data-testid="audit-changes-card">
        <CardHeader>
          <CardTitle>
            <h2 id={changesHeadingId} className="flex items-center gap-2">
              <HistoryIcon className="size-4 text-muted-foreground" aria-hidden />
              Changes
            </h2>
          </CardTitle>
          <CardDescription>
            Who changed people, departments, designations, positions and files, and when. Times are in India
            Standard Time.
          </CardDescription>
          {canExport && read.status === "read" ? (
            <ExportButton
              key={parameters.filter ?? ""}
              filter={parameters.filter}
              totalCount={read.changes.totalCount ?? 0}
            />
          ) : null}
        </CardHeader>
        <CardContent>
          {read.status === "read" ? (
            <ChangeFeed query={query} changes={read.changes} labelledBy={changesHeadingId} />
          ) : (
            <Unavailable reason={describeFailure(read.reason)} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

async function readChanges(parameters: ListApiParameters): Promise<ChangesRead> {
  try {
    return { status: "read", changes: await getChanges(parameters) };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed", reason: error };
  }
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function Unavailable({ reason }: { reason: string }) {
  return (
    <div
      role="status"
      data-testid="audit-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">The audit trail is not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
    </div>
  );
}
