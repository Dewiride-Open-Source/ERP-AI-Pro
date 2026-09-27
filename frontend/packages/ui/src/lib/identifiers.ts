const identifierCharacterPattern = /^[0-9A-Za-z]$/;

export function normaliseIdentifier(text: string, maxLength: number): string {
  if (!Number.isInteger(maxLength) || maxLength < 1) {
    throw new RangeError(`An identifier length is a whole number of at least 1; received ${maxLength}.`);
  }

  let result = "";
  for (const character of text) {
    if (result.length === maxLength) break;
    if (identifierCharacterPattern.test(character)) result += character.toUpperCase();
  }
  return result;
}
