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

export const amountIntegerDigits = 15;

export const amountMaximumScale = 4;

const amountDecoration = /[\s,₹]/g;
const decimalText = /^(-?)(\d*)(?:\.(\d*))?$/;
const canonicalDecimal = /^-?(?:0|[1-9]\d*)(?:\.\d+)?$/;
const isoDate = z.iso.date();

export function optionalInput<TOutput>(
  schema: z.ZodType<TOutput, string>,
): z.ZodType<TOutput | undefined, string> {
  return z
    .string()
    .transform((value) => (value.trim() === "" ? undefined : value))
    .pipe(schema.optional());
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
  const { scale, min, max, allowNegative = false, requiredMessage = "Enter an amount." } = options;
  assertAmountOptions(scale, min, max, allowNegative);

  const amount = z
    .string({ error: requiredMessage })
    .overwrite((text) => canonicalAmount(text))
    .superRefine((value, context) => {
      const problem = amountProblem(value, { scale, min, max, allowNegative, requiredMessage });
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
      if (isCalendarDate(range.from) && isCalendarDate(range.to) && range.to < range.from) {
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
  const match = decimalText.exec(bare);
  if (match === null) return bare;

  const [, sign = "", integerText = "", fraction] = match;
  if (integerText === "" && (fraction === undefined || fraction === "")) return bare;

  const integer = integerText.replace(/^0+(?=\d)/, "") || "0";
  const zero = integer === "0" && /^0*$/.test(fraction ?? "");
  return `${zero ? "" : sign}${integer}${fraction ? `.${fraction}` : ""}`;
}

function amountProblem(
  value: string,
  options: {
    readonly scale: number;
    readonly min: string | undefined;
    readonly max: string | undefined;
    readonly allowNegative: boolean;
    readonly requiredMessage: string;
  },
): string | undefined {
  const { scale, min, max, allowNegative, requiredMessage } = options;
  if (value === "") return requiredMessage;
  if (!canonicalDecimal.test(value)) return "Enter the amount in figures, for example 1250.50.";

  const { negative, integer, fraction } = decimalParts(value);
  if (integer.length > amountIntegerDigits) {
    return `Enter an amount with at most ${amountIntegerDigits} digits before the decimal point.`;
  }
  if (fraction.length > scale) {
    return scale === 0
      ? "Enter a whole amount, without paise."
      : `Enter at most ${scale} digits after the decimal point.`;
  }
  if (negative && !allowNegative) return "Enter an amount of zero or more.";
  if (min !== undefined && compareAmounts(value, min) < 0) {
    return `Enter an amount of at least ${rupees(min, scale)}.`;
  }
  if (max !== undefined && compareAmounts(value, max) > 0) {
    return `Enter an amount of at most ${rupees(max, scale)}.`;
  }
  return undefined;
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
  if (!isCalendarDate(value)) return "Enter a real date as day-month-year, for example 31-03-2026.";
  if (min !== undefined && value < min) return `Enter a date on or after ${displayDate(min)}.`;
  if (max !== undefined && value > max) return `Enter a date on or before ${displayDate(max)}.`;
  return undefined;
}

function isCalendarDate(value: string | undefined): value is string {
  return value !== undefined && isoDate.safeParse(value).success;
}

function displayDate(isoValue: string): string {
  const [year, month, day] = isoValue.split("-");
  return `${day}-${month}-${year}`;
}

function decimalParts(value: string): {
  readonly negative: boolean;
  readonly integer: string;
  readonly fraction: string;
} {
  const negative = value.startsWith("-");
  const [integer = "", fraction = ""] = (negative ? value.slice(1) : value).split(".");
  return { negative, integer, fraction };
}

function compareAmounts(left: string, right: string): number {
  const a = decimalParts(left);
  const b = decimalParts(right);
  if (a.negative !== b.negative) return a.negative ? -1 : 1;
  const magnitude = compareMagnitudes(a, b);
  return a.negative ? -magnitude : magnitude;
}

function compareMagnitudes(
  a: { readonly integer: string; readonly fraction: string },
  b: { readonly integer: string; readonly fraction: string },
): number {
  if (a.integer.length !== b.integer.length) return a.integer.length < b.integer.length ? -1 : 1;
  if (a.integer !== b.integer) return a.integer < b.integer ? -1 : 1;
  const length = Math.max(a.fraction.length, b.fraction.length);
  const fractionA = a.fraction.padEnd(length, "0");
  const fractionB = b.fraction.padEnd(length, "0");
  if (fractionA === fractionB) return 0;
  return fractionA < fractionB ? -1 : 1;
}

function rupees(amount: string, scale: number): string {
  const { negative, integer, fraction } = decimalParts(amount);
  const head = integer.slice(0, -3);
  const grouped = head === "" ? integer : `${head.replace(/\B(?=(\d{2})+$)/g, ",")},${integer.slice(-3)}`;
  const paise = scale > 0 ? `.${fraction.padEnd(scale, "0")}` : "";
  return `${negative ? "-" : ""}₹${grouped}${paise}`;
}

function assertAmountOptions(
  scale: number,
  min: string | undefined,
  max: string | undefined,
  allowNegative: boolean,
): void {
  if (!Number.isInteger(scale) || scale < 0 || scale > amountMaximumScale) {
    throw new RangeError(
      `An amount's scale is a whole number from 0 to ${amountMaximumScale}; received ${scale}.`,
    );
  }
  for (const bound of [min, max]) {
    if (bound === undefined) continue;
    const { negative, integer, fraction } = decimalParts(bound);
    const fits =
      canonicalDecimal.test(bound) && integer.length <= amountIntegerDigits && fraction.length <= scale;
    if (!fits || (negative && !allowNegative)) {
      throw new RangeError(`The amount bound ${bound} is not an amount this field accepts.`);
    }
  }
  if (min !== undefined && max !== undefined && compareAmounts(min, max) > 0) {
    throw new RangeError(`The amount bounds are reversed: ${min} is above ${max}.`);
  }
}

function assertDateBounds(min: string | undefined, max: string | undefined): void {
  for (const bound of [min, max]) {
    if (bound !== undefined && !isCalendarDate(bound)) {
      throw new RangeError(`The date bound ${bound} is not a yyyy-MM-dd calendar date.`);
    }
  }
  if (min !== undefined && max !== undefined && min > max) {
    throw new RangeError(`The date bounds are reversed: ${min} is after ${max}.`);
  }
}
