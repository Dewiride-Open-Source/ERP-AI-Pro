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

import { peopleNavigation } from "../../nav";
import { RoleCheckboxes } from "../../roles/components/role-checkboxes";
import { defaultRegisterPersonValues, registerPersonSchema } from "../forms/register-person.schema";
import { registerPerson } from "../server/actions";

export function RegisterPersonForm({
  labelledBy,
  canAssignRoles,
}: {
  labelledBy: string;
  canAssignRoles: boolean;
}) {
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: registerPersonSchema,
    action: registerPerson,
    defaultValues: defaultRegisterPersonValues,
    idempotent: true,
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
      data-testid="register-person-form"
      className="grid min-w-0 gap-6"
    >
      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}

      <FieldGroup>
        <div className="grid min-w-0 gap-5 md:grid-cols-2">
          <FormField
            id={fieldId("displayName")}
            label="Name"
            description="Shown until their first sign-in brings the name kept in Microsoft Entra."
            required
            errors={fieldErrorMessages(errors.displayName)}
          >
            {(frame) => <Input {...frame} {...register("displayName")} autoComplete="off" />}
          </FormField>
          <FormField
            id={fieldId("workEmail")}
            label="Work email"
            description="The address they sign in with."
            required
            errors={fieldErrorMessages(errors.workEmail)}
          >
            {(frame) => (
              <Input
                {...frame}
                {...register("workEmail")}
                type="email"
                autoComplete="off"
                spellCheck={false}
              />
            )}
          </FormField>
        </div>

        <div className="grid min-w-0 gap-5 md:grid-cols-3">
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
        </div>

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

      <div className="flex flex-wrap items-center gap-3">
        <SubmitButton pending={pending} ready={ready} data-testid="register-person-save">
          Register the person
        </SubmitButton>
        <Button asChild variant="outline">
          <Link href={peopleNavigation.basePath}>Cancel</Link>
        </Button>
      </div>
    </form>
  );
}
