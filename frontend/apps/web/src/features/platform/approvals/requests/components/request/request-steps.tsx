import type { ApprovalState } from "@dewiride/erp-api-client";
import { Timeline, TimelineContent, TimelineItem } from "@dewiride/erp-ui/components/timeline/timeline";

import { LateBadge, StepStateBadge } from "../request-badges";
import { dueSentence, stepCommentLabel, stepDaysText, stepSentence } from "../request-text";

import type { RequestStepItem } from "./request-detail";

export function RequestSteps({
  steps,
  requestState,
  labelledBy,
}: {
  steps: readonly RequestStepItem[];
  requestState: ApprovalState;
  labelledBy: string;
}) {
  return (
    <Timeline label="Steps" labelledBy={labelledBy} className="gap-0">
      {steps.map((step) => (
        <TimelineItem key={step.order} data-testid="approval-step" data-step={step.order}>
          <TimelineContent>
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <h3 className="font-medium wrap-anywhere" data-testid="approval-step-title">
                {step.order}. {step.title}
              </h3>{" "}
              <StepStateBadge state={step.state} />
              {step.late ? (
                <>
                  {" "}
                  <LateBadge />
                </>
              ) : null}
            </div>
            <p className="wrap-anywhere" data-testid="approval-step-summary">
              {stepSentence(step, requestState)}
            </p>
            {step.state === "waiting" && step.dueAt !== undefined ? (
              <p className="text-muted-foreground" data-testid="approval-step-due">
                {dueSentence(step.dueAt, step.late)}
              </p>
            ) : null}
            {step.state === "pending" ? (
              <p className="text-sm text-muted-foreground">{stepDaysText(step.workingDays)}</p>
            ) : null}
            {step.comment === undefined ? null : (
              <div
                className="grid gap-1 rounded-lg border bg-muted/40 p-3"
                data-testid="approval-step-comment"
              >
                <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
                  {stepCommentLabel(step.state)}
                </p>
                <p className="wrap-anywhere whitespace-pre-wrap">{step.comment}</p>
              </div>
            )}
          </TimelineContent>
        </TimelineItem>
      ))}
    </Timeline>
  );
}
