"use client";

import { useId, type ReactNode } from "react";

import {
  Field,
  FieldContent,
  FieldDescription,
  FieldError,
  FieldLabel,
} from "@dewiride/erp-ui/components/ui/field";

export type FormFieldOrientation = "vertical" | "horizontal" | "responsive";

export type FormControlProps = {
  id: string;
  "aria-invalid": true | undefined;
  "aria-describedby": string | undefined;
  "aria-required": true | undefined;
};

export type FormFieldProps = {
  label: ReactNode;
  description?: ReactNode;
  errors?: readonly string[] | undefined;
  required?: boolean | undefined;
  disabled?: boolean | undefined;
  orientation?: FormFieldOrientation | undefined;
  className?: string | undefined;
  id?: string | undefined;
  children: (control: FormControlProps) => ReactNode;
};

export function formFieldLabelId(controlId: string): string {
  return `${controlId}-label`;
}

function isRendered(node: ReactNode): boolean {
  return node !== undefined && node !== null && node !== false && node !== "";
}

export function FormField({
  label,
  description,
  errors,
  required = false,
  disabled = false,
  orientation = "vertical",
  className,
  id,
  children,
}: FormFieldProps) {
  const generatedId = useId();
  const controlId = id ?? generatedId;
  const descriptionId = `${controlId}-description`;
  const errorId = `${controlId}-error`;
  const messages = errors?.filter((message) => message !== "") ?? [];
  const invalid = messages.length > 0;
  const described = isRendered(description);

  const control: FormControlProps = {
    id: controlId,
    "aria-invalid": invalid || undefined,
    "aria-describedby":
      [described ? descriptionId : "", invalid ? errorId : ""].filter((part) => part !== "").join(" ") ||
      undefined,
    "aria-required": required || undefined,
  };

  const labelElement = (
    <FieldLabel id={formFieldLabelId(controlId)} htmlFor={controlId}>
      {label}
      {required ? (
        <span aria-hidden="true" className="text-muted-foreground">
          *
        </span>
      ) : null}
    </FieldLabel>
  );
  const descriptionElement = described ? (
    <FieldDescription id={descriptionId}>{description}</FieldDescription>
  ) : null;
  const errorElement = invalid ? (
    <FieldError id={errorId} errors={messages.map((message) => ({ message }))} />
  ) : null;

  return (
    <Field
      orientation={orientation}
      data-invalid={invalid || undefined}
      data-disabled={disabled || undefined}
      className={className}
    >
      {orientation === "vertical" ? (
        <>
          {labelElement}
          {children(control)}
          {descriptionElement}
          {errorElement}
        </>
      ) : (
        <>
          <FieldContent>
            {labelElement}
            {descriptionElement}
            {errorElement}
          </FieldContent>
          {children(control)}
        </>
      )}
    </Field>
  );
}
