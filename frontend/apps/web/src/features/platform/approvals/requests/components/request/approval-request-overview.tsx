import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { HistoryIcon, ListChecksIcon, ListOrderedIcon } from "lucide-react";
import Link from "next/link";
import { notFound } from "next/navigation";
import type { ReactNode } from "react";

import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { formatDateTimeIst } from "@/shared/format/dates";

import { approvalsNavigation } from "../../../nav";
import { getApprovalRequest } from "../../server/queries";
import { LateBadge, RequestStanding, RequestStateBadge } from "../request-badges";
import type { RequestRow } from "../request-rows";
import { closedOnText, dueByText } from "../request-text";

import { inPlaceNotice } from "./decision-text";
import { RequestActions } from "./request-actions";
import { actionSubjectOf, requestDetailOf, type EarlierRequestItem } from "./request-detail";
import { requestTitle } from "./request-page-title";
import { RequestSteps } from "./request-steps";

const statusLineId = "approval-request-status-line";

const stepsHeadingId = "approval-request-steps-heading";

const earlierHeadingId = "approval-request-earlier-heading";

export async function ApprovalRequestOverview({ requestId }: { requestId: string }) {
  const read = await getApprovalRequest(requestId);
  if (read.status === "missing") notFound();
  if (read.status === "failed") return <RequestUnavailable reason={describeFailure(read.reason)} />;

  const session = await requireSession();
  const viewer = session.status === "signed-in" ? session.person.id : undefined;
  const detail = requestDetailOf(read.request, viewer);
  if (detail === undefined)
    return <RequestUnavailable reason="The API sent this request without its details." />;

  const { request } = detail;
  const rights = {
    decide: detail.canDecide && mayShow(session, "platform.approvals.requests.decide"),
    withdraw: detail.canWithdraw && mayShow(session, "platform.approvals.requests.withdraw"),
  };
  const statusDetail =
    request.waiting ?? (request.closedAt === undefined ? undefined : closedOnText(request.closedAt));

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section" data-testid="approval-request">
      <header className="grid gap-4">
        <div className="grid gap-1">
          <p className="text-eyebrow text-primary uppercase">{approvalsNavigation.title}</p>
          <h1 className="text-title wrap-anywhere">{request.kindTitle}</h1>
          {request.subjectName === undefined ? null : (
            <p
              className="text-body wrap-anywhere text-muted-foreground"
              data-testid="approval-request-record"
            >
              {request.subjectName}
            </p>
          )}
          <p
            id={statusLineId}
            tabIndex={-1}
            className="flex flex-wrap items-center gap-x-2 gap-y-1 outline-none"
            data-testid="approval-request-status-line"
          >
            <RequestStanding state={request.state} late={request.late} />
            {statusDetail === undefined ? null : (
              <>
                {" "}
                <span className="text-sm text-muted-foreground">{statusDetail}</span>
              </>
            )}
          </p>
        </div>
        {rights.decide && detail.decidesInPlace && detail.waitingApprover !== undefined ? (
          <p
            className="max-w-prose rounded-lg border border-info/40 bg-info/10 p-3 text-sm text-info dark:bg-info/20"
            data-testid="approval-in-place-notice"
          >
            {inPlaceNotice(detail.waitingApprover)}
          </p>
        ) : null}
        <RequestActions subject={actionSubjectOf(detail)} rights={rights} focusAfterId={statusLineId} />
      </header>

      <RequestFacts request={request} />

      <Card data-testid="approval-request-steps">
        <CardHeader>
          <CardTitle>
            <h2 id={stepsHeadingId} className="flex items-center gap-2">
              <ListOrderedIcon className="size-4 text-muted-foreground" aria-hidden />
              Steps
            </h2>
          </CardTitle>
          <CardDescription>
            The steps are decided one after another. Times are in India Standard Time.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <RequestSteps steps={detail.steps} requestState={request.state} labelledBy={stepsHeadingId} />
        </CardContent>
      </Card>

      {detail.earlier.length === 0 ? null : (
        <Card data-testid="approval-earlier-requests">
          <CardHeader>
            <CardTitle>
              <h2 id={earlierHeadingId} className="flex items-center gap-2">
                <HistoryIcon className="size-4 text-muted-foreground" aria-hidden />
                Earlier requests for this record
              </h2>
            </CardTitle>
            <CardDescription>
              The requests of this kind asked for this record before this one, the newest first.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <EarlierRequests
              earlier={detail.earlier}
              linked={mayShow(session, "platform.approvals.requests.read-all")}
              labelledBy={earlierHeadingId}
            />
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function RequestFacts({ request }: { request: RequestRow }) {
  const askedBy =
    request.submittedBy === undefined
      ? request.askedBy
      : `${request.askedBy}, submitted by ${request.submittedBy}`;

  return (
    <Card data-testid="approval-request-details">
      <CardHeader>
        <CardTitle>
          <h2 className="flex items-center gap-2">
            <ListChecksIcon className="size-4 text-muted-foreground" aria-hidden />
            Details
          </h2>
        </CardTitle>
        <CardDescription>Who asked for this approval and when, in India Standard Time.</CardDescription>
      </CardHeader>
      <CardContent>
        <dl className="grid gap-4 sm:grid-cols-2">
          <Fact label="Asked by" testId="approval-request-asked-by">
            {askedBy}
          </Fact>
          <Fact label="Asked on" testId="approval-request-asked-on">
            {formatDateTimeIst(request.submittedAt)}
          </Fact>
          {request.dueAt === undefined ? null : (
            <Fact label="Due" testId="approval-request-due-by">
              <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
                {dueByText(request.dueAt)}
                {request.late ? (
                  <>
                    {" "}
                    <LateBadge />
                  </>
                ) : null}
              </span>
            </Fact>
          )}
          {request.closedAt === undefined ? null : (
            <Fact label="Closed" testId="approval-request-closed-on">
              {formatDateTimeIst(request.closedAt)}
            </Fact>
          )}
        </dl>
      </CardContent>
    </Card>
  );
}

function Fact({ label, testId, children }: { label: string; testId: string; children: ReactNode }) {
  return (
    <div className="min-w-0 rounded-lg border bg-muted/40 p-4" data-testid={testId}>
      <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</dt>
      <dd className="mt-1 text-base font-medium wrap-anywhere">{children}</dd>
    </div>
  );
}

// An earlier request is shown to a person who asked for it, decided it or may see every request, which the page cannot tell
// for another person, so only a person who may see every request is offered its page.
function EarlierRequests({
  earlier,
  linked,
  labelledBy,
}: {
  earlier: readonly EarlierRequestItem[];
  linked: boolean;
  labelledBy: string;
}) {
  return (
    <ul role="list" aria-labelledby={labelledBy} className="grid gap-2">
      {earlier.map((item) => {
        const asked = `Asked on ${formatDateTimeIst(item.submittedAt)}`;
        return (
          <li
            key={item.id}
            className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border p-3"
            data-testid="approval-earlier-request"
          >
            <RequestStateBadge state={item.state} />{" "}
            {linked ? (
              <Link
                href={`/platform/approvals/requests/${item.id}`}
                prefetch={false}
                className="rounded-sm font-medium text-primary underline-offset-4 focus-ring hover:underline"
              >
                {asked}
              </Link>
            ) : (
              <span className="font-medium">{asked}</span>
            )}
            {item.closedAt === undefined ? null : (
              <>
                {" "}
                <span className="text-sm text-muted-foreground">closed {closedOnText(item.closedAt)}</span>
              </>
            )}
          </li>
        );
      })}
    </ul>
  );
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function RequestUnavailable({ reason }: { reason: string }) {
  return (
    <div className="grid gap-section">
      <header className="grid gap-1">
        <p className="text-eyebrow text-primary uppercase">{approvalsNavigation.title}</p>
        <h1 className="text-title">{requestTitle}</h1>
      </header>
      <div
        role="status"
        data-testid="approval-request-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">This request&apos;s details are not available.</p>
        <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
      </div>
    </div>
  );
}
