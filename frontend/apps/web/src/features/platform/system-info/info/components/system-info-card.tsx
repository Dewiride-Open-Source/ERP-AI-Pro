import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { ActivityIcon, ServerIcon } from "lucide-react";
import type { ReactNode } from "react";

import { ApiError } from "@/shared/api/problem-details";
import { formatDateTimeIst } from "@/shared/format/dates";
import { formatDuration } from "@/shared/format/durations";

import { getSystemInfo, type SystemInfo } from "../server/queries";

import { RefreshButton } from "./refresh-button";

export async function SystemInfoCard() {
  let info: SystemInfo | undefined;
  let failure: string | undefined;

  try {
    info = await getSystemInfo();
  } catch (error) {
    failure = error instanceof ApiError ? error.message : "The API did not respond.";
  }

  return (
    <section className="grid animate-fade-up gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold tracking-widest text-primary uppercase">Platform</p>
          <h1 className="text-3xl font-semibold tracking-tight">System information</h1>
        </div>
        <RefreshButton />
      </header>

      <Card data-testid="system-info-card">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ServerIcon className="size-4 text-muted-foreground" aria-hidden />
            API
          </CardTitle>
          <CardDescription>Identity and uptime reported by the running API host.</CardDescription>
        </CardHeader>
        <CardContent>{info ? <InfoGrid info={info} /> : <Unavailable reason={failure} />}</CardContent>
      </Card>
    </section>
  );
}

function InfoGrid({ info }: { info: SystemInfo }) {
  return (
    <dl className="grid gap-4 sm:grid-cols-2">
      <Item label="Application" value={info.applicationName} testId="system-info-application" />
      <Item
        label="Version"
        value={<Badge variant="secondary">{info.version}</Badge>}
        testId="system-info-version"
      />
      <Item label="Started" value={formatDateTimeIst(info.startedAt)} testId="system-info-started" />
      <Item
        label="Uptime"
        value={
          <span className="inline-flex items-center gap-2">
            <ActivityIcon className="size-4 text-success" aria-hidden />
            {formatDuration(info.uptimeSeconds)}
          </span>
        }
        testId="system-info-uptime"
      />
    </dl>
  );
}

function Item({ label, value, testId }: { label: string; value: ReactNode; testId: string }) {
  return (
    <div className="rounded-lg border bg-muted/40 p-4" data-testid={testId}>
      <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</dt>
      <dd className="mt-1 text-base font-medium">{value}</dd>
    </div>
  );
}

function Unavailable({ reason }: { reason: string | undefined }) {
  return (
    <div
      role="status"
      data-testid="system-info-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">The API is not reachable.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason ?? "Start the API and refresh."}</p>
    </div>
  );
}
