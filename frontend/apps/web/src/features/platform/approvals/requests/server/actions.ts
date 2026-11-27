"use server";

import type {
  ApprovalDecisionRequest,
  ApprovalRequestResponse,
  ApprovalWithdrawalRequest,
} from "@dewiride/erp-api-client";
import { refresh } from "next/cache";
import { unstable_rethrow } from "next/navigation";

import { callApi } from "@/shared/api/client";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";
import { parseSubmission } from "@/shared/forms/schemas/parse-submission";
import { formFailed, formSucceeded, type FormState } from "@/shared/forms/state/form-state";

import { actionDoneMessage } from "../components/request/decision-text";
import {
  requestActionProblemMessages,
  requestActionSchema,
  type RequestActionSubmission,
} from "../forms/request-action.schema";

export async function approveApprovalRequest(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.approvals.requests.decide"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(requestActionSchema("approve"), submission);
  if (!parsed.ok) return parsed.state;

  let answer: ApprovalRequestResponse;
  try {
    answer = await callApi((client) =>
      client.api.platform.approvals.requests.byId(parsed.data.id).approve.post(decision(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: requestActionProblemMessages });
  }
  refresh();
  return formSucceeded(actionDoneMessage("approve", answer, person.id));
}

export async function returnApprovalRequest(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.approvals.requests.decide"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(requestActionSchema("return"), submission);
  if (!parsed.ok) return parsed.state;

  let answer: ApprovalRequestResponse;
  try {
    answer = await callApi((client) =>
      client.api.platform.approvals.requests.byId(parsed.data.id).returnEscaped.post(decision(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: requestActionProblemMessages });
  }
  refresh();
  return formSucceeded(actionDoneMessage("return", answer, person.id));
}

export async function rejectApprovalRequest(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.approvals.requests.decide"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(requestActionSchema("reject"), submission);
  if (!parsed.ok) return parsed.state;

  let answer: ApprovalRequestResponse;
  try {
    answer = await callApi((client) =>
      client.api.platform.approvals.requests.byId(parsed.data.id).reject.post(decision(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: requestActionProblemMessages });
  }
  refresh();
  return formSucceeded(actionDoneMessage("reject", answer, person.id));
}

export async function withdrawApprovalRequest(_previous: FormState, submission: unknown): Promise<FormState> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.approvals.requests.withdraw"))
    return formFailed(formMessages.forbidden);
  const parsed = await parseSubmission(requestActionSchema("withdraw"), submission);
  if (!parsed.ok) return parsed.state;

  let answer: ApprovalRequestResponse;
  try {
    answer = await callApi((client) =>
      client.api.platform.approvals.requests.byId(parsed.data.id).withdraw.post(withdrawal(parsed.data)),
    );
  } catch (error) {
    unstable_rethrow(error);
    return problemToFormState(error, { messages: requestActionProblemMessages });
  }
  refresh();
  return formSucceeded(actionDoneMessage("withdraw", answer, person.id));
}

function decision(values: RequestActionSubmission): ApprovalDecisionRequest {
  return { version: values.version, comment: values.comment };
}

function withdrawal(values: RequestActionSubmission): ApprovalWithdrawalRequest {
  return { version: values.version, comment: values.comment };
}
