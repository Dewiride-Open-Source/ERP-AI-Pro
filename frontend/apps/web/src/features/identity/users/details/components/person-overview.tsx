import type { Permission, PersonResponse, PersonStatus } from "@dewiride/erp-api-client";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import { notFound, unstable_rethrow } from "next/navigation";
import { Suspense } from "react";

import { auditNavigation, RecordHistory, RecordHistorySkeleton } from "@/features/platform/audit";
import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession, type HeldSession } from "@/shared/auth/session";
import { isFeatureEnabled } from "@/shared/feature-flags/feature-flags";
import { getFeatureFlags } from "@/shared/feature-flags/queries";
import { istDateKey } from "@/shared/format/dates";

import { listDepartmentOptions, listDesignationOptions } from "../../organisation/server/queries";
import { statusLabel } from "../../people/components/person-text";
import { PersonRoles } from "../../roles/components/person-roles";
import { getPerson, getPositions } from "../server/queries";

import { isOwnRecord } from "./person-action-text";
import { PersonActions } from "./person-actions";
import { PersonSummary } from "./person-summary";
import { PositionHistory, type MoveChoicesRead, type MovesRead } from "./position-history";

const personTitle = "Person";

export async function PersonOverview({ userId }: { userId: string }) {
  const read = await getPerson(userId);
  if (read.status === "missing") notFound();
  const session = await requireSession();
  if (read.status === "failed") return <PersonUnavailable reason={describeFailure(read.reason)} />;

  const { person } = read;
  const id = person.id ?? userId;
  const name = person.displayName || personTitle;
  const may = (permission: Permission) => mayShow(session, permission);
  const [moves, choices, flags] = await Promise.all([
    may("identity.users.positions.read") ? readMoves(id) : undefined,
    may("identity.users.positions.record") ? readMoveChoices() : undefined,
    getFeatureFlags(),
  ]);
  const showsHistory =
    may(auditNavigation.permission) && isFeatureEnabled(flags, auditNavigation.featureFlag);

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-4">
        <div className="grid gap-1">
          <p className="text-eyebrow text-primary uppercase">People</p>
          <h1 className="text-title wrap-anywhere">{name}</h1>
          <p className="flex flex-wrap items-center gap-2 text-muted-foreground">
            <span className="wrap-anywhere">{person.workEmail}</span>
            {person.status ? (
              <>
                {" "}
                <StatusBadge status={person.status} />
              </>
            ) : null}
          </p>
        </div>
        <PersonActions
          personId={id}
          personName={name}
          status={person.status ?? undefined}
          version={person.version ?? undefined}
          ownRecord={ownRecordOf(person, session)}
          rights={{
            edit: may("identity.users.people.update"),
            changeStatus: may("identity.users.people.change-status"),
            delete: may("identity.users.people.delete"),
          }}
        />
      </header>

      <PersonSummary person={person} />

      <PersonRoles
        personId={id}
        personName={name}
        roles={person.roles ?? []}
        version={person.version ?? undefined}
        canAssign={may("identity.users.people.assign-roles")}
      />

      {moves === undefined ? null : (
        <PositionHistory
          personId={id}
          personName={name}
          moves={moves}
          choices={choices}
          today={istDateKey(new Date())}
          canWithdraw={may("identity.users.positions.withdraw")}
        />
      )}

      {showsHistory ? (
        <Suspense fallback={<RecordHistorySkeleton />}>
          <RecordHistory recordId={id} recordName={name} />
        </Suspense>
      ) : null}
    </div>
  );
}

function StatusBadge({ status }: { status: PersonStatus }) {
  return status === "active" ? (
    <Badge
      variant="outline"
      className="border-transparent bg-success/10 text-success dark:bg-success/20"
      data-testid="person-status"
    >
      {statusLabel(status)}
    </Badge>
  ) : (
    <Badge variant="secondary" data-testid="person-status">
      {statusLabel(status)}
    </Badge>
  );
}

function ownRecordOf(person: PersonResponse, session: HeldSession): boolean {
  return session.status === "signed-in" && isOwnRecord(person.entraObjectId, session.person.id);
}

async function readMoves(personId: string): Promise<MovesRead> {
  try {
    return { status: "read", moves: await getPositions(personId) };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed", reason: describeFailure(error) };
  }
}

async function readMoveChoices(): Promise<MoveChoicesRead> {
  try {
    const [departments, designations] = await Promise.all([
      listDepartmentOptions(),
      listDesignationOptions(),
    ]);
    return { status: "read", departments, designations };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed", reason: describeFailure(error) };
  }
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function PersonUnavailable({ reason }: { reason: string }) {
  return (
    <div className="grid gap-section">
      <header className="grid gap-1">
        <p className="text-eyebrow text-primary uppercase">People</p>
        <h1 className="text-title">{personTitle}</h1>
      </header>
      <div
        role="status"
        data-testid="person-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">This person&apos;s details are not available.</p>
        <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
      </div>
    </div>
  );
}
