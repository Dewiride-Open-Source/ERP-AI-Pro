"use client";

import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { DateInput } from "@dewiride/erp-ui/components/pickers/date-input";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { DialogFooter } from "@dewiride/erp-ui/components/ui/dialog";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { ArrowLeftIcon } from "lucide-react";
import { useCallback, useLayoutEffect, useRef } from "react";
import { Controller } from "react-hook-form";

import { leavesGroup } from "@/shared/forms/client/field-focus";
import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";

import { useSavingReport } from "../../details/hooks/use-saving-dialog";
import { RoleCheckboxes } from "../../roles/components/role-checkboxes";
import {
  invitePersonSchema,
  invitePersonValues,
  type InvitePersonValues,
} from "../forms/invite-person.schema";
import { invitePerson } from "../server/actions";

import type { DirectoryMatch } from "./directory-search";

// The chosen person's name takes focus as the step appears, because the result that was chosen is gone; going back while
// the invitation is being saved would discard the form its answer belongs to, so Back waits like the dialog's close buttons.
export function InviteDetailsForm({
  person,
  canAssignRoles,
  onInvited,
  onBack,
  onSavingChange,
}: {
  person: DirectoryMatch;
  canAssignRoles: boolean;
  onInvited: (personId: string, personName: string) => void;
  onBack: () => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const heading = useRef<HTMLHeadingElement>(null);
  useLayoutEffect(() => {
    heading.current?.focus();
  }, []);
  const action = useCallback<FormAction<InvitePersonValues>>(
    async (previous, submission) => {
      const answer = await invitePerson(previous, submission);
      if (answer.status === "succeeded") onInvited(answer.personId, answer.personName);
      return answer;
    },
    [onInvited],
  );
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: invitePersonSchema,
    action,
    defaultValues: invitePersonValues(person.entraObjectId),
    idempotent: true,
  });
  useSavingReport(pending, onSavingChange);
  const {
    control,
    register,
    formState: { errors },
  } = form;

  return (
    <div className="grid min-w-0 gap-4">
      <div
        className="grid min-w-0 gap-1 rounded-lg border bg-muted/50 px-3 py-2.5"
        data-testid="invite-person-chosen"
      >
        <h3 ref={heading} tabIndex={-1} className="font-medium wrap-anywhere outline-none">
          {person.displayName}
        </h3>
        <p className="text-sm wrap-anywhere text-muted-foreground">{person.signInName}</p>
        <p className="text-sm text-muted-foreground">
          Their name and work email in the ERP come from the company directory.
        </p>
      </div>

      <form
        noValidate
        method="post"
        onSubmit={onSubmit}
        className="grid min-w-0 gap-4"
        data-testid="invite-person-form"
      >
        {alert ? <FormAlert ref={alertRef} {...alert} /> : null}

        <FieldGroup>
          <FormField
            id={fieldId("employeeCode")}
            label="Employee code"
            description="Optional. Letters, digits, hyphens, slashes and underscores."
            errors={fieldErrorMessages(errors.employeeCode)}
          >
            {(frame) => (
              <Input {...frame} {...register("employeeCode")} autoComplete="off" spellCheck={false} />
            )}
          </FormField>
          <FormField
            id={fieldId("phoneNumber")}
            label="Phone number"
            description="Optional. With + and the country code, or a 10-digit Indian mobile number."
            errors={fieldErrorMessages(errors.phoneNumber)}
          >
            {(frame) => <Input {...frame} {...register("phoneNumber")} type="tel" autoComplete="off" />}
          </FormField>
          <Controller
            control={control}
            name="dateOfJoining"
            render={({ field, fieldState }) => (
              <FormField
                id={fieldId("dateOfJoining")}
                label="Date of joining"
                description="Optional."
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
          {canAssignRoles ? (
            <Controller
              control={control}
              name="roles"
              render={({ field, fieldState }) => (
                <RoleCheckboxes
                  id={fieldId("roles")}
                  ref={field.ref}
                  legend="Roles in the ERP"
                  description="Optional. Everyone who signs in is an employee, and administrators are chosen in Microsoft Entra."
                  value={field.value}
                  onValueChange={field.onChange}
                  onBlur={field.onBlur}
                  errors={fieldErrorMessages(fieldState.error)}
                />
              )}
            />
          ) : null}
        </FieldGroup>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            aria-disabled={pending || undefined}
            className="aria-disabled:opacity-50"
            onClick={() => {
              if (!pending) onBack();
            }}
          >
            <ArrowLeftIcon data-icon="inline-start" aria-hidden />
            Back to the search
          </Button>
          <SubmitButton pending={pending} ready={ready} data-testid="invite-person-save">
            Invite
          </SubmitButton>
        </DialogFooter>
      </form>
    </div>
  );
}
