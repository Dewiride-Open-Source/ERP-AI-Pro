"use client";

import type { PersonRole } from "@dewiride/erp-api-client";
import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@dewiride/erp-ui/components/ui/dialog";
import { UserCogIcon } from "lucide-react";
import Link from "next/link";
import { useCallback, useState } from "react";
import { Controller } from "react-hook-form";
import { toast } from "sonner";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";

import { PersonActionButton } from "../../details/components/person-action-controls";
import { assignRolesSchema, assignRolesValues, type AssignRolesValues } from "../forms/assign-roles.schema";
import { assignPersonRoles } from "../server/actions";

import { RoleCheckboxes } from "./role-checkboxes";

export function AssignRolesDialog({
  personId,
  personName,
  roles,
  version,
}: {
  personId: string;
  personName: string;
  roles: readonly PersonRole[];
  version: string;
}) {
  const [open, setOpen] = useState(false);
  const saved = useCallback(() => {
    setOpen(false);
    toast.success(`Saved the roles of ${personName}.`);
  }, [personName]);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <PersonActionButton
          pending={false}
          icon={UserCogIcon}
          variant="outline"
          size="sm"
          data-testid="assign-roles"
        >
          Assign roles
        </PersonActionButton>
      </DialogTrigger>
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Assign roles</DialogTitle>
          <DialogDescription>
            Choose the roles {personName} holds in the ERP. Their employee and administrator roles come from
            Microsoft Entra.
          </DialogDescription>
        </DialogHeader>
        <AssignRolesForm
          key={version}
          defaultValues={assignRolesValues(personId, roles, version)}
          onSaved={saved}
        />
      </DialogContent>
    </Dialog>
  );
}

// The record's version names the state the roles were chosen from, so a newer version after a save starts the form again.
function AssignRolesForm({
  defaultValues,
  onSaved,
}: {
  defaultValues: AssignRolesValues;
  onSaved: () => void;
}) {
  const action = useCallback<FormAction<AssignRolesValues>>(
    async (previous, submission) => {
      const answer = await assignPersonRoles(previous, submission);
      if (answer.status === "succeeded") onSaved();
      return answer;
    },
    [onSaved],
  );
  const { form, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: assignRolesSchema,
    action,
    defaultValues,
  });

  return (
    <form noValidate method="post" onSubmit={onSubmit} className="grid gap-4" data-testid="assign-roles-form">
      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}
      <Controller
        control={form.control}
        name="roles"
        render={({ field, fieldState }) => (
          <RoleCheckboxes
            id={fieldId("roles")}
            ref={field.ref}
            legend="Roles in the ERP"
            value={field.value}
            onValueChange={field.onChange}
            onBlur={field.onBlur}
            errors={fieldErrorMessages(fieldState.error)}
          />
        )}
      />
      <Link
        href="/identity/users/permissions"
        className="w-fit rounded-sm text-primary underline-offset-4 focus-ring hover:underline"
      >
        See what each role may do
      </Link>
      <DialogFooter>
        <DialogClose asChild>
          <Button type="button" variant="outline">
            Cancel
          </Button>
        </DialogClose>
        <SubmitButton pending={pending} ready={ready} data-testid="assign-roles-save">
          Save roles
        </SubmitButton>
      </DialogFooter>
    </form>
  );
}
