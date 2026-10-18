"use client";

import { Button, buttonVariants } from "@dewiride/erp-ui/components/ui/button";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import {
  HistoryIcon,
  ListChevronsDownUpIcon,
  ListChevronsUpDownIcon,
  PencilIcon,
  PlusIcon,
} from "lucide-react";
import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { toast } from "sonner";

import { auditNavigation } from "@/features/platform/audit";

import {
  PersonActionButton,
  PersonActionRefusalMessage,
} from "../../details/components/person-action-controls";
import type { PersonActionRefusal } from "../../details/components/person-action-text";
import { useSavingDialog } from "../../details/hooks/use-saving-dialog";
import { DisclosureButton, DisclosureSpacer } from "../../organisation/components/disclosure-button";
import { branchIds } from "../../organisation/components/disclosure-tree";
import { useDisclosureTree, type DisclosureTree } from "../../organisation/hooks/use-disclosure-tree";
import { useFocusAfterRefresh } from "../hooks/use-focus-after-refresh";

import { DeleteDepartmentAction } from "./delete-department-action";
import {
  DepartmentDialog,
  type DepartmentDialogTarget,
  type DepartmentSaved,
  type EditableDepartment,
} from "./department-dialog";
import {
  addUnderLabel,
  addedMessage,
  childCountLabel,
  childListLabel,
  editLabel,
  historyLabel,
  savedMessage,
  showChildrenLabel,
} from "./department-text";
import { ancestorsOf, departmentTree, type DepartmentNode, type DepartmentRecord } from "./department-tree";

export interface DepartmentRights {
  readonly create: boolean;
  readonly update: boolean;
  readonly delete: boolean;
}

type RowActions = {
  readonly rights: DepartmentRights;
  readonly showsHistory: boolean;
  readonly headingId: string;
  readonly openAdd: (parentId: string | undefined, returnFocusTo: string) => void;
  readonly openEdit: (department: EditableDepartment, returnFocusTo: string) => void;
};

const addButtonId = "departments-add";

function rowButtonId(departmentId: string, action: "add" | "edit"): string {
  return `department-${departmentId}-${action}`;
}

export function DepartmentsPanel({
  records,
  rights,
  showsHistory,
  headingId,
}: {
  records: readonly DepartmentRecord[];
  rights: DepartmentRights;
  showsHistory: boolean;
  headingId: string;
}) {
  const hydrated = useHydrated();
  const nodes = useMemo(() => departmentTree(records), [records]);
  const tree = useDisclosureTree(nodes);
  const { open, saving, setOpen, setSaving, onOpenChange } = useSavingDialog();
  const [target, setTarget] = useState<DepartmentDialogTarget>();
  const [opening, setOpening] = useState(0);
  const keepFocusOn = useFocusAfterRefresh(records, !open);

  const show = useCallback(
    (next: DepartmentDialogTarget) => {
      setTarget(next);
      setOpening((count) => count + 1);
      setOpen(true);
    },
    [setOpen],
  );
  const { expand } = tree;
  const reveal = useCallback(
    (parentId: string | undefined) => {
      if (parentId !== undefined) expand([parentId, ...ancestorsOf(records, parentId)]);
    },
    [expand, records],
  );
  const added = useCallback(
    (saved: DepartmentSaved) => {
      if (target !== undefined) keepFocusOn(target.returnFocusTo);
      reveal(saved.parentId);
      setOpen(false);
      toast.success(addedMessage(saved.name));
    },
    [keepFocusOn, reveal, setOpen, target],
  );
  const edited = useCallback(
    (saved: DepartmentSaved) => {
      if (target !== undefined) keepFocusOn(target.returnFocusTo);
      reveal(saved.parentId);
      setOpen(false);
      toast.success(savedMessage(saved.name));
    },
    [keepFocusOn, reveal, setOpen, target],
  );

  const actions: RowActions = {
    rights,
    showsHistory,
    headingId,
    openAdd: (parentId, returnFocusTo) => show({ kind: "add", parentId, returnFocusTo }),
    openEdit: (department, returnFocusTo) => show({ kind: "edit", department, returnFocusTo }),
  };
  const branches = branchIds(nodes).length > 0;

  return (
    <div className="grid min-w-0 gap-4" data-testid="departments-panel">
      {rights.create || branches ? (
        <div className="flex flex-wrap items-center gap-2">
          {rights.create ? (
            <PersonActionButton
              id={addButtonId}
              pending={false}
              icon={PlusIcon}
              size="sm"
              aria-haspopup="dialog"
              data-testid="departments-add"
              onClick={() => actions.openAdd(undefined, addButtonId)}
            >
              Add a department
            </PersonActionButton>
          ) : null}
          {branches ? (
            <>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={!hydrated}
                data-hydrating={hydrated ? undefined : ""}
                data-testid="departments-expand-all"
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
                data-testid="departments-collapse-all"
                onClick={tree.collapseAll}
              >
                <ListChevronsDownUpIcon data-icon="inline-start" aria-hidden />
                Collapse all
              </Button>
            </>
          ) : null}
        </div>
      ) : null}

      {nodes.length === 0 ? (
        <Empty className="border" data-testid="departments-empty">
          <EmptyHeader>
            <EmptyTitle>No departments are set up yet.</EmptyTitle>
            {rights.create ? (
              <EmptyDescription>Add the first one with Add a department.</EmptyDescription>
            ) : null}
          </EmptyHeader>
        </Empty>
      ) : (
        <ul role="list" aria-labelledby={headingId} className="grid gap-2">
          {nodes.map((node) => (
            <DepartmentItem key={node.id} node={node} tree={tree} actions={actions} />
          ))}
        </ul>
      )}

      <DepartmentDialog
        target={target}
        opening={opening}
        open={open}
        saving={saving}
        onOpenChange={onOpenChange}
        onSavingChange={setSaving}
        records={records}
        fallbackFocusId={headingId}
        onAdded={added}
        onEdited={edited}
      />
    </div>
  );
}

function DepartmentItem({
  node,
  tree,
  actions,
}: {
  node: DepartmentNode;
  tree: DisclosureTree;
  actions: RowActions;
}) {
  const [refusal, setRefusal] = useState<PersonActionRefusal>();
  const childListId = `department-${node.id}-departments`;
  const expanded = tree.isExpanded(node.id);
  const { rights } = actions;
  const { version } = node;

  return (
    <li className="grid min-w-0 gap-2" data-testid="department">
      <div
        className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 rounded-lg border bg-card p-2"
        data-testid="department-row"
      >
        <div className="flex min-w-0 flex-1 items-center gap-2">
          {node.children.length > 0 ? (
            <DisclosureButton
              expanded={expanded}
              controls={childListId}
              label={showChildrenLabel(node.name)}
              onToggle={() => tree.toggle(node.id)}
              testId="department-toggle"
            />
          ) : (
            <DisclosureSpacer />
          )}
          <div className="min-w-0">
            <p className="font-medium wrap-anywhere" data-testid="department-name">
              {node.name}
            </p>
            {node.children.length > 0 ? (
              <p className="text-sm text-muted-foreground">{childCountLabel(node.children.length)}</p>
            ) : null}
          </div>
        </div>
        <div className="flex flex-wrap items-center justify-end gap-1">
          {rights.create ? (
            <PersonActionButton
              id={rowButtonId(node.id, "add")}
              pending={false}
              icon={PlusIcon}
              variant="ghost"
              size="sm"
              aria-label={addUnderLabel(node.name)}
              aria-haspopup="dialog"
              data-testid="department-add"
              onClick={() => actions.openAdd(node.id, rowButtonId(node.id, "add"))}
            >
              <span className="max-lg:sr-only">Add a department</span>
            </PersonActionButton>
          ) : null}
          {rights.update && version !== undefined ? (
            <PersonActionButton
              id={rowButtonId(node.id, "edit")}
              pending={false}
              icon={PencilIcon}
              variant="ghost"
              size="sm"
              aria-label={editLabel(node.name)}
              aria-haspopup="dialog"
              data-testid="department-edit"
              onClick={() =>
                actions.openEdit(
                  { id: node.id, name: node.name, parentId: node.parentId, version },
                  rowButtonId(node.id, "edit"),
                )
              }
            >
              <span className="max-lg:sr-only">Rename or move</span>
            </PersonActionButton>
          ) : null}
          {rights.delete ? (
            <DeleteDepartmentAction
              departmentId={node.id}
              name={node.name}
              focusAfterDeleteId={actions.headingId}
              onRefusal={setRefusal}
            />
          ) : null}
          {actions.showsHistory ? (
            <Link
              href={`${auditNavigation.basePath}?record=${node.id}`}
              aria-label={historyLabel(node.name)}
              className={buttonVariants({ variant: "ghost", size: "sm" })}
              data-testid="department-history"
            >
              <HistoryIcon data-icon="inline-start" aria-hidden />
              <span className="max-lg:sr-only">Change history</span>
            </Link>
          ) : null}
        </div>
        {refusal ? (
          <div className="basis-full">
            <PersonActionRefusalMessage refusal={refusal} testId="department-delete-error" />
          </div>
        ) : null}
      </div>
      {node.children.length > 0 ? (
        <ul
          id={childListId}
          role="list"
          aria-label={childListLabel(node.name)}
          hidden={!expanded}
          className="ml-3.5 grid gap-2 border-l pl-3.5"
        >
          {node.children.map((child) => (
            <DepartmentItem key={child.id} node={child} tree={tree} actions={actions} />
          ))}
        </ul>
      ) : null}
    </li>
  );
}
