import { ApiError, type Problem } from "../../api/problem-details.ts";
import { formFailed, formInvalid, type FailedFormState, type InvalidFormState } from "../state/form-state.ts";
import { apiKeyToFormPath, collectValidationErrors, type FieldAliases } from "./field-paths.ts";
import { formMessages } from "./form-messages.ts";
import { problemCodes, queryCodePrefix } from "./problem-codes.ts";

export type ProblemMessages = Readonly<Record<string, string>>;

export type ProblemMapping = { readonly aliases?: FieldAliases; readonly messages?: ProblemMessages };

const sendingFaults: ReadonlySet<string> = new Set([
  problemCodes.requestInvalid,
  problemCodes.requestMalformed,
  problemCodes.idempotencyKeyMissing,
  problemCodes.idempotencyKeyInvalid,
]);

export function problemToFormState(
  error: unknown,
  { aliases = {}, messages = {} }: ProblemMapping = {},
): InvalidFormState | FailedFormState {
  if (error instanceof TypeError) return formFailed(formMessages.unreachable);
  if (!(error instanceof ApiError)) throw error;

  const { status, problem } = error;
  if (status === 0) return formFailed(formMessages.unreachable);
  if (status >= 500 && status <= 599) {
    return formFailed(formMessages.serverFailed, { reference: problem.traceId });
  }
  if (status < 400 || status > 599) throw error;

  if (status === 400 && problem.fields !== undefined) {
    return formInvalid(
      collectValidationErrors(
        Object.entries(problem.fields).flatMap(([key, fieldMessages]) =>
          fieldMessages.map((message) => [apiKeyToFormPath(key, aliases), message] as const),
        ),
      ),
    );
  }

  const mapped =
    problem.code !== undefined && Object.hasOwn(messages, problem.code) ? messages[problem.code] : undefined;
  return formFailed(mapped ?? refusal(status, problem), { code: problem.code, reference: problem.traceId });
}

function refusal(status: number, problem: Problem): string {
  switch (problem.code) {
    case problemCodes.idempotencyInProgress:
      return formMessages.stillSaving;
    case problemCodes.idempotencyKeyReused:
      return formMessages.detailsChanged;
    case problemCodes.idempotencyReplayUnavailable:
      return formMessages.alreadySaved;
    case problemCodes.featureDisabled:
      return formMessages.featureDisabled;
  }

  switch (status) {
    case 400:
      return isSendingFault(problem.code) ? formMessages.notSent : (problem.detail ?? formMessages.notSent);
    case 401:
      return formMessages.sessionEnded;
    case 403:
      return formMessages.forbidden;
    case 404:
      return formMessages.notFound;
    case 429:
      return formMessages.tooManyRequests;
    default:
      return problem.detail ?? formMessages.refused;
  }
}

// These codes mean the web app built the request wrongly; their detail names request types and parameters and is
// written for the server log, never for the person filling in the form.
function isSendingFault(code: string | undefined): boolean {
  return code === undefined || sendingFaults.has(code) || code.startsWith(queryCodePrefix);
}
