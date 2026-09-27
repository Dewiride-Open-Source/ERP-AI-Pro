export interface ComboboxOption {
  readonly value: string;
  readonly label: string;
  readonly description?: string | undefined;
  readonly disabled?: boolean | undefined;
}

export type ComboboxDirection = 1 | -1;

const combiningMarkPattern = /\p{M}/gu;

export function searchText(text: string): string {
  return text.normalize("NFD").replace(combiningMarkPattern, "").toLowerCase().trim();
}

export function filterComboboxOptions(
  options: readonly ComboboxOption[],
  query: string,
): readonly ComboboxOption[] {
  const needle = searchText(query);
  if (needle === "") return options;
  return options.filter((option) => searchText(option.label).includes(needle));
}

export function nextEnabledIndex(
  options: readonly ComboboxOption[],
  from: number,
  direction: ComboboxDirection,
): number {
  const count = options.length;
  if (count === 0) return -1;
  let start = from;
  if (from < 0 || from >= count) start = direction === 1 ? -1 : count;
  for (let step = 1; step <= count; step += 1) {
    const index = (((start + direction * step) % count) + count) % count;
    if (!options[index]?.disabled) return index;
  }
  return -1;
}

export function rememberChosenOption(
  options: readonly ComboboxOption[],
  value: string | null,
  remembered: ComboboxOption | null,
): ComboboxOption | null {
  const listed = value === null ? undefined : options.find((option) => option.value === value);
  if (listed === undefined) return remembered;
  if (remembered !== null && remembered.value === listed.value && remembered.label === listed.label) {
    return remembered;
  }
  return listed;
}

export function chosenOptionLabel(value: string | null, remembered: ComboboxOption | null): string {
  return value !== null && remembered?.value === value ? remembered.label : "";
}

export function firstEnabledIndex(options: readonly ComboboxOption[]): number {
  return nextEnabledIndex(options, -1, 1);
}

export function lastEnabledIndex(options: readonly ComboboxOption[]): number {
  return nextEnabledIndex(options, -1, -1);
}
