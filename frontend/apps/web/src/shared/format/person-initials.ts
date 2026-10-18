const wordSeparator = /\s+/u;

const graphemes = new Intl.Segmenter("en-IN", { granularity: "grapheme" });

function initial(word: string | undefined): string {
  if (word === undefined) return "";
  return graphemes.segment(word)[Symbol.iterator]().next().value?.segment ?? "";
}

export function personInitials(name: string): string {
  const words = name
    .trim()
    .split(wordSeparator)
    .filter((word) => word !== "");
  const first = words[0];
  if (first === undefined) return "";
  const last = words.length > 1 ? words.at(-1) : undefined;
  return `${initial(first)}${initial(last)}`.normalize("NFC").toLocaleUpperCase("en-IN");
}
