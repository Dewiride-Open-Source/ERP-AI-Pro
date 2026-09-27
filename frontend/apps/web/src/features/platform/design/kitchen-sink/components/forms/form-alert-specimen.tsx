import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";

import { Specimen, SpecimenRow } from "../specimen";

import { formFieldIds } from "./form-field-ids";

const exampleReference = "4bf92f3577b34da6a3ce929d0e0e4736";

export function FormAlertSpecimen() {
  return (
    <Specimen
      title="Error summary"
      description="Takes focus when a save is refused, links each message to its field and shows the reference to quote to support."
    >
      <SpecimenRow label="Refused details">
        <FormAlert
          title="Some details need attention."
          messages={[
            { message: "Check these details against the registration certificate." },
            { message: "Enter the supplier's legal name.", fieldId: formFieldIds.legalName },
            { message: "Enter an email address such as accounts@acme.in.", fieldId: formFieldIds.email },
          ]}
          reference={exampleReference}
        />
      </SpecimenRow>
      <SpecimenRow label="A failure without field messages">
        <FormAlert
          title="The server could not finish this. Try again."
          messages={[]}
          reference={exampleReference}
        />
      </SpecimenRow>
    </Specimen>
  );
}
