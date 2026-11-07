"use server";

import type {
  CorrectExchangeRateRequest,
  ExchangeRateResponse,
  ImportedExchangeRateRequest,
  ImportedExchangeRatesResponse,
  RecordExchangeRateRequest,
} from "@dewiride/erp-api-client";
import { refresh } from "next/cache";
import { unstable_rethrow } from "next/navigation";
import { z } from "zod";

import { callApi, sendApi } from "@/shared/api/client";
import { apiDateOf } from "@/shared/api/dates/date-only";
import { ApiError } from "@/shared/api/problem-details";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";
import { parseSubmission } from "@/shared/forms/schemas/parse-submission";
import {
  formFailed,
  formSucceeded,
  type FailedFormState,
  type FormState,
  type InvalidFormState,
  type SucceededFormState,
} from "@/shared/forms/state/form-state";

import { todayInIndia } from "../components/exchange-rate-display";
import {
  correctExchangeRateProblemMessages,
  correctExchangeRateSchema,
  importExchangeRatesProblemMessages,
  importExchangeRatesSchema,
  recordExchangeRateSchema,
  type RecordedExchangeRate,
} from "../forms/exchange-rate.schema";

export type ExchangeRateRecordingAnswer =
  InvalidFormState | FailedFormState | (SucceededFormState & { readonly exchangeRateId: string });

export type ExchangeRateImportAnswer =
  | InvalidFormState
  | FailedFormState
  | (SucceededFormState & { readonly recorded: number; readonly unchanged: number });

export type ExchangeRateRemoval =
  | { readonly done: true; readonly alreadyRemoved: boolean }
  | { readonly done: false; readonly message: string; readonly reference?: string | undefined };

const rateId = z.guid();

const rateGone = "exchange-rate.not-found";

const pageOutOfDate = "This page is out of date. Reload the page, then try again.";

export async function recordExchangeRate(
  _previous: FormState,
  submission: unknown,
): Promise<ExchangeRateRecordingAnswer> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.reference-data.exchange-rates.record"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(recordExchangeRateSchema(todayInIndia()), submission, {
    idempotent: true,
  });
  if (!parsed.ok) return parsed.state;

  let recorded: ExchangeRateResponse;
  try {
    recorded = await callApi((client) =>
      client.api.platform.referenceData.exchangeRates.post(recording(parsed.data), {
        headers: { "Idempotency-Key": parsed.idempotencyKey },
      }),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error);
  }
  if (!recorded.id) return formFailed(formMessages.serverFailed);
  return { status: "succeeded", exchangeRateId: recorded.id };
}

export async function importExchangeRates(
  _previous: FormState,
  submission: unknown,
): Promise<ExchangeRateImportAnswer> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.reference-data.exchange-rates.record"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(importExchangeRatesSchema(todayInIndia()), submission, {
    idempotent: true,
  });
  if (!parsed.ok) return parsed.state;

  let imported: ImportedExchangeRatesResponse;
  try {
    imported = await callApi((client) =>
      client.api.platform.referenceData.exchangeRates.imports.post(
        { rates: parsed.data.rates.map(recording) },
        { headers: { "Idempotency-Key": parsed.idempotencyKey } },
      ),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: importExchangeRatesProblemMessages });
  }
  return { status: "succeeded", recorded: imported.recorded ?? 0, unchanged: imported.unchanged ?? 0 };
}

export async function correctExchangeRate(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.reference-data.exchange-rates.correct"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(correctExchangeRateSchema, submission);
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) =>
      client.api.platform.referenceData.exchangeRates.byId(parsed.data.id).put(correction(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: correctExchangeRateProblemMessages });
  }
  refresh();
  return formSucceeded();
}

// The page of a removed rate answers "not found", so the page that removed it moves on itself instead of being refreshed; a
// rate someone else removed first counts as removed.
export async function removeExchangeRate(id: string): Promise<ExchangeRateRemoval> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.reference-data.exchange-rates.correct"))
    return { done: false, message: formMessages.forbidden };
  const parsed = rateId.safeParse(id);
  if (!parsed.success) return { done: false, message: pageOutOfDate };

  try {
    await sendApi((client) => client.api.platform.referenceData.exchangeRates.byId(parsed.data).delete());
  } catch (error) {
    unstable_rethrow(error);
    if (error instanceof ApiError && error.problem.code === rateGone)
      return { done: true, alreadyRemoved: true };
    return refusal(problemToFormState(error));
  }
  return { done: true, alreadyRemoved: false };
}

function recording(values: RecordedExchangeRate): RecordExchangeRateRequest & ImportedExchangeRateRequest {
  return {
    currency: values.currency,
    rateDate: apiDateOf(values.rateDate),
    rate: Number(values.rate),
    unit: values.unit,
    source: values.source ?? null,
  };
}

function correction(values: z.output<typeof correctExchangeRateSchema>): CorrectExchangeRateRequest {
  return {
    rate: Number(values.rate),
    unit: values.unit,
    source: values.source ?? null,
    version: values.version,
  };
}

function refusal(state: InvalidFormState | FailedFormState): ExchangeRateRemoval {
  return state.status === "failed"
    ? { done: false, message: state.message, reference: state.reference }
    : { done: false, message: formMessages.refused };
}
