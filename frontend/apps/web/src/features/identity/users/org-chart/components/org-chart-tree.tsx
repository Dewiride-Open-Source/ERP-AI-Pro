"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { ListChevronsDownUpIcon, ListChevronsUpDownIcon } from "lucide-react";
import Link from "next/link";

import { DisclosureButton, DisclosureSpacer } from "../../organisation/components/disclosure-button";
import { branchIds } from "../../organisation/components/disclosure-tree";
import { useDisclosureTree, type DisclosureTree } from "../../organisation/hooks/use-disclosure-tree";

import { initiallyExpanded, type ChartNode } from "./org-chart";
import {
  managerOutsideNote,
  offChartExplanation,
  positionText,
  reportCountLabel,
  reportsListLabel,
  showReportsLabel,
} from "./org-chart-text";

type PersonLinks = {
  readonly linksPeople: boolean;
  readonly departmentName: string | undefined;
};

export function OrgChartTree({
  roots,
  linksPeople,
  departmentName,
  headingId,
  pending,
}: {
  roots: readonly ChartNode[];
  linksPeople: boolean;
  departmentName: string | undefined;
  headingId: string;
  pending: boolean;
}) {
  const hydrated = useHydrated();
  const tree = useDisclosureTree(roots, initiallyExpanded(roots));
  const branches = branchIds(roots).length > 0;
  const offChart = roots.some((root) => root.managerOutside === "off-chart");
  const links: PersonLinks = { linksPeople, departmentName };

  return (
    <div className="grid min-w-0 gap-3">
      {branches ? (
        <div className="flex flex-wrap items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={!hydrated}
            data-hydrating={hydrated ? undefined : ""}
            data-testid="org-chart-expand-all"
            onClick={tree.expandAll}
          >
            <ListChevronsUpDownIcon data-icon="inline-start" aria-hidden />
            Expand all
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={!hydrated}
            data-hydrating={hydrated ? undefined : ""}
            data-testid="org-chart-collapse-all"
            onClick={tree.collapseAll}
          >
            <ListChevronsDownUpIcon data-icon="inline-start" aria-hidden />
            Collapse all
          </Button>
        </div>
      ) : null}
      <ul
        role="list"
        aria-labelledby={headingId}
        aria-busy={pending || undefined}
        data-pending={pending || undefined}
        data-testid="org-chart-people"
        className="grid min-w-0 gap-2 transition-opacity data-pending:opacity-60"
      >
        {roots.map((node) => (
          <ChartPersonItem key={node.id} node={node} tree={tree} links={links} />
        ))}
      </ul>
      {offChart ? (
        <p className="text-sm text-muted-foreground" data-testid="org-chart-off-chart-note">
          {offChartExplanation}
        </p>
      ) : null}
    </div>
  );
}

function ChartPersonItem({
  node,
  tree,
  links,
}: {
  node: ChartNode;
  tree: DisclosureTree;
  links: PersonLinks;
}) {
  const reportsListId = `org-chart-${node.id}-reports`;
  const expanded = tree.isExpanded(node.id);
  const note = managerOutsideNote(node, links.departmentName);
  const reports = node.children.length;

  return (
    <li className="grid min-w-0 gap-2" data-testid="org-chart-person">
      <div
        className="flex min-w-0 items-start gap-2 rounded-lg border bg-card p-2"
        data-testid="org-chart-row"
      >
        {reports > 0 ? (
          <DisclosureButton
            expanded={expanded}
            controls={reportsListId}
            label={showReportsLabel(node.name)}
            onToggle={() => tree.toggle(node.id)}
            testId="org-chart-toggle"
          />
        ) : (
          <DisclosureSpacer />
        )}
        <div className="grid min-w-0 gap-0.5 py-0.5">
          <p className="font-medium wrap-anywhere" data-testid="org-chart-name">
            {links.linksPeople ? (
              <Link
                href={`/identity/users/${node.id}`}
                className="rounded-sm text-primary underline-offset-4 focus-ring hover:underline"
              >
                {node.name}
              </Link>
            ) : (
              node.name
            )}
          </p>
          <p className="text-sm wrap-anywhere text-muted-foreground">{positionText(node)}</p>
          {reports > 0 ? <p className="text-sm text-muted-foreground">{reportCountLabel(reports)}</p> : null}
          {note === undefined ? null : (
            <p className="text-sm wrap-anywhere text-muted-foreground" data-testid="org-chart-manager-note">
              {note}
            </p>
          )}
        </div>
      </div>
      {reports > 0 ? (
        <ul
          id={reportsListId}
          role="list"
          aria-label={reportsListLabel(node.name)}
          hidden={!expanded}
          className="ml-3.5 grid min-w-0 gap-2 border-l pl-3.5"
        >
          {node.children.map((child) => (
            <ChartPersonItem key={child.id} node={child} tree={tree} links={links} />
          ))}
        </ul>
      ) : null}
    </li>
  );
}
