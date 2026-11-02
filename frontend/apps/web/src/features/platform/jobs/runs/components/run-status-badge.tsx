import type { JobRunStatus } from "@dewiride/erp-api-client";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";

import { runStatusDetail, runStatusLabel, type RunState } from "./run-text";

interface BadgeLook {
  readonly variant: "secondary" | "outline" | "destructive";
  readonly className?: string;
}

const looks: Readonly<Record<JobRunStatus, BadgeLook>> = {
  pending: { variant: "secondary" },
  running: { variant: "outline", className: "border-transparent bg-info/10 text-info dark:bg-info/20" },
  failed: {
    variant: "outline",
    className: "border-transparent bg-warning/10 text-warning dark:bg-warning/20",
  },
  succeeded: {
    variant: "outline",
    className: "border-transparent bg-success/10 text-success dark:bg-success/20",
  },
  deadLettered: { variant: "destructive" },
  cancelled: { variant: "secondary" },
};

export function RunStatusBadge({ status }: { status: JobRunStatus }) {
  const { variant, className } = looks[status];

  return (
    <Badge variant={variant} className={className}>
      {runStatusLabel(status)}
    </Badge>
  );
}

export function RunStatus({ run, testId }: { run: RunState; testId?: string | undefined }) {
  const detail = runStatusDetail(run);

  return (
    <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1" data-testid={testId}>
      <RunStatusBadge status={run.status} />
      {detail === undefined ? null : (
        <>
          {" "}
          <span className="text-sm text-muted-foreground">{detail}</span>
        </>
      )}
    </span>
  );
}
