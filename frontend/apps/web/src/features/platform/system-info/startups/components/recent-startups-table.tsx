import type { StartupResponse } from "@dewiride/erp-api-client";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@dewiride/erp-ui/components/ui/table";
import { HistoryIcon } from "lucide-react";

import { ApiError } from "@/shared/api/problem-details";
import { formatDateTimeIst } from "@/shared/format/dates";

import { getRecentStartups } from "../server/queries";

const missing = "—";

export async function RecentStartupsTable() {
  let startups: StartupResponse[] | undefined;
  let failure: string | undefined;

  try {
    startups = (await getRecentStartups()).startups ?? [];
  } catch (error) {
    failure = error instanceof ApiError ? error.message : "The API did not respond.";
  }

  return (
    <Card className="animate-fade-up" data-testid="recent-startups-card">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <HistoryIcon className="size-4 text-muted-foreground" aria-hidden />
          Recent starts
        </CardTitle>
        <CardDescription>The 20 most recent starts of the API, newest first.</CardDescription>
      </CardHeader>
      <CardContent>
        {startups ? <StartupsTable startups={startups} /> : <Unavailable reason={failure} />}
      </CardContent>
    </Card>
  );
}

function StartupsTable({ startups }: { startups: StartupResponse[] }) {
  if (startups.length === 0) return <Empty />;

  return (
    <Table data-testid="recent-startups-table">
      <TableHeader>
        <TableRow>
          <TableHead>Started</TableHead>
          <TableHead>Version</TableHead>
          <TableHead>Framework</TableHead>
          <TableHead>Environment</TableHead>
          <TableHead>Recorded</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {startups.map((startup) => (
          <TableRow key={startup.id} data-testid="recent-startups-row">
            <TableCell>{startup.startedAt ? formatDateTimeIst(startup.startedAt) : missing}</TableCell>
            <TableCell>
              <Badge variant="secondary" data-testid="recent-startups-version">
                {startup.version ?? missing}
              </Badge>
            </TableCell>
            <TableCell>{startup.framework ?? missing}</TableCell>
            <TableCell>
              {startup.environmentName ?? missing}
              {startup.configurationLabel ? (
                <span className="ml-2 text-xs text-muted-foreground">{startup.configurationLabel}</span>
              ) : null}
            </TableCell>
            <TableCell>{startup.recordedAt ? formatDateTimeIst(startup.recordedAt) : missing}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function Empty() {
  return (
    <p
      role="status"
      data-testid="recent-startups-empty"
      className="rounded-lg border bg-muted/40 p-4 text-sm text-muted-foreground"
    >
      No starts have been recorded yet.
    </p>
  );
}

function Unavailable({ reason }: { reason: string | undefined }) {
  return (
    <div
      role="status"
      data-testid="recent-startups-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">Recent starts are not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason ?? "The API did not respond."}</p>
    </div>
  );
}
