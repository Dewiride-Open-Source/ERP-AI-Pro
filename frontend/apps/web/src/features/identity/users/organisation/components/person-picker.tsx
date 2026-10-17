"use client";

import { Combobox, type ComboboxProps } from "@dewiride/erp-ui/components/combobox/combobox";

import { usePersonSearch, type PersonSearch } from "../hooks/use-person-search";

export type PersonPickerProps = Omit<
  ComboboxProps,
  "options" | "inputValue" | "onInputValueChange" | "loading" | "emptyMessage"
> & {
  find: (text: string) => Promise<PersonSearch>;
  emptyMessage?: string | undefined;
};

// The people come from a search on the server as the person types, and the first page of them as soon as the field takes
// focus, so a short organisation can be picked from without typing.
export function PersonPicker({
  find,
  emptyMessage = "No active person matches.",
  onFocus,
  ...comboboxProps
}: PersonPickerProps) {
  const people = usePersonSearch(find);

  return (
    <Combobox
      {...comboboxProps}
      options={people.options}
      inputValue={people.text}
      onInputValueChange={people.search}
      loading={people.loading}
      emptyMessage={people.failure ?? emptyMessage}
      onFocus={(event) => {
        people.start();
        onFocus?.(event);
      }}
    />
  );
}
