const wordSeparator = /\s+/u;

export function personInitials(name: string): string {
  const words = name
    .trim()
    .split(wordSeparator)
    .filter((word) => word !== "");
  const first = words[0];
  if (first === undefined) return "";
  const last = words.length > 1 ? words.at(-1) : undefined;
  const initial = (word: string | undefined) => (word === undefined ? "" : (Array.from(word)[0] ?? ""));
  return `${initial(first)}${initial(last)}`.toLocaleUpperCase("en-IN");
}
