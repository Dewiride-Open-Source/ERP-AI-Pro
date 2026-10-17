"use client";

import { Combobox, type ComboboxOption } from "@dewiride/erp-ui/components/combobox/combobox";
import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { DateInput } from "@dewiride/erp-ui/components/pickers/date-input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@dewiride/erp-ui/components/ui/dialog";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { CalendarPlusIcon } from "lucide-react";
import { useCallback } from "react";
import { Controller } from "react-hook-form";
import { toast } from "sonner";

import { leavesGroup } from "@/shared/forms/client/field-focus";
import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";

import {
  PersonActionButton,
  SavingDialogClose,
  SavingDialogCornerClose,
} from "../../details/components/person-action-controls";
import { useSavingDialog, useSavingReport } from "../../details/hooks/use-saving-dialog";
import { PersonPicker } from "../../organisation/components/person-picker";
import { recordMoveSchema, recordMoveValues, type RecordMoveValues } from "../forms/record-move.schema";
import { recordPersonMove, searchManagers } from "../server/actions";

import { noChoiceMessage, recordedMoveMessage } from "./move-text";

type Choices = {
  readonly departments: readonly ComboboxOption[];
  readonly designations: readonly ComboboxOption[];
};

export function RecordMoveDialog({
  personId,
  personName,
  departments,
  designations,
}: Choices & { personId: string; personName: string }) {
  const { open, saving, setOpen, setSaving, onOpenChange } = useSavingDialog();
  const recorded = useCallback(
    (effectiveFrom: string) => {
      setOpen(false);
      toast.success(recordedMoveMessage(personName, effectiveFrom));
    },
    [personName, setOpen],
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogTrigger asChild>
        <PersonActionButton
          pending={false}
          icon={CalendarPlusIcon}
          variant="outline"
          size="sm"
          data-testid="record-move"
        >
          Record a move
        </PersonActionButton>
      </DialogTrigger>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg"
      >
        <DialogHeader>
          <DialogTitle>Record a move</DialogTitle>
          <DialogDescription>
            From the date you choose, {personName} works in this department with this designation and reports
            to this manager, until their next move. Leave a choice empty for none.
          </DialogDescription>
        </DialogHeader>
        <RecordMoveForm
          personId={personId}
          departments={departments}
          designations={designations}
          onRecorded={recorded}
          onSavingChange={setSaving}
        />
        <SavingDialogCornerClose saving={saving} />
      </DialogContent>
    </Dialog>
  );
}

function RecordMoveForm({
  personId,
  departments,
  designations,
  onRecorded,
  onSavingChange,
}: Choices & {
  personId: string;
  onRecorded: (effectiveFrom: string) => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const action = useCallback<FormAction<RecordMoveValues>>(
    async (previous, submission) => {
      const answer = await recordPersonMove(previous, submission);
      if (answer.status === "succeeded") onRecorded(submission.values.effectiveFrom);
      return answer;
    },
    [onRecorded],
  );
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: recordMoveSchema,
    action,
    defaultValues: recordMoveValues(personId),
    idempotent: true,
  });
  useSavingReport(pending, onSavingChange);
  const findManagers = useCallback((text: string) => searchManagers(personId, text), [personId]);
  const { control } = form;

  return (
    <form noValidate method="post" onSubmit={onSubmit} className="grid gap-4" data-testid="record-move-form">
      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}

      <FieldGroup>
        <Controller
          control={control}
          name="effectiveFrom"
          render={({ field, fieldState }) => (
            <FormField
              id={fieldId("effectiveFrom")}
              label="Takes effect on"
              description="A date in the past or the future."
              required
              errors={fieldErrorMessages(fieldState.error)}
            >
              {(frame) => (
                <div
                  className="min-w-0"
                  onBlur={(event) => {
                    if (leavesGroup(event)) field.onBlur();
                  }}
                >
                  <DateInput
                    {...frame}
                    ref={field.ref}
                    name={field.name}
                    value={field.value}
                    onValueChange={field.onChange}
                  />
                </div>
              )}
            </FormField>
          )}
        />

        <Controller
          control={control}
          name="departmentId"
          render={({ field, fieldState }) => (
            <FormField
              id={fieldId("departmentId")}
              label="Department"
              errors={fieldErrorMessages(fieldState.error)}
            >
              {(frame) => (
                <Combobox
                  {...frame}
                  ref={field.ref}
                  name={field.name}
                  options={departments}
                  value={field.value === "" ? null : field.value}
                  onValueChange={(value) => field.onChange(value ?? "")}
                  onBlur={field.onBlur}
                  placeholder="Search departments"
                  emptyMessage={noChoiceMessage("department", departments.length)}
                />
              )}
            </FormField>
          )}
        />

        <Controller
          control={control}
          name="designationId"
          render={({ field, fieldState }) => (
            <FormField
              id={fieldId("designationId")}
              label="Designation"
              errors={fieldErrorMessages(fieldState.error)}
            >
              {(frame) => (
                <Combobox
                  {...frame}
                  ref={field.ref}
                  name={field.name}
                  options={designations}
                  value={field.value === "" ? null : field.value}
                  onValueChange={(value) => field.onChange(value ?? "")}
                  onBlur={field.onBlur}
                  placeholder="Search designations"
                  emptyMessage={noChoiceMessage("designation", designations.length)}
                />
              )}
            </FormField>
          )}
        />

        <Controller
          control={control}
          name="managerId"
          render={({ field, fieldState }) => (
            <FormField
              id={fieldId("managerId")}
              label="Manager"
              description="Type part of a name; only active people are listed."
              errors={fieldErrorMessages(fieldState.error)}
            >
              {(frame) => (
                <PersonPicker
                  {...frame}
                  ref={field.ref}
                  name={field.name}
                  find={findManagers}
                  value={field.value === "" ? null : field.value}
                  onValueChange={(value) => field.onChange(value ?? "")}
                  onBlur={field.onBlur}
                  placeholder="Search active people"
                />
              )}
            </FormField>
          )}
        />
      </FieldGroup>

      <DialogFooter>
        <SavingDialogClose saving={pending} variant="outline">
          Cancel
        </SavingDialogClose>
        <SubmitButton pending={pending} ready={ready} data-testid="record-move-save">
          Record the move
        </SubmitButton>
      </DialogFooter>
    </form>
  );
}
