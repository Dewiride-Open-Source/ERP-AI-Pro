import type { DesignationResponse, Permission } from "@dewiride/erp-api-client";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { IdCardIcon } from "lucide-react";
import { unstable_rethrow } from "next/navigation";

import { auditNavigation } from "@/features/platform/audit";
import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { isFeatureEnabled } from "@/shared/feature-flags/feature-flags";
import { getFeatureFlags } from "@/shared/feature-flags/queries";

import { designationsNavigation } from "../../nav";
import { listDesignations } from "../server/queries";

import { designationRecords } from "./designation-text";
import { DesignationsPanel } from "./designations-panel";

const designationsHeadingId = "designations-heading";

type DesignationsRead =
  | { readonly status: "read"; readonly designations: readonly DesignationResponse[] }
  | { readonly status: "failed"; readonly reason: unknown };

export async function DesignationsOverview() {
  const [session, read, flags] = await Promise.all([requireSession(), readDesignations(), getFeatureFlags()]);
  const may = (permission: Permission) => mayShow(session, permission);
  const showsHistory =
    may(auditNavigation.permission) && isFeatureEnabled(flags, auditNavigation.featureFlag);

  return (
    <div className="grid min-w-0 gap-section">
      <header className="grid gap-2">
        <div>
          <p className="text-eyebrow text-primary uppercase">{designationsNavigation.area.title}</p>
          <h1 className="text-title">{designationsNavigation.title}</h1>
        </div>
        <p className="max-w-prose text-body text-muted-foreground">
          The designations people can hold in the company. A person&apos;s designation comes from their moves.
        </p>
      </header>

      <Card data-testid="designations-card">
        <CardHeader>
          <CardTitle>
            <h2 id={designationsHeadingId} tabIndex={-1} className="flex items-center gap-2 outline-none">
              <IdCardIcon className="size-4 text-muted-foreground" aria-hidden />
              All designations
            </h2>
          </CardTitle>
          <CardDescription>Sorted by name.</CardDescription>
        </CardHeader>
        <CardContent>
          {read.status === "read" ? (
            <DesignationsPanel
              designations={designationRecords(read.designations)}
              rights={{
                create: may("identity.users.designations.create"),
                update: may("identity.users.designations.update"),
                delete: may("identity.users.designations.delete"),
              }}
              showsHistory={showsHistory}
              headingId={designationsHeadingId}
            />
          ) : (
            <Unavailable reason={describeFailure(read.reason)} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

async function readDesignations(): Promise<DesignationsRead> {
  try {
    return { status: "read", designations: await listDesignations() };
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
      data-testid="designations-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">The designations are not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
    </div>
  );
}
