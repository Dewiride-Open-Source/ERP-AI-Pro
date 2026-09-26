"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Field, FieldDescription, FieldError, FieldLabel } from "@dewiride/erp-ui/components/ui/field";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { useRef, useState, type FormEvent } from "react";
import { toast } from "sonner";

const nameId = "example-form-name";
const descriptionId = "example-form-name-description";
const errorId = "example-form-name-error";

export function ExampleForm() {
  const inputRef = useRef<HTMLInputElement>(null);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | undefined>(undefined);

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmed = name.trim();
    if (trimmed.length === 0) {
      setError("Enter a name.");
      inputRef.current?.focus();
      return;
    }
    setError(undefined);
    toast.success("Saved", { description: `${trimmed} was saved.` });
  };

  const invalid = error !== undefined;

  return (
    <form noValidate onSubmit={onSubmit} data-testid="example-form" className="grid max-w-md gap-4">
      <Field data-invalid={invalid || undefined}>
        <FieldLabel htmlFor={nameId}>Name</FieldLabel>
        <Input
          ref={inputRef}
          id={nameId}
          name="name"
          required
          autoComplete="name"
          value={name}
          onChange={(event) => setName(event.currentTarget.value)}
          aria-invalid={invalid || undefined}
          aria-describedby={invalid ? `${descriptionId} ${errorId}` : descriptionId}
        />
        <FieldDescription id={descriptionId}>Required. Shown on documents you issue.</FieldDescription>
        {invalid ? <FieldError id={errorId}>{error}</FieldError> : null}
      </Field>
      <Button type="submit" className="w-fit" data-testid="example-form-submit">
        Save
      </Button>
    </form>
  );
}
