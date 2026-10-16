export interface FilterFormState {
  readonly signature: string;
  readonly key: number;
  readonly applied: string | undefined;
  readonly clearing: boolean;
  readonly focusFirstField: boolean;
}

export function initialFilterFormState(signature: string): FilterFormState {
  return { signature, key: 0, applied: undefined, clearing: false, focusFirstField: false };
}

// The filter form keeps its controls, and the focus inside them, when the address changes because the form itself applied
// these filters; any other change (Clear filters, Back, a link) remounts it so its fields show the new filters.
export function filterFormStateFor(state: FilterFormState, signature: string): FilterFormState {
  if (state.signature === signature) return state;
  return {
    signature,
    key: state.applied === signature ? state.key : state.key + 1,
    applied: undefined,
    clearing: false,
    focusFirstField: state.clearing,
  };
}

export function filtersApplied(state: FilterFormState, signature: string): FilterFormState {
  return { ...state, applied: signature };
}

export function filtersClearing(state: FilterFormState): FilterFormState {
  return { ...state, clearing: true };
}
