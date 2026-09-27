export interface EditableText {
  value: string;
  readonly selectionEnd: number | null;
  setSelectionRange(start: number, end: number): void;
}

// The transform must map every prefix of a text to a prefix of the text's own result, so the caret lands after the
// characters that were kept in front of it.
export function replaceInputText(input: EditableText, transform: (text: string) => string): string {
  const raw = input.value;
  const text = transform(raw);
  if (text === raw) return text;

  const caret = transform(raw.slice(0, input.selectionEnd ?? raw.length)).length;
  input.value = text;
  input.setSelectionRange(caret, caret);
  return text;
}
