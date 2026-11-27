import type { ApprovalState, ApprovalStepState } from "@dewiride/erp-api-client";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import { ClockAlertIcon } from "lucide-react";

import { approvalStateLabel, lateLabel, stepStateLabel } from "./request-text";

interface BadgeLook {
  readonly variant: "secondary" | "outline" | "destructive";
  readonly className?: string;
}

const waitingLook: BadgeLook = {
  variant: "outline",
  className: "border-transparent bg-info/10 text-info dark:bg-info/20",
};

const approvedLook: BadgeLook = {
  variant: "outline",
  className: "border-transparent bg-success/10 text-success dark:bg-success/20",
};

const sentBackLook: BadgeLook = {
  variant: "outline",
  className: "border-transparent bg-warning/10 text-warning dark:bg-warning/20",
};

const requestLooks: Readonly<Record<ApprovalState, BadgeLook>> = {
  submitted: waitingLook,
  approved: approvedLook,
  returned: sentBackLook,
  rejected: { variant: "destructive" },
  withdrawn: { variant: "secondary" },
};

const stepLooks: Readonly<Record<ApprovalStepState, BadgeLook>> = {
  pending: { variant: "secondary" },
  waiting: waitingLook,
  approved: approvedLook,
  returned: sentBackLook,
  rejected: { variant: "destructive" },
  closed: { variant: "secondary" },
};

export function RequestStateBadge({ state }: { state: ApprovalState }) {
  const { variant, className } = requestLooks[state];

  return (
    <Badge variant={variant} className={className} data-testid="approval-request-state-badge">
      {approvalStateLabel(state)}
    </Badge>
  );
}

export function StepStateBadge({ state }: { state: ApprovalStepState }) {
  const { variant, className } = stepLooks[state];

  return (
    <Badge variant={variant} className={className} data-testid="approval-step-state-badge">
      {stepStateLabel(state)}
    </Badge>
  );
}

export function LateBadge() {
  return (
    <Badge variant="destructive" data-testid="approval-late-badge">
      <ClockAlertIcon data-icon="inline-start" aria-hidden />
      {lateLabel}
    </Badge>
  );
}

// The badges sit in a line of their own, separated by spaces for assistive technology, which reads them as one phrase.
export function RequestStanding({
  state,
  late,
  testId,
}: {
  state: ApprovalState;
  late: boolean;
  testId?: string | undefined;
}) {
  return (
    <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1" data-testid={testId}>
      <RequestStateBadge state={state} />
      {late ? (
        <>
          {" "}
          <LateBadge />
        </>
      ) : null}
    </span>
  );
}
