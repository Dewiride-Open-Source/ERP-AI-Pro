"use server";

import type {
  ChangeNumberSeriesRequest,
  NumberFormatRequest,
  SetUpNumberSeriesRequest,
} from "@dewiride/erp-api-client";
import { refresh } from "next/cache";
import { unstable_rethrow } from "next/navigation";
import type { z } from "zod";

import { callApi } from "@/shared/api/client";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";
import { parseSubmission } from "@/shared/forms/schemas/parse-submission";
import { formFailed, formSucceeded, type FormState } from "@/shared/forms/state/form-state";

import type { NumberFormat } from "../components/number-format";
import {
  changeNumberSeriesSchema,
  numberSeriesProblemMessages,
  setUpNumberSeriesSchema,
} from "../forms/number-series.schema";

export async function setUpNumberSeries(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.numbering.series.change")) return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(setUpNumberSeriesSchema(), submission, { idempotent: true });
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) =>
      client.api.platform.numbering.series.post(setUp(parsed.data), {
        headers: { "Idempotency-Key": parsed.idempotencyKey },
      }),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: numberSeriesProblemMessages });
  }
  refresh();
  return formSucceeded();
}

export async function changeNumberSeries(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.numbering.series.change")) return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(changeNumberSeriesSchema(), submission);
  if (!parsed.ok) return parsed.state;

  try {
    await callApi((client) =>
      client.api.platform.numbering.series.byId(parsed.data.id).put(change(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: numberSeriesProblemMessages });
  }
  refresh();
  return formSucceeded();
}

function setUp(values: z.output<ReturnType<typeof setUpNumberSeriesSchema>>): SetUpNumberSeriesRequest {
  return {
    documentType: values.documentType,
    format: formatRequest(values.format),
    nextNumber: values.nextNumber,
  };
}

function change(values: z.output<ReturnType<typeof changeNumberSeriesSchema>>): ChangeNumberSeriesRequest {
  return {
    format: formatRequest(values.format),
    nextNumber: values.nextNumber,
    version: values.version,
  };
}

function formatRequest(format: NumberFormat): NumberFormatRequest {
  return { prefix: format.prefix, year: format.year, separator: format.separator, digits: format.digits };
}
