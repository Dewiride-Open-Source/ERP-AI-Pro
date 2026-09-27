import { z } from "zod";

export const identifierLengths = { gstin: 15, pan: 10, ifsc: 11 } as const;

const outsideIdentifier = /[^0-9A-Z]/g;

// GST Council, "Registration under GST Law" (first two digits the State code), and CBIC, "FAQs: Registration" Q2.1
// (a 15-character GSTIN); shape only, because no allow-listed source publishes the check character.
export const gstinSchema = identifierSchema(
  "Enter the GSTIN.",
  /^[0-9]{2}[0-9A-Z]{13}$/,
  "Enter a 15-character GSTIN: two digits, then 13 letters or digits.",
);

// Income Tax Department, ITR-3 AY 2026-27 JSON schema V1.1 on incometax.gov.in: PAN pattern [A-Z]{5}[0-9]{4}[A-Z].
export const panSchema = identifierSchema(
  "Enter the PAN.",
  /^[A-Z]{5}[0-9]{4}[A-Z]$/,
  "Enter a 10-character PAN: five letters, four digits, then a letter.",
);

// Income Tax Department, ITR-3 AY 2026-27 JSON schema V1.1 on incometax.gov.in: IFSCCode pattern [A-Z]{4}[0][A-Z0-9]{6}.
export const ifscSchema = identifierSchema(
  "Enter the IFSC.",
  /^[A-Z]{4}0[A-Z0-9]{6}$/,
  "Enter an 11-character IFSC: four letters, a zero, then six letters or digits.",
);

function identifierSchema(required: string, shape: RegExp, malformed: string) {
  return z
    .string({ error: required })
    .overwrite((value) => value.toUpperCase().replace(outsideIdentifier, ""))
    .min(1, { error: required, abort: true })
    .regex(shape, { error: malformed });
}
