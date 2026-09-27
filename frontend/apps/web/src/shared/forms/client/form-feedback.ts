import type { FormAlertMessage, FormAlertProps } from "@dewiride/erp-ui/components/forms/form-alert";
import type { FieldError } from "react-hook-form";

import { formMessages } from "../errors/form-messages.ts";
import type { FormState } from "../state/form-state.ts";

export type FormAlertContent = Readonly<Pick<FormAlertProps, "title" | "messages" | "reference">>;

export type FieldLookup = {
  readonly rendered: (path: string) => boolean;
  readonly standing: (path: string) => boolean;
  readonly fieldId: (path: string) => string;
};

// A server message belongs to a field only when the form held a control with that field's id when it was sent; a key on an
// object or a collection element (customer, orderItems.1) or on a member no control carries has no field to show it, focus
// or clear it, so it stays in the summary, unlinked, until the next submission.
export function renderedField(formId: string, controlIds: ReadonlySet<string>): (path: string) => boolean {
  return (path) => controlIds.has(formFieldId(formId, path));
}

export function renderedFieldErrors(
  state: FormState,
  rendered: (path: string) => boolean,
): readonly (readonly [path: string, message: string])[] {
  return state.status === "invalid"
    ? Object.entries(state.fieldErrors)
        .filter(([path]) => rendered(path))
        .map(([path, messages]) => [path, messages.join(" ")] as const)
    : [];
}

export function formAlertContent(
  state: FormState,
  { rendered, standing, fieldId }: FieldLookup,
): FormAlertContent | undefined {
  switch (state.status) {
    case "invalid": {
      const messages: FormAlertMessage[] = [
        ...state.formErrors.map((message) => ({ message })),
        ...Object.entries(state.fieldErrors).flatMap(([path, fieldMessages]) => {
          if (!rendered(path)) return fieldMessages.map((message) => ({ message }));
          return standing(path) ? fieldMessages.map((message) => ({ message, fieldId: fieldId(path) })) : [];
        }),
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
