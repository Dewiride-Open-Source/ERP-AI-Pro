"use server";

import { ApiError } from "@/shared/api/problem-details";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";
import { parseSubmission } from "@/shared/forms/schemas/parse-submission";
import { formSucceeded, type FormState } from "@/shared/forms/state/form-state";

import {
  supplierAlreadyRegisteredCode,
  supplierExampleFieldAliases,
  supplierExampleProblemMessages,
  supplierExampleSchema,
  type ServerAnswer,
} from "../forms/supplier-example.schema";

const exampleTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

export async function registerSupplierExample(_previous: FormState, submission: unknown): Promise<FormState> {
  const parsed = await parseSubmission(supplierExampleSchema, submission, { idempotent: true });
  if (!parsed.ok) return parsed.state;

  const failure = rehearsedFailure(parsed.data.serverAnswer);
  if (failure === undefined) return formSucceeded("Saved as an example. Nothing was stored.");
  return problemToFormState(failure, {
    aliases: supplierExampleFieldAliases,
    messages: supplierExampleProblemMessages,
  });
}

// The page demonstrates every answer a create endpoint can give without calling the API, so each failure is the error
// shared/api/client.ts would raise for that answer, mapped by the same function a real form uses.
function rehearsedFailure(answer: ServerAnswer): Error | undefined {
  switch (answer) {
    case "accept":
      return undefined;
    case "field-error":
      return validationProblem({
        legalName: ["A supplier with this legal name is already registered."],
      });
    case "nested-problem":
      return validationProblem({
        "": ["Check these details against the supplier's registration certificate."],
        "Registration.Gstin": ["This GSTIN is registered to a different PAN."],
        "bankAccount.Ifsc": ["No bank branch uses this IFSC."],
        "balances[0].Amount": ["The opening balance is above the credit limit agreed with the supplier."],
        "agreement.Validity.To": ["The agreement must run until at least the end of the financial year."],
      });
    case "conflict":
      return new ApiError({
        status: 409,
        code: supplierAlreadyRegisteredCode,
        title: "The supplier is already registered.",
        detail: "A supplier with GSTIN 29AABCG1234K1Z5 already exists.",
        traceId: exampleTraceId,
      });
    case "server-error":
      return new ApiError({
        status: 500,
        code: "server.error",
        title: "An unexpected error occurred.",
        traceId: exampleTraceId,
      });
    case "unreachable":
      return new TypeError("fetch failed", {
        cause: Object.assign(new Error("connect ECONNREFUSED 127.0.0.1:5080"), { code: "ECONNREFUSED" }),
      });
  }
}

function validationProblem(fields: Readonly<Record<string, readonly string[]>>): ApiError {
  return new ApiError({
    status: 400,
    code: "request.invalid",
    title: "One or more validation errors occurred.",
    detail: "RegisterSupplierRequest is invalid.",
    traceId: exampleTraceId,
    fields,
  });
}
