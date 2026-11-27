"use server";

import type {
  ApprovalKindResponse,
  ChangeApprovalKindSettingRequest,
  SetUpApprovalKindSettingRequest,
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

import { settingSavedMessage } from "../components/kind-text";
import {
  changeKindSettingSchema,
  kindSettingProblemMessages,
  setUpKindSettingSchema,
} from "../forms/kind-setting.schema";

export async function setUpApprovalKindSetting(
  _previous: FormState,
  submission: unknown,
): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.approvals.kinds.change")) return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(setUpKindSettingSchema, submission, { idempotent: true });
  if (!parsed.ok) return parsed.state;

  let answer: ApprovalKindResponse;
  try {
    answer = await callApi((client) =>
      client.api.platform.approvals.kindSettings.post(setUp(parsed.data), {
        headers: { "Idempotency-Key": parsed.idempotencyKey },
      }),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: kindSettingProblemMessages });
  }
  refresh();
  return formSucceeded(savedMessage(answer));
}

export async function changeApprovalKindSetting(
  _previous: FormState,
  submission: unknown,
): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.approvals.kinds.change")) return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(changeKindSettingSchema, submission);
  if (!parsed.ok) return parsed.state;

  let answer: ApprovalKindResponse;
  try {
    answer = await callApi((client) =>
      client.api.platform.approvals.kindSettings.byId(parsed.data.id).put(change(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: kindSettingProblemMessages });
  }
  refresh();
  return formSucceeded(savedMessage(answer));
}

function setUp(values: z.output<typeof setUpKindSettingSchema>): SetUpApprovalKindSettingRequest {
  return { kind: values.kind, required: values.required, workingDaysToDecide: values.workingDaysToDecide };
}

function change(values: z.output<typeof changeKindSettingSchema>): ChangeApprovalKindSettingRequest {
  return {
    required: values.required,
    workingDaysToDecide: values.workingDaysToDecide,
    version: values.version,
  };
}

// The message words the setting as the API saved it, so it never repeats what the form sent.
function savedMessage(answer: ApprovalKindResponse): string | undefined {
  const title = answer.title?.trim();
  const required = answer.setting?.required;
  const days = answer.setting?.workingDaysToDecide;
  return title && typeof required === "boolean" && typeof days === "number"
    ? settingSavedMessage(title, required, days)
    : undefined;
}
