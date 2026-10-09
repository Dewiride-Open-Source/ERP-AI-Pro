const combiningMarkPattern = /\p{M}/gu;

export function searchText(text: string): string {
  return text.normalize("NFD").replace(combiningMarkPattern, "").toLowerCase().trim();
}
