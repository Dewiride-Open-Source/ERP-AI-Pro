import { compareIsoDates, formatDisplayDate, isIsoDate } from "@dewiride/erp-ui/lib/calendar-date";
import {
  groupIndian,
  maxAmountIntegerDigits,
  maxAmountScale,
  normaliseAmount,
  type AmountFormat,
} from "@dewiride/erp-ui/lib/indian-number";
import { z } from "zod";

export type TextFieldSchema<TRequired extends boolean> = z.ZodType<
  TRequired extends true ? string : string | undefined,
  string
>;

export type AmountOptions<TRequired extends boolean> = {
  readonly scale: number;
  readonly required: TRequired;
  readonly min?: string | undefined;
  readonly max?: string | undefined;
  readonly allowNegative?: boolean | undefined;
  readonly requiredMessage?: string | undefined;
};

export type CalendarDateOptions<TRequired extends boolean> = {
  readonly required: TRequired;
  readonly min?: string | undefined;
  readonly max?: string | undefined;
  readonly requiredMessage?: string | undefined;
};

export type DateRangeOptions<TRequired extends boolean> = {
  readonly required: TRequired;
  readonly min?: string | undefined;
  readonly max?: string | undefined;
};

type AmountParts = { readonly negative: boolean; readonly integer: string; readonly fraction: string };

type AmountBound = { readonly parts: AmountParts; readonly shown: string };

type AmountRules = {
  readonly format: AmountFormat;
  readonly min: AmountBound | undefined;
  readonly max: AmountBound | undefined;
  readonly requiredMessage: string;
};

const amountDecoration = /[\s,₹]/g;
const amountFigures = /^(-?)(\d+)(?:\.(\d+))?$/;
const leadingZeros = /^0+(?=\d)/;
const anyAmount: AmountFormat = { scale: maxAmountScale, allowNegative: true };
const realDateMessage = "Enter a real date as day-month-year, for example 31-03-2026.";

export function optionalInput<TOutput>(
  schema: z.ZodType<TOutput, string>,
): z.ZodType<TOutput | undefined, string> {
  return z
    .string()
    .nullish()
    .transform((value) => (value === undefined || value === null || value.trim() === "" ? undefined : value))
    .pipe(schema.optional()) as z.ZodType<TOutput | undefined, string>;
}

export function requiredText(
  maxLength: number,
  {
    required = "Enter a value.",
    tooLong = `Use ${maxLength} characters or fewer.`,
  }: {
    readonly required?: string;
    readonly tooLong?: string;
  } = {},
) {
  return z
    .string({ error: required })
    .trim()
    .min(1, { error: required, abort: true })
    .max(maxLength, { error: tooLong });
}

export function amountSchema<TRequired extends boolean>(
  options: AmountOptions<TRequired>,
): TextFieldSchema<TRequired> {
  const { scale, allowNegative = false, requiredMessage = "Enter an amount." } = options;
  if (!Number.isInteger(scale) || scale < 0 || scale > maxAmountScale) {
    throw new RangeError(
      `An amount's scale is a whole number from 0 to ${maxAmountScale}; received ${scale}.`,
    );
  }

  const format: AmountFormat = { scale, allowNegative };
  const min = amountBound(options.min, format);
  const max = amountBound(options.max, format);
  if (min !== undefined && max !== undefined && compareAmounts(min.parts, max.parts) > 0) {
    throw new RangeError(`The amount bounds are reversed: ${options.min} is above ${options.max}.`);
  }

  const rules: AmountRules = { format, min, max, requiredMessage };
  const amount = z
    .string({ error: requiredMessage })
    .overwrite(canonicalAmount)
    .superRefine((value, context) => {
      const problem = amountProblem(value, rules);
      if (problem !== undefined) context.addIssue({ code: "custom", message: problem });
    });
  return (options.required ? amount : optionalInput(amount)) as TextFieldSchema<TRequired>;
}

export function calendarDateSchema<TRequired extends boolean>(
  options: CalendarDateOptions<TRequired>,
): TextFieldSchema<TRequired> {
  const { min, max, requiredMessage = "Enter a date." } = options;
  assertDateBounds(min, max);

  const date = z
    .string({ error: requiredMessage })
    .trim()
    .superRefine((value, context) => {
      const problem = dateProblem(value, { min, max, requiredMessage });
      if (problem !== undefined) context.addIssue({ code: "custom", message: problem });
    });
  return (options.required ? date : optionalInput(date)) as TextFieldSchema<TRequired>;
}

export function dateRangeSchema<TRequired extends boolean>(options: DateRangeOptions<TRequired>) {
  const bounds = { required: options.required, min: options.min, max: options.max };
  return z
    .object({
      from: calendarDateSchema({ ...bounds, requiredMessage: "Enter the start date." }),
      to: calendarDateSchema({ ...bounds, requiredMessage: "Enter the end date." }),
    })
    .superRefine((range, context) => {
      if (
        isCalendarDate(range.from) &&
        isCalendarDate(range.to) &&
        compareIsoDates(range.to, range.from) < 0
      ) {
        context.addIssue({
          code: "custom",
          path: ["to"],
          message: "The end date must be on or after the start date.",
        });
      }
    });
}

function canonicalAmount(text: string): string {
  const bare = text.replace(amountDecoration, "");
  const normalised = normaliseAmount(bare, anyAmount);
  return "canonical" in normalised && normalised.canonical !== "" ? normalised.canonical : bare;
}

function amountProblem(
  value: string,
  { format, min, max, requiredMessage }: AmountRules,
): string | undefined {
  if (value === "") return requiredMessage;

  const parts = amountParts(value);
  if (parts === undefined) return "Enter the amount in figures, for example 1250.50.";
  if (parts.integer.replace(leadingZeros, "").length > maxAmountIntegerDigits) {
    return `Enter an amount with at most ${maxAmountIntegerDigits} digits before the decimal point.`;
  }
  if (parts.fraction.length > format.scale) {
    return format.scale === 0
      ? "Enter a whole amount, without paise."
      : `Enter at most ${format.scale} digits after the decimal point.`;
  }
  if (parts.negative && !format.allowNegative) return "Enter an amount of zero or more.";
  if (min !== undefined && compareAmounts(parts, min.parts) < 0)
    return `Enter an amount of at least ${min.shown}.`;
  if (max !== undefined && compareAmounts(parts, max.parts) > 0)
    return `Enter an amount of at most ${max.shown}.`;
  return undefined;
}

function amountBound(bound: string | undefined, format: AmountFormat): AmountBound | undefined {
  if (bound === undefined) return undefined;

  const parts = amountParts(bound);
  const normalised = normaliseAmount(bound, format);
  if (parts === undefined || !("canonical" in normalised) || normalised.canonical !== bound) {
    throw new RangeError(`The amount bound ${bound} is not an amount this field accepts.`);
  }

  const grouped = groupIndian(
    parts.fraction === "" ? parts.integer : `${parts.integer}.${parts.fraction}`,
    format.scale,
  );
  return { parts, shown: `${parts.negative ? "-" : ""}₹${grouped}` };
}

function amountParts(value: string): AmountParts | undefined {
  const match = amountFigures.exec(value);
  if (match === null) return undefined;

  const [, sign = "", integer = "", fraction = ""] = match;
  return { negative: sign !== "", integer, fraction };
}

function compareAmounts(a: AmountParts, b: AmountParts): number {
  if (a.negative !== b.negative) return a.negative ? -1 : 1;
  const magnitude = compareMagnitudes(a, b);
  return a.negative ? -magnitude : magnitude;
}

function compareMagnitudes(a: AmountParts, b: AmountParts): number {
  if (a.integer.length !== b.integer.length) return a.integer.length < b.integer.length ? -1 : 1;
  if (a.integer !== b.integer) return a.integer < b.integer ? -1 : 1;
  const length = Math.max(a.fraction.length, b.fraction.length);
  const fractionA = a.fraction.padEnd(length, "0");
  const fractionB = b.fraction.padEnd(length, "0");
  if (fractionA === fractionB) return 0;
  return fractionA < fractionB ? -1 : 1;
}

function dateProblem(
  value: string,
  options: {
    readonly min: string | undefined;
    readonly max: string | undefined;
    readonly requiredMessage: string;
  },
): string | undefined {
  const { min, max, requiredMessage } = options;
  if (value === "") return requiredMessage;
  if (!isIsoDate(value)) return realDateMessage;
  if (min !== undefined && compareIsoDates(value, min) < 0) {
    return `Enter a date on or after ${formatDisplayDate(min)}.`;
  }
  if (max !== undefined && compareIsoDates(value, max) > 0) {
    return `Enter a date on or before ${formatDisplayDate(max)}.`;
  }
  return undefined;
}

function isCalendarDate(value: string | undefined): value is string {
  return value !== undefined && isIsoDate(value);
}

function assertDateBounds(min: string | undefined, max: string | undefined): void {
  for (const bound of [min, max]) {
    if (bound !== undefined && !isIsoDate(bound)) {
      throw new RangeError(`The date bound ${bound} is not a yyyy-MM-dd calendar date.`);
    }
  }
  if (min !== undefined && max !== undefined && compareIsoDates(min, max) > 0) {
    throw new RangeError(`The date bounds are reversed: ${min} is after ${max}.`);
  }
}
