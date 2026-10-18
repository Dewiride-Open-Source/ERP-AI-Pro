"use client";

import { Combobox } from "@dewiride/erp-ui/components/combobox/combobox";
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
import { Controller, type FieldError, type RefCallBack, type UseFormRegisterReturn } from "react-hook-form";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";

import { SavingDialogClose, SavingDialogCornerClose } from "../../details/components/person-action-controls";
import { useSavingReport } from "../../details/hooks/use-saving-dialog";
import type { OrganisationOption } from "../../organisation/components/organisation-options";
import { noChoiceMessage } from "../../positions/components/move-text";
import {
  addDepartmentSchema,
  addDepartmentValues,
  departmentNameMaxLength,
  editDepartmentSchema,
  editDepartmentValues,
  type AddDepartmentValues,
  type EditDepartmentValues,
} from "../forms/department.schema";
import { createDepartment, updateDepartment } from "../server/actions";

import { otherParentsMessage } from "./department-text";
import { parentChoices, type DepartmentRecord } from "./department-tree";

export type EditableDepartment = DepartmentRecord & { readonly version: string };

export type DepartmentDialogTarget =
  | { readonly kind: "add"; readonly parentId: string | undefined; readonly returnFocusTo: string }
  | { readonly kind: "edit"; readonly department: EditableDepartment; readonly returnFocusTo: string };

export type DepartmentSaved = { readonly name: string; readonly parentId: string | undefined };

type ParentControl = {
  readonly name: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly onBlur: () => void;
  readonly ref: RefCallBack;
};

// One dialog serves every row, because a department moved under another one leaves its row and takes its buttons along;
// focus then returns, by id, to the button that opened the dialog, wherever that department now sits.
export function DepartmentDialog({
  target,
  opening,
  open,
  saving,
  onOpenChange,
  onSavingChange,
  records,
  fallbackFocusId,
  onAdded,
  onEdited,
}: {
  target: DepartmentDialogTarget | undefined;
  opening: number;
  open: boolean;
  saving: boolean;
  onOpenChange: (open: boolean) => void;
  onSavingChange: (saving: boolean) => void;
  records: readonly DepartmentRecord[];
  fallbackFocusId: string;
  onAdded: (saved: DepartmentSaved) => void;
  onEdited: (saved: DepartmentSaved) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg"
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          const returnTo = target === undefined ? null : document.getElementById(target.returnFocusTo);
          (returnTo ?? document.getElementById(fallbackFocusId))?.focus();
        }}
      >
        {target?.kind === "edit" ? (
          <EditDepartmentForm
            key={opening}
            department={target.department}
            parents={parentChoices(records, target.department.id)}
            onSaved={onEdited}
            onSavingChange={onSavingChange}
          />
        ) : (
          <AddDepartmentForm
            key={opening}
            parentId={target?.parentId}
            parents={parentChoices(records, undefined)}
            onSaved={onAdded}
            onSavingChange={onSavingChange}
          />
        )}
        <SavingDialogCornerClose saving={saving} />
      </DialogContent>
    </Dialog>
  );
}

function AddDepartmentForm({
  parentId,
  parents,
  onSaved,
  onSavingChange,
}: {
  parentId: string | undefined;
  parents: readonly OrganisationOption[];
  onSaved: (saved: DepartmentSaved) => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const action = useCallback<FormAction<AddDepartmentValues>>(
    async (previous, submission) => {
      const answer = await createDepartment(previous, submission);
      if (answer.status === "succeeded") onSaved(savedDepartment(submission.values));
      return answer;
    },
    [onSaved],
  );
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: addDepartmentSchema,
    action,
    defaultValues: addDepartmentValues(parentId),
    idempotent: true,
  });
  useSavingReport(pending, onSavingChange);

  return (
    <>
      <DialogHeader className="pr-8">
        <DialogTitle>Add a department</DialogTitle>
        <DialogDescription>
          Name the department and choose the department it sits under, or leave that empty to add it at the
          top.
        </DialogDescription>
      </DialogHeader>
      <form noValidate method="post" onSubmit={onSubmit} className="grid gap-4" data-testid="department-form">
        {alert ? <FormAlert ref={alertRef} {...alert} /> : null}
        <FieldGroup>
          <NameField
            id={fieldId("name")}
            registration={form.register("name")}
            error={form.formState.errors.name}
          />
          <Controller
            control={form.control}
            name="parentId"
            render={({ field, fieldState }) => (
              <ParentField
                id={fieldId("parentId")}
                field={field}
                error={fieldState.error}
                parents={parents}
                emptyMessage={noChoiceMessage("department", parents.length)}
              />
            )}
          />
        </FieldGroup>
        <DialogFooter>
          <SavingDialogClose saving={pending} variant="outline">
            Cancel
          </SavingDialogClose>
          <SubmitButton pending={pending} ready={ready} data-testid="department-save">
            Add the department
          </SubmitButton>
        </DialogFooter>
      </form>
    </>
  );
}

function EditDepartmentForm({
  department,
  parents,
  onSaved,
  onSavingChange,
}: {
  department: EditableDepartment;
  parents: readonly OrganisationOption[];
  onSaved: (saved: DepartmentSaved) => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const action = useCallback<FormAction<EditDepartmentValues>>(
    async (previous, submission) => {
      const answer = await updateDepartment(previous, submission);
      if (answer.status === "succeeded") onSaved(savedDepartment(submission.values));
      return answer;
    },
    [onSaved],
  );
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: editDepartmentSchema,
    action,
    defaultValues: editDepartmentValues(department),
  });
  useSavingReport(pending, onSavingChange);

  return (
    <>
      <DialogHeader className="pr-8">
        <DialogTitle className="wrap-anywhere">Rename or move {department.name}</DialogTitle>
        <DialogDescription>
          The name and the department it sits under hold for every date, so people&apos;s past moves show them
          too.
        </DialogDescription>
      </DialogHeader>
      <form noValidate method="post" onSubmit={onSubmit} className="grid gap-4" data-testid="department-form">
        {alert ? <FormAlert ref={alertRef} {...alert} /> : null}
        <FieldGroup>
          <NameField
            id={fieldId("name")}
            registration={form.register("name")}
            error={form.formState.errors.name}
          />
          <Controller
            control={form.control}
            name="parentId"
            render={({ field, fieldState }) => (
              <ParentField
                id={fieldId("parentId")}
                field={field}
                error={fieldState.error}
                parents={parents}
                emptyMessage={otherParentsMessage(parents.length)}
              />
            )}
          />
        </FieldGroup>
        <DialogFooter>
          <SavingDialogClose saving={pending} variant="outline">
            Cancel
          </SavingDialogClose>
          <SubmitButton pending={pending} ready={ready} data-testid="department-save">
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
      description={`Up to ${departmentNameMaxLength} characters. No other department in the same place may have it.`}
      required
      errors={fieldErrorMessages(error)}
    >
      {(frame) => <Input {...frame} {...registration} autoComplete="off" />}
    </FormField>
  );
}

function ParentField({
  id,
  field,
  error,
  parents,
  emptyMessage,
}: {
  id: string;
  field: ParentControl;
  error: FieldError | undefined;
  parents: readonly OrganisationOption[];
  emptyMessage: string;
}) {
  return (
    <FormField
      id={id}
      label="Sits under"
      description="Leave empty to put it at the top."
      errors={fieldErrorMessages(error)}
    >
      {(frame) => (
        <Combobox
          {...frame}
          ref={field.ref}
          name={field.name}
          options={parents}
          value={field.value === "" ? null : field.value}
          onValueChange={(value) => field.onChange(value ?? "")}
          onBlur={field.onBlur}
          placeholder="Search departments"
          emptyMessage={emptyMessage}
        />
      )}
    </FormField>
  );
}

function savedDepartment(values: { readonly name: string; readonly parentId: string }): DepartmentSaved {
  const parentId = values.parentId.trim();
  return { name: values.name.trim(), parentId: parentId === "" ? undefined : parentId };
}
