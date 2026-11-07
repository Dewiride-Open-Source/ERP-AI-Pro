import type { ExchangeRateResponse } from "@dewiride/erp-api-client";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";
import { groupIndian } from "@dewiride/erp-ui/lib/indian-number";

import { isoDateOf } from "@/shared/api/dates/date-only";
import { istDateKey } from "@/shared/format/dates";

import {
  currencyOptions,
  type CurrencyItem,
  type ListRead,
} from "../../reference-lists/components/reference-options.ts";
import { indianRupee, rateScale } from "../forms/exchange-rate.schema.ts";

export interface ExchangeRateRecord {
  readonly id: string;
  readonly currency: string;
  readonly rateDate: string;
  readonly rate: number;
  readonly unit: number;
  readonly source: string | null;
  readonly version: string;
}

export interface ExchangeRateListRow {
  readonly id: string;
  readonly name: string;
  readonly day: string;
  readonly currency: string;
  readonly rate: string;
  readonly unit: string;
  readonly source: string;
}

export interface CurrencyFilterOption {
  readonly value: string;
  readonly label: string;
}

export interface Removal {
  readonly title: string;
  readonly description: string;
  readonly confirm: string;
  readonly cancel: string;
  readonly done: string;
  readonly gone: string;
}

export const sourceNotGiven = "Not given";

export const currenciesUnavailable =
  "Currencies could not be read, so the list cannot be filtered by currency now.";

export function exchangeRateRecord(response: ExchangeRateResponse): ExchangeRateRecord | undefined {
  const { id, currency, rate, unit, version } = response;
  const rateDate = isoDateOf(response.rateDate);
  if (!id || !currency || rateDate === undefined || typeof rate !== "number" || typeof unit !== "number") {
    return undefined;
  }
  return version
    ? { id, currency, rateDate, rate, unit, source: response.source || null, version }
    : undefined;
}

// The list's rows reach the browser as the texts it shows, so its page needs none of the rules that read them.
export function exchangeRateListRow(response: ExchangeRateResponse): ExchangeRateListRow | undefined {
  const recorded = exchangeRateRecord(response);
  if (recorded === undefined) return undefined;
  const { id, currency, rateDate, rate, unit, source } = recorded;
  return {
    id,
    name: exchangeRateName(currency, rateDate),
    day: formatDisplayDate(rateDate),
    currency,
    rate: rateText(rate),
    unit: unitText(unit),
    source: sourceText(source),
  };
}

export function exchangeRateName(currency: string, rateDate: string): string {
  return `${currency} on ${formatDisplayDate(rateDate)}`;
}

export function rateText(rate: number): string {
  return `₹${groupIndian(rateInputValue(rate), rateScale)}`;
}

export function rateInputValue(rate: number): string {
  return rate.toFixed(rateScale);
}

export function unitText(unit: number): string {
  return groupIndian(String(unit), 0);
}

export function rateForUnitText(rate: number, unit: number, currency: string): string {
  return `${rateText(rate)} for ${unitText(unit)} ${currency}`;
}

export function sourceText(source: string | null): string {
  return source ?? sourceNotGiven;
}

export function todayInIndia(): string {
  return istDateKey(new Date());
}

export function recordedMessage(currency: string, rateDate: string): string {
  return `Recorded the rate of ${currency} for ${formatDisplayDate(rateDate)}.`;
}

export function correctedMessage(currency: string, rateDate: string): string {
  return `Corrected the rate of ${exchangeRateName(currency, rateDate)}.`;
}

export function removal(currency: string, rateDate: string): Removal {
  const name = exchangeRateName(currency, rateDate);
  return {
    title: `Remove the rate of ${name}?`,
    description: `The rate leaves the list, and its history stays in the audit trail. A rate of ${currency} can then be recorded for ${formatDisplayDate(rateDate)} again.`,
    confirm: "Remove the rate",
    cancel: "Keep the rate",
    done: `Removed the rate of ${name}.`,
    gone: "It had already been removed.",
  };
}

// A chosen code the list cannot name, because the list could not be read, stays chosen under its code alone.
export function currencyFilterOptions(
  list: ListRead<CurrencyItem>,
  chosen: readonly string[],
): readonly CurrencyFilterOption[] {
  const known = currencyOptions(list, null, [indianRupee]).map(({ value, label }) => ({ value, label }));
  const offered = new Set(known.map((option) => option.value));
  return [
    ...chosen.filter((code) => !offered.has(code)).map((code) => ({ value: code, label: code })),
    ...known,
  ];
}

export function currencyFilterNote(
  list: ListRead<CurrencyItem>,
  chosen: readonly string[],
): string | undefined {
  return list.status === "failed" && chosen.length === 0 ? currenciesUnavailable : undefined;
}

// A code the read list does not hold, the rupee included, matches no rate and would make the API refuse the whole filter, so
// the list leaves it out; with no list to compare against, the API decides.
export function knownCurrencies(list: ListRead<CurrencyItem>, chosen: readonly string[]): readonly string[] {
  if (list.status !== "read") return chosen;
  const foreign = new Set(list.items.map((item) => item.code).filter((code) => code !== indianRupee));
  return chosen.filter((code) => foreign.has(code));
}
