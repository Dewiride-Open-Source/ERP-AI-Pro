export type FieldErrors = Readonly<Record<string, readonly string[]>>;

export type ValidationErrors = { readonly fieldErrors: FieldErrors; readonly formErrors: readonly string[] };

export type IdleFormState = { readonly status: "idle" };

export type InvalidFormState = { readonly status: "invalid" } & ValidationErrors;

export type FailedFormState = {
  readonly status: "failed";
  readonly message: string;
  readonly code?: string;
  readonly reference?: string;
};

export type SucceededFormState = { readonly status: "succeeded"; readonly message?: string };

export type FormState = IdleFormState | InvalidFormState | FailedFormState | SucceededFormState;

export const idleFormState: IdleFormState = { status: "idle" };

export function formInvalid({
  fieldErrors,
  formErrors = [],
}: {
  readonly fieldErrors: FieldErrors;
  readonly formErrors?: readonly string[];
}): InvalidFormState {
  return { status: "invalid", fieldErrors, formErrors };
}

export function formFailed(
  message: string,
  { code, reference }: { readonly code?: string | undefined; readonly reference?: string | undefined } = {},
): FailedFormState {
  return {
    status: "failed",
    message,
    ...(code === undefined ? {} : { code }),
    ...(reference === undefined ? {} : { reference }),
  };
}

export function formSucceeded(message?: string): SucceededFormState {
  return message === undefined ? { status: "succeeded" } : { status: "succeeded", message };
}
