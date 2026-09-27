import type { FormAlertMessage, FormAlertProps } from "@dewiride/erp-ui/components/forms/form-alert";
import type { FieldError } from "react-hook-form";

import { formMessages } from "../errors/form-messages.ts";
import type { FormState } from "../state/form-state.ts";

export type FormAlertContent = Readonly<Pick<FormAlertProps, "title" | "messages" | "reference">>;

export type FieldLookup = {
  readonly standing: (path: string) => boolean;
  readonly fieldId: (path: string) => string;
};

export function formAlertContent(
  state: FormState,
  { standing, fieldId }: FieldLookup,
): FormAlertContent | undefined {
  switch (state.status) {
    case "invalid": {
      const messages: FormAlertMessage[] = [
        ...state.formErrors.map((message) => ({ message })),
        ...Object.entries(state.fieldErrors)
          .filter(([path]) => standing(path))
          .flatMap(([path, fieldMessages]) =>
            fieldMessages.map((message) => ({ message, fieldId: fieldId(path) })),
          ),
      ];
      return messages.length === 0 ? undefined : { title: formMessages.detailsNeedAttention, messages };
    }
    case "failed":
      return state.reference === undefined
        ? { title: state.message, messages: [] }
        : { title: state.message, messages: [], reference: state.reference };
    case "idle":
    case "succeeded":
      return undefined;
  }
}

export function formErrorAlertContent(message: string | undefined): FormAlertContent | undefined {
  return message === undefined || message === ""
    ? undefined
    : { title: formMessages.detailsNeedAttention, messages: [{ message }] };
}

export function hasFieldErrors(errors: object): boolean {
  return Object.keys(errors).some((name) => name !== "root");
}

export function serverErrorMessage(state: FormState): string | undefined {
  switch (state.status) {
    case "invalid":
      return formMessages.detailsNeedAttention;
    case "failed":
      return state.message;
    case "idle":
    case "succeeded":
      return undefined;
  }
}

export function formFieldId(formId: string, path: string): string {
  return `${formId}-${path}`.replace(/[^A-Za-z0-9_-]/g, "-");
}

export function fieldErrorMessages(error: FieldError | undefined): readonly string[] | undefined {
  const message = error?.message ?? error?.root?.message;
  return message === undefined || message === "" ? undefined : [message];
}
