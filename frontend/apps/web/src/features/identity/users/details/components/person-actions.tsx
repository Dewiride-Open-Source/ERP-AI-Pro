import type { PersonDeactivationReason, PersonStatus } from "@dewiride/erp-api-client";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { InfoIcon, PencilIcon } from "lucide-react";
import Link from "next/link";

import { DeleteAction } from "./delete-action";
import { ownRecordNote, teamUnreadable } from "./person-action-text";
import { StatusAction } from "./status-action";
import type { TeamMember } from "./team-handover-dialog";

export interface PersonActionRights {
  readonly edit: boolean;
  readonly changeStatus: boolean;
  readonly delete: boolean;
}

export type TeamRead =
  | { readonly status: "read"; readonly members: readonly TeamMember[] }
  | { readonly status: "failed"; readonly reason: string };

// The API refuses an administrator who deactivates or deletes their own record, so their own page explains that in place
// of the two buttons.
export function PersonActions({
  personId,
  personName,
  status,
  deactivatedBecause,
  version,
  ownRecord,
  team,
  rights,
}: {
  personId: string;
  personName: string;
  status: PersonStatus | undefined;
  deactivatedBecause: PersonDeactivationReason | undefined;
  version: string | undefined;
  ownRecord: boolean;
  team: TeamRead | undefined;
  rights: PersonActionRights;
}) {
  const offersStatusOrDeletion = rights.changeStatus || rights.delete;
  if (!rights.edit && !offersStatusOrDeletion) return null;
  const offersStatus = !ownRecord && rights.changeStatus && status !== undefined && version !== undefined;

  return (
    <div className="flex flex-wrap items-start gap-2" data-testid="person-actions">
      {rights.edit ? (
        <Button asChild variant="outline" size="sm">
          <Link href={`/identity/users/${personId}/edit`} data-testid="person-edit">
            <PencilIcon data-icon="inline-start" aria-hidden />
            Edit details
          </Link>
        </Button>
      ) : null}
      {offersStatusOrDeletion && ownRecord ? (
        <p className="flex items-center gap-2 text-sm text-muted-foreground" data-testid="person-own-record">
          <InfoIcon className="size-4 shrink-0" aria-hidden />
          {ownRecordNote}
        </p>
      ) : null}
      {offersStatus ? (
        <StatusAction
          personId={personId}
          personName={personName}
          status={status}
          version={version}
          deactivatedBecause={deactivatedBecause}
          team={team?.status === "read" ? team.members : undefined}
        />
      ) : null}
      {!ownRecord && rights.delete ? (
        <DeleteAction personId={personId} personName={personName} status={status} />
      ) : null}
      {offersStatus && team?.status === "failed" ? (
        <p
          role="status"
          className="basis-full text-sm text-muted-foreground"
          data-testid="person-team-unavailable"
        >
          {teamUnreadable(personName)} {team.reason}
        </p>
      ) : null}
    </div>
  );
}
