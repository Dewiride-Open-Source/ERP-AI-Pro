"use client";

import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { useState, type SubmitEvent } from "react";

import { Specimen, SpecimenRow } from "../specimen";

export function SubmitButtonSpecimen() {
  const [pending, setPending] = useState(false);

  const onSubmit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    setPending(true);
  };

  return (
    <Specimen
      title="Submit button"
      description="Disabled until the page is interactive and while a save runs, with a spinner beside a label that never changes."
    >
      <SpecimenRow label="Ready; pressing it starts a save">
        <form
          noValidate
          onSubmit={onSubmit}
          data-testid="forms-submit-example"
          className="flex flex-wrap gap-3"
        >
          <SubmitButton pending={pending} ready>
            Save
          </SubmitButton>
          <Button type="button" variant="outline" disabled={!pending} onClick={() => setPending(false)}>
            Finish saving
          </Button>
        </form>
      </SpecimenRow>
      <SpecimenRow label="Saving">
        <SubmitButton pending ready>
          Save draft
        </SubmitButton>
      </SpecimenRow>
      <SpecimenRow label="Not ready: the page is still loading">
        <SubmitButton pending={false} ready={false}>
          Send for approval
        </SubmitButton>
      </SpecimenRow>
    </Specimen>
  );
}
