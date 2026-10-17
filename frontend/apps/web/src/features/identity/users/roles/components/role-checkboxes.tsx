"use client";

import type { PersonRole } from "@dewiride/erp-api-client";
import { Checkbox } from "@dewiride/erp-ui/components/ui/checkbox";
import { FieldDescription, FieldError, FieldLegend, FieldSet } from "@dewiride/erp-ui/components/ui/field";
import { Label } from "@dewiride/erp-ui/components/ui/label";
import type { Ref } from "react";

import { assignableRoles } from "../../registration/forms/register-person.schema";

import { roleLabels, withRole } from "./role-labels";

// The first box carries the field's own id, so a message about the roles links to and focuses the group's first choice.
export function RoleCheckboxes({
  id,
  legend,
  description,
  value,
  onValueChange,
  onBlur,
  errors,
  ref,
}: {
  id: string;
  legend: string;
  description?: string | undefined;
  value: readonly PersonRole[];
  onValueChange: (roles: PersonRole[]) => void;
  onBlur: () => void;
  errors?: readonly string[] | undefined;
  ref?: Ref<HTMLButtonElement> | undefined;
}) {
  const descriptionId = `${id}-description`;
  const errorId = `${id}-error`;
  const messages = errors?.filter((message) => message !== "") ?? [];
  const invalid = messages.length > 0;
  const describedBy = [description === undefined ? "" : descriptionId, invalid ? errorId : ""]
    .filter((part) => part !== "")
    .join(" ");

  return (
    <FieldSet
      data-invalid={invalid || undefined}
      aria-describedby={describedBy === "" ? undefined : describedBy}
      className="gap-3"
    >
      <FieldLegend variant="label" className="mb-0">
        {legend}
      </FieldLegend>
      {description === undefined ? null : (
        <FieldDescription id={descriptionId}>{description}</FieldDescription>
      )}
      {assignableRoles.map((role, index) => {
        const boxId = index === 0 ? id : `${id}-${role}`;
        return (
          <div key={role} className="flex items-center gap-2">
            <Checkbox
              id={boxId}
              ref={index === 0 ? ref : undefined}
              checked={value.includes(role)}
              onCheckedChange={(checked) => onValueChange(withRole(value, role, checked === true))}
              onBlur={onBlur}
              aria-invalid={invalid || undefined}
            />
            <Label htmlFor={boxId}>{roleLabels[role]}</Label>
          </div>
        );
      })}
      {invalid ? <FieldError id={errorId} errors={messages.map((message) => ({ message }))} /> : null}
    </FieldSet>
  );
}
