import type { PersonStatus } from "@dewiride/erp-api-client";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { InfoIcon, PencilIcon } from "lucide-react";
import Link from "next/link";

import { DeleteAction } from "./delete-action";
import { ownRecordNote } from "./person-action-text";
import { StatusAction } from "./status-action";

export interface PersonActionRights {
  readonly edit: boolean;
  readonly changeStatus: boolean;
  readonly delete: boolean;
}

// The API refuses an administrator who deactivates or deletes their own record, so their own page explains that in place
// of the two buttons.
export function PersonActions({
  personId,
  personName,
  status,
  version,
  ownRecord,
  rights,
}: {
  personId: string;
  personName: string;
  status: PersonStatus | undefined;
  version: string | undefined;
  ownRecord: boolean;
  rights: PersonActionRights;
}) {
  const offersStatusOrDeletion = rights.changeStatus || rights.delete;
  if (!rights.edit && !offersStatusOrDeletion) return null;

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
      {!ownRecord && rights.changeStatus && status !== undefined && version !== undefined ? (
        <StatusAction personId={personId} personName={personName} status={status} version={version} />
      ) : null}
      {!ownRecord && rights.delete ? <DeleteAction personId={personId} personName={personName} /> : null}
    </div>
  );
}
