"use client";

import { buttonVariants } from "@dewiride/erp-ui/components/ui/button";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { HistoryIcon, PencilIcon, PlusIcon } from "lucide-react";
import Link from "next/link";
import { useCallback, useState } from "react";
import { toast } from "sonner";

import { auditNavigation } from "@/features/platform/audit";

import {
  PersonActionButton,
  PersonActionRefusalMessage,
} from "../../details/components/person-action-controls";
import type { PersonActionRefusal } from "../../details/components/person-action-text";
import { useSavingDialog } from "../../details/hooks/use-saving-dialog";

import { DeleteDesignationAction } from "./delete-designation-action";
import {
  DesignationDialog,
  type DesignationDialogTarget,
  type RenamableDesignation,
} from "./designation-dialog";
import {
  addedMessage,
  historyLabel,
  renameLabel,
  renamedMessage,
  type DesignationRecord,
} from "./designation-text";

export interface DesignationRights {
  readonly create: boolean;
  readonly update: boolean;
  readonly delete: boolean;
}

type RowActions = {
  readonly rights: DesignationRights;
  readonly showsHistory: boolean;
  readonly headingId: string;
  readonly openRename: (designation: RenamableDesignation, returnFocusTo: string) => void;
};

const addButtonId = "designations-add";

function renameButtonId(designationId: string): string {
  return `designation-${designationId}-rename`;
}

export function DesignationsPanel({
  designations,
  rights,
  showsHistory,
  headingId,
}: {
  designations: readonly DesignationRecord[];
  rights: DesignationRights;
  showsHistory: boolean;
  headingId: string;
}) {
  const { open, saving, setOpen, setSaving, onOpenChange } = useSavingDialog();
  const [target, setTarget] = useState<DesignationDialogTarget>();
  const [opening, setOpening] = useState(0);

  const show = useCallback(
    (next: DesignationDialogTarget) => {
      setTarget(next);
      setOpening((count) => count + 1);
      setOpen(true);
    },
    [setOpen],
  );
  const added = useCallback(
    (name: string) => {
      setOpen(false);
      toast.success(addedMessage(name));
    },
    [setOpen],
  );
  const renamed = useCallback(
    (from: string, to: string) => {
      setOpen(false);
      toast.success(renamedMessage(from, to));
    },
    [setOpen],
  );

  const actions: RowActions = {
    rights,
    showsHistory,
    headingId,
    openRename: (designation, returnFocusTo) => show({ kind: "rename", designation, returnFocusTo }),
  };

  return (
    <div className="grid min-w-0 gap-4" data-testid="designations-panel">
      {rights.create ? (
        <div className="flex flex-wrap items-center gap-2">
          <PersonActionButton
            id={addButtonId}
            pending={false}
            icon={PlusIcon}
            size="sm"
            aria-haspopup="dialog"
            data-testid="designations-add"
            onClick={() => show({ kind: "add", returnFocusTo: addButtonId })}
          >
            Add a designation
          </PersonActionButton>
        </div>
      ) : null}

      {designations.length === 0 ? (
        <Empty className="border" data-testid="designations-empty">
          <EmptyHeader>
            <EmptyTitle>No designations are set up yet.</EmptyTitle>
            {rights.create ? (
              <EmptyDescription>Add the first one with Add a designation.</EmptyDescription>
            ) : null}
          </EmptyHeader>
        </Empty>
      ) : (
        <ul role="list" aria-labelledby={headingId} className="grid gap-2">
          {designations.map((designation) => (
            <DesignationItem key={designation.id} designation={designation} actions={actions} />
          ))}
        </ul>
      )}

      <DesignationDialog
        target={target}
        opening={opening}
        open={open}
        saving={saving}
        onOpenChange={onOpenChange}
        onSavingChange={setSaving}
        fallbackFocusId={headingId}
        onAdded={added}
        onRenamed={renamed}
      />
    </div>
  );
}

function DesignationItem({ designation, actions }: { designation: DesignationRecord; actions: RowActions }) {
  const [refusal, setRefusal] = useState<PersonActionRefusal>();
  const { rights } = actions;
  const { version } = designation;
  const offersActions = (rights.update && version !== undefined) || rights.delete || actions.showsHistory;

  return (
    <li
      className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 rounded-lg border bg-card p-2 pl-3"
      data-testid="designation"
    >
      <p className="min-w-0 flex-1 py-1 font-medium wrap-anywhere" data-testid="designation-name">
        {designation.name}
      </p>
      {offersActions ? (
        <div className="flex flex-wrap items-center justify-end gap-1">
          {rights.update && version !== undefined ? (
            <PersonActionButton
              id={renameButtonId(designation.id)}
              pending={false}
              icon={PencilIcon}
              variant="ghost"
              size="sm"
              aria-label={renameLabel(designation.name)}
              aria-haspopup="dialog"
              data-testid="designation-rename"
              onClick={() => actions.openRename({ ...designation, version }, renameButtonId(designation.id))}
            >
              <span className="max-sm:sr-only">Rename</span>
            </PersonActionButton>
          ) : null}
          {rights.delete ? (
            <DeleteDesignationAction
              designationId={designation.id}
              name={designation.name}
              focusAfterDeleteId={actions.headingId}
              onRefusal={setRefusal}
            />
          ) : null}
          {actions.showsHistory ? (
            <Link
              href={`${auditNavigation.basePath}?record=${designation.id}`}
              aria-label={historyLabel(designation.name)}
              className={buttonVariants({ variant: "ghost", size: "sm" })}
              data-testid="designation-history"
            >
              <HistoryIcon data-icon="inline-start" aria-hidden />
              <span className="max-sm:sr-only">Change history</span>
            </Link>
          ) : null}
        </div>
      ) : null}
      {refusal ? (
        <div className="basis-full">
          <PersonActionRefusalMessage refusal={refusal} testId="designation-delete-error" />
        </div>
      ) : null}
    </li>
  );
}
