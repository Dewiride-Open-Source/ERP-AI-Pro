import type { Permission, PersonResponse } from "@dewiride/erp-api-client";
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
import { PersonStatusBadge } from "../../people/components/person-status-badge";
import { deactivationNote } from "../../people/components/person-text";
import { PersonRoles } from "../../roles/components/person-roles";
import { getPerson, getPositions, getTeam } from "../server/queries";

import { isOwnRecord } from "./person-action-text";
import { PersonActions, type TeamRead } from "./person-actions";
import { PersonSummary } from "./person-summary";
import { PositionHistory, type MoveChoicesRead, type MovesRead } from "./position-history";

const personTitle = "Person";

const teamHandoverPermissions: readonly Permission[] = [
  "identity.users.people.change-status",
  "identity.users.positions.read",
  "identity.users.positions.record",
  "identity.users.positions.withdraw",
];

export async function PersonOverview({ userId }: { userId: string }) {
  const read = await getPerson(userId);
  if (read.status === "missing") notFound();
  const session = await requireSession();
  if (read.status === "failed") return <PersonUnavailable reason={describeFailure(read.reason)} />;

  const { person } = read;
  const id = person.id ?? userId;
  const name = person.displayName || personTitle;
  const may = (permission: Permission) => mayShow(session, permission);
  const ownRecord = ownRecordOf(person, session);
  const [moves, choices, flags, team] = await Promise.all([
    may("identity.users.positions.read") ? readMoves(id) : undefined,
    may("identity.users.positions.record") ? readMoveChoices() : undefined,
    getFeatureFlags(),
    !ownRecord && teamHandoverPermissions.every(may) ? readTeam(id) : undefined,
  ]);
  const showsHistory =
    may(auditNavigation.permission) && isFeatureEnabled(flags, auditNavigation.featureFlag);
  const deactivatedBecause =
    person.status === "deactivated" ? (person.deactivatedBecause ?? undefined) : undefined;
  const deactivation = deactivationNote(deactivatedBecause);

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
                <PersonStatusBadge status={person.status} testId="person-status" />
              </>
            ) : null}
          </p>
          {deactivation === undefined ? null : (
            <p className="text-sm text-muted-foreground" data-testid="person-deactivation-reason">
              {deactivation}
            </p>
          )}
        </div>
        <PersonActions
          personId={id}
          personName={name}
          status={person.status ?? undefined}
          deactivatedBecause={deactivatedBecause}
          version={person.version ?? undefined}
          ownRecord={ownRecord}
          team={team}
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

async function readTeam(personId: string): Promise<TeamRead> {
  try {
    return { status: "read", members: await getTeam(personId) };
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
