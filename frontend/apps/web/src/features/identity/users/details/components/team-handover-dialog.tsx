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
  DialogTrigger,
} from "@dewiride/erp-ui/components/ui/dialog";
import { FieldError, FieldGroup, FieldLegend, FieldSet } from "@dewiride/erp-ui/components/ui/field";
import { Label } from "@dewiride/erp-ui/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@dewiride/erp-ui/components/ui/radio-group";
import { useCallback, useMemo, type ReactElement, type Ref } from "react";
import { Controller, useWatch } from "react-hook-form";
import { toast } from "sonner";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";

import { PersonPicker } from "../../organisation/components/person-picker";
import {
  teamHandoverSchema,
  teamHandoverValues,
  type TeamHandoverValues,
} from "../forms/team-handover.schema";
import { useSavingDialog, useSavingReport } from "../hooks/use-saving-dialog";
import { handOverTeam, searchNewManagers } from "../server/actions";

import { SavingDialogClose, SavingDialogCornerClose } from "./person-action-controls";
import {
  teamHandover,
  teamMembersLabel,
  type TeamHandover,
  type TeamHandoverMode,
} from "./person-action-text";

export type TeamMember = { readonly id: string; readonly displayName: string };

type TeamOf = {
  readonly personId: string;
  readonly personName: string;
  readonly version: string;
  readonly team: readonly TeamMember[];
};

// Deactivating a manager and handing over the team of a manager already deactivated are one change to the API: each member
// reports to the chosen person, or to no one, from today.
export function TeamHandoverDialog({
  mode,
  personId,
  personName,
  version,
  team,
  trigger,
  onHandedOver,
}: TeamOf & { mode: TeamHandoverMode; trigger: ReactElement; onHandedOver: () => void }) {
  const { open, saving, setOpen, setSaving, onOpenChange } = useSavingDialog();
  const texts = teamHandover(mode, personName, team.length);
  const handedOver = useCallback(() => {
    setOpen(false);
    toast.success(texts.done);
    onHandedOver();
  }, [onHandedOver, setOpen, texts.done]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg"
      >
        <DialogHeader>
          <DialogTitle>{texts.title}</DialogTitle>
          <DialogDescription>{texts.description}</DialogDescription>
        </DialogHeader>
        <TeamHandoverForm
          key={version}
          mode={mode}
          personId={personId}
          personName={personName}
          version={version}
          team={team}
          texts={texts}
          onHandedOver={handedOver}
          onSavingChange={setSaving}
        />
        <SavingDialogCornerClose saving={saving} />
      </DialogContent>
    </Dialog>
  );
}

// The picker leaves out the person and their team, whom the API refuses as the new manager, and it is offered only while
// "Another person" is chosen, so a choice of "No manager" sends no manager the picker held before.
function TeamHandoverForm({
  mode,
  personId,
  personName,
  version,
  team,
  texts,
  onHandedOver,
  onSavingChange,
}: TeamOf & {
  mode: TeamHandoverMode;
  texts: TeamHandover;
  onHandedOver: () => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const action = useCallback<FormAction<TeamHandoverValues>>(
    async (previous, submission) => {
      const answer = await handOverTeam(previous, submission);
      if (answer.status === "succeeded") onHandedOver();
      return answer;
    },
    [onHandedOver],
  );
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: teamHandoverSchema,
    action,
    defaultValues: teamHandoverValues(personId, version),
  });
  useSavingReport(pending, onSavingChange);
  const teamIds = useMemo(() => team.map((member) => member.id), [team]);
  const findManagers = useCallback(
    (text: string) => searchNewManagers(personId, teamIds, text),
    [personId, teamIds],
  );
  const { control, resetField } = form;
  const reportsTo = useWatch({ control, name: "reportsTo" });

  return (
    <form
      noValidate
      method="post"
      onSubmit={onSubmit}
      className="grid min-w-0 gap-4"
      data-testid="team-handover-form"
    >
      <div className="grid min-w-0 gap-2">
        <p className="text-sm">{texts.team}</p>
        <ul
          aria-label={teamMembersLabel(personName)}
          className="grid min-w-0 gap-1 rounded-lg border bg-muted/40 px-3 py-2 text-sm"
          data-testid="team-handover-members"
        >
          {team.map((member) => (
            <li key={member.id} className="font-medium wrap-anywhere">
              {member.displayName}
            </li>
          ))}
        </ul>
      </div>

      <FieldGroup>
        <Controller
          control={control}
          name="reportsTo"
          render={({ field, fieldState }) => (
            <ReportsToChoice
              id={fieldId("reportsTo")}
              ref={field.ref}
              value={field.value}
              onValueChange={(value) => {
                field.onChange(value);
                if (value !== "person") resetField("managerId");
              }}
              onBlur={field.onBlur}
              errors={fieldErrorMessages(fieldState.error)}
            />
          )}
        />

        {reportsTo === "person" ? (
          <Controller
            control={control}
            name="managerId"
            render={({ field, fieldState }) => (
              <FormField
                id={fieldId("managerId")}
                label="New manager"
                description="Type part of a name; only active people outside the team are listed."
                required
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
        ) : null}
      </FieldGroup>

      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}

      <DialogFooter>
        <SavingDialogClose saving={pending} variant="outline">
          {texts.cancel}
        </SavingDialogClose>
        <SubmitButton
          pending={pending}
          ready={ready}
          variant={mode === "deactivate" ? "destructive" : "default"}
          data-testid="team-handover-save"
        >
          {texts.confirm}
        </SubmitButton>
      </DialogFooter>
    </form>
  );
}

// The first choice carries the field's own id, so a message about the choice links to and focuses the group.
function ReportsToChoice({
  id,
  ref,
  value,
  onValueChange,
  onBlur,
  errors,
}: {
  id: string;
  ref: Ref<HTMLButtonElement>;
  value: string;
  onValueChange: (value: string) => void;
  onBlur: () => void;
  errors: readonly string[] | undefined;
}) {
  const legendId = `${id}-legend`;
  const errorId = `${id}-error`;
  const nobodyId = `${id}-nobody`;
  const nobodyHintId = `${id}-nobody-hint`;
  const messages = errors?.filter((message) => message !== "") ?? [];
  const invalid = messages.length > 0;

  return (
    <FieldSet data-invalid={invalid || undefined} className="gap-3">
      <FieldLegend id={legendId} variant="label" className="mb-0">
        Who they report to from today{" "}
        <span aria-hidden="true" className="font-normal text-muted-foreground">
          (required)
        </span>
      </FieldLegend>
      <RadioGroup
        aria-labelledby={legendId}
        aria-describedby={invalid ? errorId : undefined}
        aria-invalid={invalid || undefined}
        aria-required
        value={value}
        onValueChange={onValueChange}
        className="gap-3"
      >
        <div className="flex items-start gap-3">
          <RadioGroupItem
            id={id}
            ref={ref}
            value="person"
            onBlur={onBlur}
            aria-invalid={invalid || undefined}
            className="mt-0.5"
          />
          <Label htmlFor={id}>Another person</Label>
        </div>
        <div className="flex items-start gap-3">
          <RadioGroupItem
            id={nobodyId}
            value="nobody"
            onBlur={onBlur}
            aria-invalid={invalid || undefined}
            aria-describedby={nobodyHintId}
            className="mt-0.5"
          />
          <div className="grid gap-1">
            <Label htmlFor={nobodyId}>No manager</Label>
            <p id={nobodyHintId} className="text-sm text-muted-foreground">
              They report to no one until a move gives them a manager.
            </p>
          </div>
        </div>
      </RadioGroup>
      {invalid ? <FieldError id={errorId} errors={messages.map((message) => ({ message }))} /> : null}
    </FieldSet>
  );
}
