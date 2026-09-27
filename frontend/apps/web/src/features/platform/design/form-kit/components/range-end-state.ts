import type { FormControlProps } from "@dewiride/erp-ui/components/forms/form-field";
import type { DateRangeEndState } from "@dewiride/erp-ui/components/pickers/date-range-input";

// FormField lists the messages of both ends in its one error region, `<id>-error`, so an end without a message of its own
// is neither invalid nor described by that region.
export function rangeEndState(
  frame: FormControlProps,
  errors: readonly string[] | undefined,
): DateRangeEndState {
  const describedBy = frame["aria-describedby"];
  if (errors !== undefined && errors.length > 0) return { invalid: true, describedBy };
  const errorId = `${frame.id}-error`;
  const withoutError = describedBy
    ?.split(" ")
    .filter((id) => id !== errorId)
    .join(" ");
  return { invalid: false, describedBy: withoutError === "" ? undefined : withoutError };
}
