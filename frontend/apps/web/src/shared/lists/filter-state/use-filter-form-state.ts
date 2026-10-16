import { useState } from "react";

import {
  filterFormStateFor,
  filtersApplied,
  filtersClearing,
  initialFilterFormState,
} from "./filter-form-state.ts";

export interface FilterFormControl {
  readonly key: number;
  readonly focusFirstField: boolean;
  readonly onApply: (signature: string) => void;
  readonly onClear: () => void;
}

export function useFilterFormState(signature: string): FilterFormControl {
  const [state, setState] = useState(() => initialFilterFormState(signature));
  const current = filterFormStateFor(state, signature);
  if (current !== state) setState(current);

  return {
    key: current.key,
    focusFirstField: current.focusFirstField,
    onApply: (applied) => setState((latest) => filtersApplied(latest, applied)),
    onClear: () => setState(filtersClearing),
  };
}
