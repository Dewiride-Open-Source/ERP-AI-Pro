"use client";

import type { DataTableColumn, DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";
import { CheckIcon, LockIcon } from "lucide-react";
import { useMemo } from "react";
import { toast } from "sonner";

import type { ListQuery } from "@/shared/lists/list-query";
import { useRemoveRow } from "@/shared/lists/list-row-removal";
import { ListTable } from "@/shared/lists/list-table";

import { approvalsList } from "../lists/approvals.list";
import { dismissApproval } from "../server/actions";
import type { ApprovalRequest } from "../server/queries";

const basePath = "/design/feedback";

function DismissApproval({ approval, focusAfterId }: { approval: ApprovalRequest; focusAfterId: string }) {
  const removeRow = useRemoveRow();

  const dismiss = () => {
    document.getElementById(focusAfterId)?.focus();
    void removeRow(approval.id, () => dismissApproval(approval.id)).then((outcome) => {
      if (outcome.removed) {
        toast.success(`Dismissed “${approval.request}”.`);
        return;
      }
      toast.error(`“${approval.request}” was not dismissed.`, { description: outcome.message });
    });
  };

  return (
    <Button
      type="button"
      variant="ghost"
      size="sm"
      aria-label={`Dismiss ${approval.request}`}
      onClick={dismiss}
    >
      <CheckIcon aria-hidden="true" />
      <span className="max-sm:sr-only">Dismiss</span>
    </Button>
  );
}

function approvalColumns(focusAfterId: string): readonly DataTableColumn<ApprovalRequest>[] {
  return [
    {
      id: "request",
      header: "Request",
      cell: (approval) => (
        <span className="flex flex-wrap items-center gap-2 font-medium whitespace-normal">
          {approval.request}
          {approval.locked ? (
            <Badge variant="outline">
              <LockIcon aria-hidden="true" />
              Locked
            </Badge>
          ) : null}
        </span>
      ),
      hideable: false,
      role: "title",
    },
    {
      id: "requestedBy",
      header: "Requested by",
      cell: (approval) => approval.requestedBy,
      sort: "text",
    },
    {
      id: "raisedOn",
      header: "Raised on",
      cell: (approval) => <span className="tabular-nums">{formatDisplayDate(approval.raisedOn)}</span>,
      sort: "date",
    },
    {
      id: "actions",
      header: "Actions",
      role: "actions",
      align: "end",
      cell: (approval) => <DismissApproval approval={approval} focusAfterId={focusAfterId} />,
    },
  ];
}

export function ApprovalsList({
  query,
  page,
  approvals,
  labelledBy,
}: {
  query: ListQuery;
  page: DataTablePage;
  approvals: readonly ApprovalRequest[];
  labelledBy: string;
}) {
  const columns = useMemo(() => approvalColumns(labelledBy), [labelledBy]);

  return (
    <ListTable
      basePath={basePath}
      definition={approvalsList}
      query={query}
      page={page}
      label="Approval requests"
      labelledBy={labelledBy}
      noun={{ one: "request", other: "requests" }}
      columns={columns}
      rows={approvals}
      getRowId={(approval) => approval.id}
      noMatches="No requests match."
      empty={
        <Empty className="border" data-testid="approvals-empty">
          <EmptyHeader>
            <EmptyTitle>No requests are waiting.</EmptyTitle>
            <EmptyDescription>Restore the requests to try again.</EmptyDescription>
          </EmptyHeader>
        </Empty>
      }
    />
  );
}
