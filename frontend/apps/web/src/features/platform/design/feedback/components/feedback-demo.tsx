import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import Link from "next/link";
import { redirect } from "next/navigation";

import { listHref, readListQuery, withPage, type SearchParameters } from "@/shared/lists/list-query";

import { approvalsList } from "../lists/approvals.list";
import { approvalRequests, getApprovalRequests } from "../server/queries";

import { ApprovalsList } from "./approvals-list";

import { ConfirmDemo } from "./confirm-demo";
import { FailureDemo } from "./failure-demo";
import { RemindersDemo } from "./reminders-demo";
import { RestoreApprovals } from "./restore-approvals";
import { RetryDemo } from "./retry-demo";

const basePath = "/design/feedback";

const approvalsHeadingId = "feedback-approvals-heading";

const remindersHeadingId = "feedback-reminders-heading";

const designPages = [
  { href: "/design/feedback/report", title: "Receivables ageing (takes three seconds)" },
  { href: "/design/form-kit", title: "Form kit" },
  { href: "/design/data-table", title: "Data table" },
  { href: "/design/kitchen-sink", title: "Kitchen sink" },
] as const;

export async function FeedbackDemo({ searchParameters }: { searchParameters: SearchParameters }) {
  const { query, canonical } = readListQuery(searchParameters, approvalsList);
  if (!canonical) redirect(listHref(basePath, query, approvalsList));

  const approvals = await getApprovalRequests(query);
  if (query.page > 1 && query.page > approvals.totalPages) {
    redirect(listHref(basePath, withPage(query, Math.max(1, approvals.totalPages)), approvalsList));
  }

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">Platform</p>
        <h1 className="text-title">Feedback</h1>
        <p className="max-w-prose text-body text-muted-foreground">
          How the ERP asks before it acts, says what happened, recovers from a failure and moves things on
          screen. Nothing here is saved.
        </p>
      </header>

      <Card data-testid="feedback-confirm">
        <CardHeader>
          <CardTitle>
            <h2>Ask before acting</h2>
          </CardTitle>
          <CardDescription>
            A dialog confirms an action that is hard to undo, and a toast says what happened once it is done.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ConfirmDemo />
        </CardContent>
      </Card>

      <Card data-testid="feedback-approvals">
        <CardHeader>
          <CardTitle>
            <h2 id={approvalsHeadingId} tabIndex={-1} className="outline-none">
              Approval requests (example)
            </h2>
          </CardTitle>
          <CardDescription>
            Dismissing a request takes it off the list at once; when the server refuses, it comes back with
            the reason. Dismissals are kept for this browser only.
          </CardDescription>
          {approvals.totalCount < approvalRequests.length ? (
            <CardAction>
              <RestoreApprovals />
            </CardAction>
          ) : null}
        </CardHeader>
        <CardContent>
          <ApprovalsList
            query={query}
            page={{
              page: approvals.page,
              pageSize: approvals.pageSize,
              totalCount: approvals.totalCount,
              pageCount: approvals.totalPages,
            }}
            approvals={approvals.items}
            labelledBy={approvalsHeadingId}
          />
        </CardContent>
      </Card>

      <Card data-testid="feedback-reminders">
        <CardHeader>
          <CardTitle>
            <h2 id={remindersHeadingId}>Reminders (example)</h2>
          </CardTitle>
          <CardDescription>
            Items grow into the list when added and shrink out when removed, without moving when reduced
            motion is requested.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <RemindersDemo labelledBy={remindersHeadingId} />
        </CardContent>
      </Card>

      <Card data-testid="feedback-retry">
        <CardHeader>
          <CardTitle>
            <h2>Recover from a failure</h2>
          </CardTitle>
          <CardDescription>
            A part of a page that could not load says why, gives a reference and offers to try again.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <RetryDemo />
          <p className="text-body text-muted-foreground">
            When the whole page fails, the app shell shows the same state in place of the page.
          </p>
          <FailureDemo />
        </CardContent>
      </Card>

      <Card data-testid="feedback-navigation">
        <CardHeader>
          <CardTitle>
            <h2>Loading and page entrances</h2>
          </CardTitle>
          <CardDescription>
            While a page is prepared its loading status shows, and the page fades in when it arrives. The
            report takes three seconds, long enough to see its loading status.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ul className="flex flex-wrap gap-3">
            {designPages.map((page) => (
              <li key={page.href} className="text-body">
                <Link
                  href={page.href}
                  className="rounded-sm font-medium text-primary underline-offset-4 focus-ring hover:underline"
                >
                  {page.title}
                </Link>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </div>
  );
}
