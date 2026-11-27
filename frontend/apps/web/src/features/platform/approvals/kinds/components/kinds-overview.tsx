import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { LayersIcon } from "lucide-react";

import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";

import { approvalsNavigation } from "../../nav";
import { kindsPage } from "../../requests/components/approval-pages";
import { listApprovalKinds } from "../server/queries";

import { requiresApproval, type ApprovalKindItem } from "./kind-items";
import { KindSettingAction } from "./kind-setting-action";
import {
  approvalOffLabel,
  approvalText,
  needsApprovalLabel,
  stepRuleText,
  workingDaysSentence,
} from "./kind-text";

const listHeadingId = "approval-kinds-heading";

export const kindsTitle = kindsPage.title;

export async function KindsOverview() {
  const [session, read] = await Promise.all([requireSession(), listApprovalKinds()]);
  const canChange = mayShow(session, "platform.approvals.kinds.change");

  return (
    <div className="grid min-w-0 gap-section" data-testid="approval-kinds">
      <header className="grid gap-2">
        <div>
          <p className="text-eyebrow text-primary uppercase">{approvalsNavigation.title}</p>
          <h1 className="text-title">{kindsPage.title}</h1>
        </div>
        <p className="max-w-prose text-body text-muted-foreground">{kindsPage.description}</p>
      </header>

      <Card data-testid="approval-kinds-card">
        <CardHeader>
          <CardTitle>
            <h2 id={listHeadingId} className="flex items-center gap-2">
              <LayersIcon className="size-4 text-muted-foreground" aria-hidden />
              {kindsPage.listHeading}
            </h2>
          </CardTitle>
          <CardDescription>{kindsPage.listDescription}</CardDescription>
        </CardHeader>
        <CardContent>
          {read.status === "failed" ? (
            <div
              role="status"
              data-testid="approval-kinds-unavailable"
              className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
            >
              <p className="font-medium">The kinds of request are not available.</p>
              <p className="mt-1 text-sm text-muted-foreground">{read.reason}</p>
            </div>
          ) : read.kinds.length === 0 ? (
            <Empty className="border" data-testid="approval-kinds-empty">
              <EmptyHeader>
                <EmptyTitle>{kindsPage.empty.title}</EmptyTitle>
                <EmptyDescription>{kindsPage.empty.description}</EmptyDescription>
              </EmptyHeader>
            </Empty>
          ) : (
            <ul
              role="list"
              aria-labelledby={listHeadingId}
              className="grid gap-4"
              data-testid="approval-kinds-list"
            >
              {read.kinds.map((kind) => (
                <KindCard key={kind.key} kind={kind} canChange={canChange} />
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function KindCard({ kind, canChange }: { kind: ApprovalKindItem; canChange: boolean }) {
  const required = requiresApproval(kind);

  return (
    <li className="grid min-w-0 gap-3 rounded-lg border p-4" data-testid="approval-kind" data-kind={kind.key}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="grid min-w-0 gap-1">
          <h3 className="font-medium wrap-anywhere" data-testid="approval-kind-title">
            {kind.title}
          </h3>
          <p className="text-sm text-muted-foreground">{kind.description}</p>
        </div>
        {canChange ? <KindSettingAction kind={kind} /> : null}
      </div>
      <p className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <Badge
          variant="outline"
          className={
            required
              ? "border-transparent bg-info/10 text-info dark:bg-info/20"
              : "border-transparent bg-warning/10 text-warning dark:bg-warning/20"
          }
          data-testid="approval-kind-standing"
        >
          {required ? needsApprovalLabel : approvalOffLabel}
        </Badge>{" "}
        <span className="text-sm text-muted-foreground" data-testid="approval-kind-working-days">
          {workingDaysSentence(kind)}
        </span>
      </p>
      <p className="text-sm">{approvalText(kind)}</p>
      <ol role="list" aria-label={`Steps of ${kind.title}`} className="grid gap-2">
        {kind.steps.map((step) => (
          <li
            key={step.order}
            className="grid gap-0.5 rounded-md bg-muted/40 p-3"
            data-testid="approval-kind-step"
          >
            <p className="font-medium wrap-anywhere">
              {step.order}. {step.title}
            </p>
            <p className="text-sm text-muted-foreground">{stepRuleText(step)}</p>
          </li>
        ))}
      </ol>
    </li>
  );
}
