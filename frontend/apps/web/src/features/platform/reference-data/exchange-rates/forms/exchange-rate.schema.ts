import { z } from "zod";

import { formMessages } from "@/shared/forms/errors/form-messages";
import type { ProblemMessages } from "@/shared/forms/errors/problem-to-form-state";
import { amountSchema, calendarDateSchema, optionalInput } from "@/shared/forms/schemas/field-schemas";

export const exchangeRateUnits = [1, 10, 100, 1_000, 10_000, 100_000] as const;

export type ExchangeRateUnit = (typeof exchangeRateUnits)[number];

export const rateScale = 4;

export const maxRate = "1000000";

export const sourceMaxLength = 200;

export const recordVersionMaxLength = 64;

export const maxImportedRates = 1_000;

export const indianRupee = "INR";

export const currencyCodePattern = /^[A-Z]{3}$/;

export const changedMeanwhileCode = "concurrency.conflict";

const currencyMissing = "Choose the currency.";

const unitMissing = "Choose how many units of the currency the rate is for.";

const unitTexts: ReadonlySet<string> = new Set(exchangeRateUnits.map(String));

const zeroAmount = /^-?0+(?:\.0+)?$/;

const currency = z
  .string({ error: currencyMissing })
  .trim()
  .toUpperCase()
  .min(1, { error: currencyMissing, abort: true })
  .regex(currencyCodePattern, { error: "Choose the currency from the list.", abort: true })
  .refine((code) => code !== indianRupee, {
    error: "Choose a foreign currency; the rupee has no rate against itself.",
  });

const rate = amountSchema({
  scale: rateScale,
  required: true,
  max: maxRate,
  requiredMessage: "Enter the rate.",
}).refine((value) => !zeroAmount.test(value), { error: "Enter a rate of more than ₹0." });

const unit = z
  .string({ error: unitMissing })
  .transform((text) => text.replace(/[\s,]/g, ""))
  .refine((text) => unitTexts.has(text), { error: unitMissing })
  .transform((text) => Number(text) as ExchangeRateUnit);

const source = optionalInput(
  z
    .string()
    .trim()
    .max(sourceMaxLength, { error: `Use ${sourceMaxLength} characters or fewer.` }),
);

const version = z
  .string({ error: formMessages.pageOutOfDate })
  .min(1, { error: formMessages.pageOutOfDate })
  .max(recordVersionMaxLength, { error: formMessages.pageOutOfDate });

// A rate is recorded for today or an earlier day by India's clock, so the latest day is given by whoever validates: the page
// as it renders and the Server Function as it runs.
export function recordExchangeRateSchema(today: string) {
  return z.object({
    currency,
    rateDate: calendarDateSchema({
      required: true,
      max: today,
      requiredMessage: "Enter the day the rate is for.",
    }),
    rate,
    unit,
    source,
  });
}

export type RecordExchangeRateValues = z.input<ReturnType<typeof recordExchangeRateSchema>>;

export type RecordedExchangeRate = z.output<ReturnType<typeof recordExchangeRateSchema>>;

export const recordExchangeRateValues: RecordExchangeRateValues = {
  currency: "",
  rateDate: "",
  rate: "",
  unit: "1",
  source: "",
};

export const correctExchangeRateSchema = z.object({
  id: z.guid({ error: formMessages.pageOutOfDate }),
  rate,
  unit,
  source,
  version,
});

export type CorrectExchangeRateValues = z.input<typeof correctExchangeRateSchema>;

export function correctExchangeRateValues(recorded: {
  readonly id: string;
  readonly rate: string;
  readonly unit: number;
  readonly source: string | null;
  readonly version: string;
}): CorrectExchangeRateValues {
  return {
    id: recorded.id,
    rate: recorded.rate,
    unit: String(recorded.unit),
    source: recorded.source ?? "",
    version: recorded.version,
  };
}

export function importExchangeRatesSchema(today: string) {
  return z.object({
    rates: z
      .array(recordExchangeRateSchema(today))
      .min(1, { error: "Choose a file that holds at least one rate." })
      .max(maxImportedRates, {
        error: `An import takes at most ${maxImportedRates.toLocaleString("en-IN")} rates; split the file.`,
      }),
  });
}

export type ImportExchangeRatesValues = z.input<ReturnType<typeof importExchangeRatesSchema>>;

export const correctExchangeRateProblemMessages: ProblemMessages = {
  [changedMeanwhileCode]:
    "Someone changed this rate after the page was loaded. Show the latest rate, then correct it again.",
  "exchange-rate.not-found":
    "This rate was removed after the page was loaded, so it can no longer be corrected.",
};

export const importExchangeRatesProblemMessages: ProblemMessages = {
  [changedMeanwhileCode]:
    "Someone recorded a rate of one of these currencies and days while the file was being imported, so no rate of the file was recorded. Import the file again.",
};
