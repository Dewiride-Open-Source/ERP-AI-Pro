import { searchText } from "../combobox/combobox-options.ts";

export interface CommandItem {
  readonly id: string;
  readonly label: string;
  readonly description?: string | undefined;
  readonly group?: string | undefined;
  readonly keywords?: readonly string[] | undefined;
}

export interface CommandSection<T extends CommandItem> {
  readonly group: string | undefined;
  readonly entries: readonly { readonly item: T; readonly index: number }[];
}

export type CommandDirection = 1 | -1;

const termSeparator = /\s+/u;

// Every word typed must appear in the item's label, description, group or keywords, so "plat att" finds Attachments in
// the Platform group; the order of the items is kept, because it is the order of the navigation.
export function filterCommands<T extends CommandItem>(items: readonly T[], query: string): readonly T[] {
  const needle = searchText(query);
  if (needle === "") return items;
  const terms = needle.split(termSeparator);
  return items.filter((item) => {
    const haystack = [item.label, item.description, item.group, ...(item.keywords ?? [])]
      .filter((text): text is string => text !== undefined)
      .map(searchText)
      .join("\n");
    return terms.every((term) => haystack.includes(term));
  });
}

export function commandSections<T extends CommandItem>(items: readonly T[]): readonly CommandSection<T>[] {
  const sections: { group: string | undefined; entries: { item: T; index: number }[] }[] = [];
  items.forEach((item, index) => {
    const last = sections.at(-1);
    if (last !== undefined && last.group === item.group) last.entries.push({ item, index });
    else sections.push({ group: item.group, entries: [{ item, index }] });
  });
  return sections;
}

export function nextCommandIndex(count: number, from: number, direction: CommandDirection): number {
  if (count === 0) return -1;
  if (from < 0 || from >= count) return direction === 1 ? 0 : count - 1;
  return (from + direction + count) % count;
}
