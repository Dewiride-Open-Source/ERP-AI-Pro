import { SpecimenGrid } from "../specimen";

import { AmountSpecimen } from "./amount-specimen";
import { CalendarSpecimen } from "./calendar-specimen";
import { ComboboxSpecimen } from "./combobox-specimen";
import { DateRangeSpecimen } from "./date-range-specimen";
import { DateSpecimen } from "./date-specimen";
import { FieldFrameSpecimen } from "./field-frame-specimen";
import { FormAlertSpecimen } from "./form-alert-specimen";
import { IdentifierSpecimen } from "./identifier-specimen";
import { SubmitButtonSpecimen } from "./submit-button-specimen";

export function FormsShowcase() {
  return (
    <SpecimenGrid>
      <FieldFrameSpecimen />
      <FormAlertSpecimen />
      <SubmitButtonSpecimen />
      <AmountSpecimen />
      <IdentifierSpecimen />
      <DateSpecimen />
      <DateRangeSpecimen />
      <ComboboxSpecimen />
      <CalendarSpecimen />
    </SpecimenGrid>
  );
}
