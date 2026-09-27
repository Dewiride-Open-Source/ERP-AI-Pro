import { z } from "zod";

import { issuesToFieldErrors } from "../errors/field-paths.ts";
import { formMessages } from "../errors/form-messages.ts";
import { formFailed, formInvalid, type FailedFormState, type InvalidFormState } from "../state/form-state.ts";

export type FormSubmission<TValues> = { readonly values: TValues; readonly idempotencyKey?: string };

export type SubmissionAccepted<TData> = { readonly ok: true; readonly data: TData };

export type IdempotentSubmissionAccepted<TData> = SubmissionAccepted<TData> & {
  readonly idempotencyKey: string;
};

export type SubmissionRefused = { readonly ok: false; readonly state: InvalidFormState | FailedFormState };

const envelope = z.object({ values: z.record(z.string(), z.unknown()) });

const idempotentEnvelope = envelope.extend({ idempotencyKey: z.uuid() });

export function parseSubmission<TSchema extends z.ZodType>(
  schema: TSchema,
  submission: unknown,
  options: { readonly idempotent: true },
): Promise<IdempotentSubmissionAccepted<z.output<TSchema>> | SubmissionRefused>;
export function parseSubmission<TSchema extends z.ZodType>(
  schema: TSchema,
  submission: unknown,
  options?: { readonly idempotent?: false },
): Promise<SubmissionAccepted<z.output<TSchema>> | SubmissionRefused>;
export async function parseSubmission<TSchema extends z.ZodType>(
  schema: TSchema,
  submission: unknown,
  { idempotent = false }: { readonly idempotent?: boolean } = {},
): Promise<
  SubmissionAccepted<z.output<TSchema>> | IdempotentSubmissionAccepted<z.output<TSchema>> | SubmissionRefused
> {
  const received = (idempotent ? idempotentEnvelope : envelope).safeParse(submission);
  if (!received.success) return { ok: false, state: formFailed(formMessages.notSent) };

  const parsed = await schema.safeParseAsync(received.data.values);
  if (!parsed.success) return { ok: false, state: formInvalid(issuesToFieldErrors(parsed.error.issues)) };

  return "idempotencyKey" in received.data && typeof received.data.idempotencyKey === "string"
    ? { ok: true, data: parsed.data, idempotencyKey: received.data.idempotencyKey }
    : { ok: true, data: parsed.data };
}
