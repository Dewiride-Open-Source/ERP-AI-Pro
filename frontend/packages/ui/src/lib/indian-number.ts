export interface AmountFormat {
  readonly scale: number;
  readonly allowNegative: boolean;
}

export type AmountNormalisation = { readonly canonical: string } | { readonly invalid: true };

// decimal(19,4), the column type of every money amount: 15 integer digits and 4 decimals.
export const maxAmountIntegerDigits = 15;
export const maxAmountScale = 4;

interface SanitisedAmount {
  text: string;
  pointSeen: boolean;
  fractionDigits: number;
}

// Text copied from documents and spreadsheets writes a minus as U+2212 MINUS SIGN, U+2013 EN DASH, U+FE63 SMALL
// HYPHEN-MINUS or U+FF0D FULLWIDTH HYPHEN-MINUS, and an accounting negative in parentheses: "(2,500.00)".
const minusCharacters = new Set(["-", "(", ...String.fromCodePoint(0x2212, 0x2013, 0xfe63, 0xff0d)]);

const amountPattern = /^(-?)(\d*)(?:\.(\d*))?$/;
const canonicalAmountPattern = /^(-?)(\d+)(?:\.(\d+))?$/;
const letterPattern = /^[A-Za-z]$/;
const invalidAmount: AmountNormalisation = { invalid: true };

function assertScale(scale: number): void {
  if (!Number.isInteger(scale) || scale < 0 || scale > maxAmountScale) {
    throw new RangeError(`An amount scale is a whole number from 0 to ${maxAmountScale}; received ${scale}.`);
  }
}

function isDigit(character: string): boolean {
  return character >= "0" && character <= "9";
}

// A point straight after a letter ends an abbreviation such as "Rs.", so it never becomes the decimal point.
function acceptsPoint(amount: SanitisedAmount, previous: string): boolean {
  return !amount.pointSeen && !letterPattern.test(previous);
}

function appendDigit(amount: SanitisedAmount, digit: string, format: AmountFormat): void {
  if (!amount.pointSeen) {
    amount.text += digit;
  } else if (amount.fractionDigits < format.scale) {
    amount.fractionDigits += 1;
    amount.text += digit;
  }
}

export function sanitiseAmountText(text: string, format: AmountFormat): string {
  assertScale(format.scale);
  const amount: SanitisedAmount = { text: "", pointSeen: false, fractionDigits: 0 };
  let previous = "";

  for (const character of text) {
    if (isDigit(character)) {
      appendDigit(amount, character, format);
    } else if (character === "." && acceptsPoint(amount, previous)) {
      amount.pointSeen = true;
      amount.text += ".";
    } else if (minusCharacters.has(character) && amount.text === "") {
      amount.text = "-";
    }
    previous = character;
  }

  return amount.text;
}

export function normaliseAmount(text: string, format: AmountFormat): AmountNormalisation {
  assertScale(format.scale);
  const match = amountPattern.exec(text);
  if (!match) return invalidAmount;

  const [, sign = "", integer = "", fraction = ""] = match;
  if (sign !== "" && !format.allowNegative) return invalidAmount;
  if (fraction.length > format.scale) return invalidAmount;

  const significantInteger = integer.replace(/^0+/, "");
  if (significantInteger.length > maxAmountIntegerDigits) return invalidAmount;
  if (integer === "" && fraction === "") return { canonical: "" };

  const digits = fraction === "" ? significantInteger || "0" : `${significantInteger || "0"}.${fraction}`;
  const zero = significantInteger === "" && /^0*$/.test(fraction);
  return { canonical: sign !== "" && !zero ? `-${digits}` : digits };
}

function groupIntegerDigits(integer: string): string {
  if (integer.length <= 3) return integer;
  const groups = [integer.slice(-3)];
  for (let end = integer.length - 3; end > 0; end -= 2) {
    groups.unshift(integer.slice(Math.max(0, end - 2), end));
  }
  return groups.join(",");
}

export function groupIndian(canonical: string, scale: number): string {
  assertScale(scale);
  const match = canonicalAmountPattern.exec(canonical);
  if (!match) return canonical;

  const [, sign = "", integer = "", fraction = ""] = match;
  const paddedFraction = fraction.padEnd(scale, "0");
  const grouped = `${sign}${groupIntegerDigits(integer)}`;
  return paddedFraction === "" ? grouped : `${grouped}.${paddedFraction}`;
}

export function ungroupedOffset(grouped: string, offset: number, canonical: string): number {
  return Math.min(canonical.length, grouped.slice(0, offset).replaceAll(",", "").length);
}
