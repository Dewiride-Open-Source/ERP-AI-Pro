"use server";

import type { DirectoryAccountCheckResponse } from "@dewiride/erp-api-client";
import { refresh } from "next/cache";
import { unstable_rethrow } from "next/navigation";

import { callApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";

import { refusedOutcome } from "../../details/components/person-action-text";
import {
  accountCheckFailure,
  accountCheckResult,
  type AccountCheckAnswer,
} from "../components/account-check";

export async function checkMicrosoftAccounts(): Promise<AccountCheckAnswer> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "identity.users.people.change-status"))
    return { status: "refused", message: formMessages.forbidden };

  let answer: DirectoryAccountCheckResponse;
  try {
    answer = await callApi((client) => client.api.identity.users.directory.accountCheck.post());
  } catch (error) {
    unstable_rethrow(error);
    const failed =
      (error instanceof ApiError ? accountCheckFailure(error) : undefined) ??
      refusedOutcome(problemToFormState(error));
    return { status: "refused", message: failed.message, reference: failed.reference };
  }
  const result = accountCheckResult(answer);
  if (result.deactivated.length > 0) refresh();
  return result;
}
