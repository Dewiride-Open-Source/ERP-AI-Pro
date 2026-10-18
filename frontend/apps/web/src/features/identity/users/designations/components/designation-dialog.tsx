"use client";

import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@dewiride/erp-ui/components/ui/dialog";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { useCallback } from "react";
import type { FieldError, UseFormRegisterReturn } from "react-hook-form";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";

import { SavingDialogClose, SavingDialogCornerClose } from "../../details/components/person-action-controls";
import { useSavingReport } from "../../details/hooks/use-saving-dialog";
import {
  addDesignationSchema,
  defaultAddDesignationValues,
  designationNameMaxLength,
  renameDesignationSchema,
  renameDesignationValues,
  type AddDesignationValues,
  type RenameDesignationValues,
} from "../forms/designation.schema";
import { createDesignation, renameDesignation } from "../server/actions";

import type { DesignationRecord } from "./designation-text";

export type RenamableDesignation = DesignationRecord & { readonly version: string };

export type DesignationDialogTarget =
  | { readonly kind: "add"; readonly returnFocusTo: string }
  | { readonly kind: "rename"; readonly designation: RenamableDesignation; readonly returnFocusTo: string };

// One dialog serves every row, so a renamed designation that moves to another place in the alphabetical list gets focus
// back on its own Rename button, found by id, rather than on a button the move replaced.
export function DesignationDialog({
  target,
  opening,
  open,
  saving,
  onOpenChange,
  onSavingChange,
  fallbackFocusId,
  onAdded,
  onRenamed,
}: {
  target: DesignationDialogTarget | undefined;
  opening: number;
  open: boolean;
  saving: boolean;
  onOpenChange: (open: boolean) => void;
  onSavingChange: (saving: boolean) => void;
  fallbackFocusId: string;
  onAdded: (name: string) => void;
  onRenamed: (from: string, to: string) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-md"
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          const returnTo = target === undefined ? null : document.getElementById(target.returnFocusTo);
          (returnTo ?? document.getElementById(fallbackFocusId))?.focus();
        }}
      >
        {target?.kind === "rename" ? (
          <RenameDesignationForm
            key={opening}
            designation={target.designation}
            onSaved={onRenamed}
            onSavingChange={onSavingChange}
          />
        ) : (
          <AddDesignationForm key={opening} onSaved={onAdded} onSavingChange={onSavingChange} />
        )}
        <SavingDialogCornerClose saving={saving} />
      </DialogContent>
    </Dialog>
  );
}

function AddDesignationForm({
  onSaved,
  onSavingChange,
}: {
  onSaved: (name: string) => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const action = useCallback<FormAction<AddDesignationValues>>(
    async (previous, submission) => {
      const answer = await createDesignation(previous, submission);
      if (answer.status === "succeeded") onSaved(submission.values.name.trim());
      return answer;
    },
    [onSaved],
  );
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: addDesignationSchema,
    action,
    defaultValues: defaultAddDesignationValues,
    idempotent: true,
  });
  useSavingReport(pending, onSavingChange);

  return (
    <>
      <DialogHeader className="pr-8">
        <DialogTitle>Add a designation</DialogTitle>
        <DialogDescription>
          Name a designation people can hold, such as Accountant or Team lead.
        </DialogDescription>
      </DialogHeader>
      <form
        noValidate
        method="post"
        onSubmit={onSubmit}
        className="grid gap-4"
        data-testid="designation-form"
      >
        {alert ? <FormAlert ref={alertRef} {...alert} /> : null}
        <FieldGroup>
          <NameField
            id={fieldId("name")}
            registration={form.register("name")}
            error={form.formState.errors.name}
          />
        </FieldGroup>
        <DialogFooter>
          <SavingDialogClose saving={pending} variant="outline">
            Cancel
          </SavingDialogClose>
          <SubmitButton pending={pending} ready={ready} data-testid="designation-save">
            Add the designation
          </SubmitButton>
        </DialogFooter>
      </form>
    </>
  );
}

function RenameDesignationForm({
  designation,
  onSaved,
  onSavingChange,
}: {
  designation: RenamableDesignation;
  onSaved: (from: string, to: string) => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const action = useCallback<FormAction<RenameDesignationValues>>(
    async (previous, submission) => {
      const answer = await renameDesignation(previous, submission);
      if (answer.status === "succeeded") onSaved(designation.name, submission.values.name.trim());
      return answer;
    },
    [designation.name, onSaved],
  );
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: renameDesignationSchema,
    action,
    defaultValues: renameDesignationValues(designation),
  });
  useSavingReport(pending, onSavingChange);

  return (
    <>
      <DialogHeader className="pr-8">
        <DialogTitle className="wrap-anywhere">Rename {designation.name}</DialogTitle>
        <DialogDescription>
          The new name shows for everyone who holds it, also in their past moves.
        </DialogDescription>
      </DialogHeader>
      <form
        noValidate
        method="post"
        onSubmit={onSubmit}
        className="grid gap-4"
        data-testid="designation-form"
      >
        {alert ? <FormAlert ref={alertRef} {...alert} /> : null}
        <FieldGroup>
          <NameField
            id={fieldId("name")}
            registration={form.register("name")}
            error={form.formState.errors.name}
          />
        </FieldGroup>
        <DialogFooter>
          <SavingDialogClose saving={pending} variant="outline">
            Cancel
          </SavingDialogClose>
          <SubmitButton pending={pending} ready={ready} data-testid="designation-save">
            Save
          </SubmitButton>
        </DialogFooter>
      </form>
    </>
  );
}

function NameField({
  id,
  registration,
  error,
}: {
  id: string;
  registration: UseFormRegisterReturn<"name">;
  error: FieldError | undefined;
}) {
  return (
    <FormField
      id={id}
      label="Name"
      description={`Up to ${designationNameMaxLength} characters. No other designation may have it.`}
      required
      errors={fieldErrorMessages(error)}
    >
      {(frame) => <Input {...frame} {...registration} autoComplete="off" />}
    </FormField>
  );
}
