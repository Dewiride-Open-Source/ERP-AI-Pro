"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { useRef, useState, type SubmitEvent } from "react";
import { toast } from "sonner";

export function ExampleForm() {
  const inputRef = useRef<HTMLInputElement>(null);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | undefined>(undefined);

  const onSubmit = (event: SubmitEvent<HTMLFormElement>) => {
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

  return (
    <form noValidate onSubmit={onSubmit} data-testid="example-form" className="grid max-w-md gap-4">
      <FormField
        id="example-form-name"
        label="Name"
        description="Required. Shown on documents you issue."
        errors={error === undefined ? undefined : [error]}
        required
      >
        {(frame) => (
          <Input
            {...frame}
            ref={inputRef}
            name="name"
            autoComplete="name"
            value={name}
            onChange={(event) => setName(event.currentTarget.value)}
          />
        )}
      </FormField>
      <Button type="submit" className="w-fit">
        Save
      </Button>
    </form>
  );
}
