import type { DepartmentResponse, Permission } from "@dewiride/erp-api-client";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { Building2Icon } from "lucide-react";
import { unstable_rethrow } from "next/navigation";

import { auditNavigation } from "@/features/platform/audit";
import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { isFeatureEnabled } from "@/shared/feature-flags/feature-flags";
import { getFeatureFlags } from "@/shared/feature-flags/queries";

import { departmentsNavigation } from "../../nav";
import { listDepartments } from "../server/queries";

import { departmentRecords } from "./department-tree";
import { DepartmentsPanel } from "./departments-panel";

const departmentsHeadingId = "departments-heading";

type DepartmentsRead =
  | { readonly status: "read"; readonly departments: readonly DepartmentResponse[] }
  | { readonly status: "failed"; readonly reason: unknown };

export async function DepartmentsOverview() {
  const [session, read, flags] = await Promise.all([requireSession(), readDepartments(), getFeatureFlags()]);
  const may = (permission: Permission) => mayShow(session, permission);
  const showsHistory =
    may(auditNavigation.permission) && isFeatureEnabled(flags, auditNavigation.featureFlag);

  return (
    <div className="grid min-w-0 gap-section">
      <header className="grid gap-2">
        <div>
          <p className="text-eyebrow text-primary uppercase">{departmentsNavigation.area.title}</p>
          <h1 className="text-title">{departmentsNavigation.title}</h1>
        </div>
        <p className="max-w-prose text-body text-muted-foreground">
          The company&apos;s departments, each under the department it belongs to. A person&apos;s department
          comes from their moves.
        </p>
      </header>

      <Card data-testid="departments-card">
        <CardHeader>
          <CardTitle>
            <h2 id={departmentsHeadingId} tabIndex={-1} className="flex items-center gap-2 outline-none">
              <Building2Icon className="size-4 text-muted-foreground" aria-hidden />
              All departments
            </h2>
          </CardTitle>
          <CardDescription>
            Sorted by name at every level. Open a department to see the departments under it.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {read.status === "read" ? (
            <DepartmentsPanel
              records={departmentRecords(read.departments)}
              rights={{
                create: may("identity.users.departments.create"),
                update: may("identity.users.departments.update"),
                delete: may("identity.users.departments.delete"),
              }}
              showsHistory={showsHistory}
              headingId={departmentsHeadingId}
            />
          ) : (
            <Unavailable reason={describeFailure(read.reason)} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

async function readDepartments(): Promise<DepartmentsRead> {
  try {
    return { status: "read", departments: await listDepartments() };
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
      data-testid="departments-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">The departments are not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
    </div>
  );
}
