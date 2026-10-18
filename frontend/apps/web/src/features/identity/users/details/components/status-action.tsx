"use client";

import type { PersonDeactivationReason, PersonStatus } from "@dewiride/erp-api-client";
import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
import { ArrowRightLeftIcon, UserCheckIcon, UserXIcon } from "lucide-react";
import { useCallback, useLayoutEffect, useRef, useState, useTransition } from "react";
import { toast } from "sonner";

import { changePersonStatus } from "../server/actions";

import { PersonActionButton, PersonActionRefusalMessage, settlePersonAction } from "./person-action-controls";
import { handOverTeamAction, statusChange, type PersonActionRefusal } from "./person-action-text";
import { TeamHandoverDialog, type TeamMember } from "./team-handover-dialog";

const statusActionId = "person-status-action";

// A team is read only for someone who may hand it over, so without one the deactivation is the plain confirmation, whose
// refusal says when a team appeared since the page was read. Handing a team over changes the button the page shows, to
// Reactivate or to none at all, so focus moves to the status button once the page shows the change.
export function StatusAction({
  personId,
  personName,
  status,
  version,
  deactivatedBecause,
  team,
}: {
  personId: string;
  personName: string;
  status: PersonStatus;
  version: string;
  deactivatedBecause: PersonDeactivationReason | undefined;
  team: readonly TeamMember[] | undefined;
}) {
  const [pending, startChange] = useTransition();
  const [refusal, setRefusal] = useState<PersonActionRefusal>();
  const refocus = useRef(false);
  const change = statusChange(status, personName, deactivatedBecause);
  const teamSize = team?.length ?? 0;

  useLayoutEffect(() => {
    if (!refocus.current) return;
    refocus.current = false;
    document.getElementById(statusActionId)?.focus();
  }, [status, teamSize]);

  const handedOver = useCallback(() => {
    refocus.current = true;
  }, []);

  const confirm = () => {
    setRefusal(undefined);
    startChange(async () => {
      const outcome = await settlePersonAction(() => changePersonStatus(personId, change.to, version));
      if (outcome.done) toast.success(change.done);
      else setRefusal(outcome);
    });
  };

  const statusButton = (
    <PersonActionButton
      id={statusActionId}
      pending={pending}
      icon={change.to === "deactivated" ? UserXIcon : UserCheckIcon}
      variant="outline"
      size="sm"
      data-testid="person-status-action"
    >
      {change.action}
    </PersonActionButton>
  );

  return (
    <>
      <div className="grid justify-items-start gap-1">
        {status === "active" && team !== undefined && teamSize > 0 ? (
          <TeamHandoverDialog
            mode="deactivate"
            personId={personId}
            personName={personName}
            version={version}
            team={team}
            trigger={statusButton}
            onHandedOver={handedOver}
          />
        ) : (
          <ConfirmDialog
            trigger={statusButton}
            title={change.title}
            description={change.description}
            cancelLabel={change.cancel}
            confirmLabel={change.confirm}
            tone={change.tone}
            onConfirm={confirm}
          />
        )}
        {refusal ? <PersonActionRefusalMessage refusal={refusal} testId="person-status-error" /> : null}
      </div>
      {status === "deactivated" && team !== undefined && teamSize > 0 ? (
        <TeamHandoverDialog
          mode="hand-over"
          personId={personId}
          personName={personName}
          version={version}
          team={team}
          trigger={
            <PersonActionButton
              pending={false}
              icon={ArrowRightLeftIcon}
              variant="outline"
              size="sm"
              data-testid="person-hand-over-team"
            >
              {handOverTeamAction}
            </PersonActionButton>
          }
          onHandedOver={handedOver}
        />
      ) : null}
    </>
  );
}
