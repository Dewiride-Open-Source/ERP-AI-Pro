"use client";

import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { DateInput } from "@dewiride/erp-ui/components/pickers/date-input";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import Link from "next/link";
import { Controller } from "react-hook-form";

import { leavesGroup } from "@/shared/forms/client/field-focus";
import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm } from "@/shared/forms/client/use-action-form";

import { editPersonSchema, type EditPersonValues } from "../forms/edit-person.schema";
import { updatePerson } from "../server/actions";

export function EditPersonForm({
  labelledBy,
  defaultValues,
}: {
  labelledBy: string;
  defaultValues: EditPersonValues;
}) {
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: editPersonSchema,
    action: updatePerson,
    defaultValues,
  });
  const {
    control,
    register,
    formState: { errors },
  } = form;

  return (
    <form
      noValidate
      method="post"
      onSubmit={onSubmit}
      aria-labelledby={labelledBy}
      data-testid="edit-person-form"
      className="grid min-w-0 gap-6"
    >
      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}

      <FieldGroup>
        <div className="grid min-w-0 gap-5 md:grid-cols-3">
          <FormField
            id={fieldId("employeeCode")}
            label="Employee code"
            description="Letters, digits, hyphens, slashes and underscores."
            errors={fieldErrorMessages(errors.employeeCode)}
          >
            {(frame) => (
              <Input {...frame} {...register("employeeCode")} autoComplete="off" spellCheck={false} />
            )}
          </FormField>
          <FormField
            id={fieldId("phoneNumber")}
            label="Phone number"
            description="With + and the country code, or a 10-digit Indian mobile number."
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
        </div>
      </FieldGroup>

      <div className="flex flex-wrap items-center gap-3">
        <SubmitButton pending={pending} ready={ready} data-testid="edit-person-save">
          Save changes
        </SubmitButton>
        <Button asChild variant="outline">
          <Link href={`/identity/users/${defaultValues.id}`}>Cancel</Link>
        </Button>
      </div>
    </form>
  );
}
