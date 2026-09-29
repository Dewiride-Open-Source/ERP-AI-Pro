"use client";

import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { unstable_isUnrecognizedActionError, unstable_rethrow } from "next/navigation";
import {
  startTransition,
  useActionState,
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type RefObject,
  type SubmitEvent,
} from "react";
import {
  get,
  useForm,
  type DefaultValues,
  type FieldErrors,
  type FieldPath,
  type FieldValues,
  type UseFormReturn,
} from "react-hook-form";
import type { z } from "zod";

import type { FormSubmission } from "../schemas/parse-submission.ts";
import { schemaResolver } from "../schemas/schema-resolver.ts";
import { idleFormState, type FormState } from "../state/form-state.ts";
import { idempotencyKeyToSend, type SentIdempotencyKey } from "../state/idempotency-key.ts";
import {
  formAlertContent,
  formErrorAlertContent,
  formFieldId,
  hasFieldErrors,
  renderedField,
  renderedFieldErrors,
  serverErrorMessage,
  type FormAlertContent,
} from "./form-feedback.ts";
import { rejectedActionState } from "./rejected-action.ts";

export type FormAction<TValues> = (
  state: FormState,
  submission: FormSubmission<TValues>,
) => Promise<FormState>;

export type ActionFormOptions<TSchema extends z.ZodType<unknown, FieldValues>> = {
  readonly schema: TSchema;
  readonly action: FormAction<z.input<TSchema>>;
  readonly defaultValues: DefaultValues<z.input<TSchema>>;
  readonly idempotent?: boolean;
};

export type ActionForm<TValues extends FieldValues, TOutput> = {
  readonly form: UseFormReturn<TValues, unknown, TOutput>;
  readonly state: FormState;
  readonly pending: boolean;
  readonly ready: boolean;
  readonly onSubmit: (event: SubmitEvent<HTMLFormElement>) => void;
  readonly alertRef: RefObject<HTMLDivElement | null>;
  readonly alert: FormAlertContent | undefined;
  readonly fieldId: (name: FieldPath<TValues>) => string;
  readonly idempotencyKey: string | undefined;
};

type Sending<TValues> = {
  readonly submission: FormSubmission<TValues>;
  readonly controlIds: ReadonlySet<string>;
};

type Answer = { readonly state: FormState; readonly controlIds: ReadonlySet<string> };

const noAnswer: Answer = { state: idleFormState, controlIds: new Set() };

const serverErrorName = "root.server";

export function useActionForm<TSchema extends z.ZodType<unknown, FieldValues>>({
  schema,
  action,
  defaultValues,
  idempotent = false,
}: ActionFormOptions<TSchema>): ActionForm<z.input<TSchema>, z.output<TSchema>> {
  const resolver = useMemo(() => schemaResolver(schema), [schema]);
  const form = useForm<z.input<TSchema>, unknown, z.output<TSchema>>({
    resolver,
    defaultValues,
    mode: "onTouched",
    criteriaMode: "firstError",
    shouldFocusError: true,
  });
  const settle = useCallback(
    async (previous: Answer, { submission, controlIds }: Sending<z.input<TSchema>>): Promise<Answer> => {
      try {
        return { state: await action(previous.state, submission), controlIds };
      } catch (error) {
        unstable_rethrow(error);
        return { state: rejectedActionState(error, unstable_isUnrecognizedActionError), controlIds };
      }
    },
    [action],
  );
  const [answer, dispatch, pending] = useActionState(settle, noAnswer);
  const { state } = answer;
  const ready = useHydrated();
  const formId = useId();
  const alertRef = useRef<HTMLDivElement>(null);
  const lastSentKey = useRef<SentIdempotencyKey | undefined>(undefined);
  const alertAwaitsFocus = useRef(false);
  const [idempotencyKey, setIdempotencyKey] = useState<string | undefined>(undefined);
  const {
    setError,
    formState: { errors },
  } = form;

  const rendered = useMemo(() => renderedField(formId, answer.controlIds), [formId, answer.controlIds]);
  useEffect(() => {
    for (const [path, message] of renderedFieldErrors(state, rendered)) {
      setError(path as FieldPath<z.input<TSchema>>, { type: "server", message });
    }
    const message = serverErrorMessage(state);
    if (message !== undefined) setError(serverErrorName, { type: "server", message });
  }, [state, rendered, setError]);

  const serverError = errors.root?.server;
  useEffect(() => {
    if (serverError !== undefined) alertRef.current?.focus();
  }, [serverError]);

  const rootError = errors.root;
  useEffect(() => {
    if (!alertAwaitsFocus.current) return;
    alertAwaitsFocus.current = false;
    alertRef.current?.focus();
  }, [rootError]);

  const fieldId = (name: string) => formFieldId(formId, name);
  const alert =
    serverError === undefined
      ? formErrorAlertContent(rootError?.message)
      : formAlertContent(state, {
          rendered,
          standing: (path) =>
            (get(errors, path) as { readonly type?: unknown } | undefined)?.type === "server",
          fieldId,
        });

  const send = (controlIds: ReadonlySet<string>) => {
    alertAwaitsFocus.current = false;
    const values = form.getValues();
    if (!idempotent) {
      startTransition(() => dispatch({ submission: { values }, controlIds }));
      return;
    }

    const sent = idempotencyKeyToSend(lastSentKey.current, state, () => crypto.randomUUID());
    lastSentKey.current = sent;
    setIdempotencyKey(sent.key);
    startTransition(() => dispatch({ submission: { values, idempotencyKey: sent.key }, controlIds }));
  };

  const refuse = (invalid: FieldErrors<z.input<TSchema>>) => {
    alertAwaitsFocus.current = !hasFieldErrors(invalid);
  };

  const onSubmit = (event: SubmitEvent<HTMLFormElement>) => {
    if (pending) {
      event.preventDefault();
      return;
    }
    const controlIds = formControlIds(event.currentTarget);
    void form.handleSubmit(() => send(controlIds), refuse)(event);
  };

  return { form, state, pending, ready, onSubmit, alertRef, alert, fieldId, idempotencyKey };
}

function formControlIds(form: HTMLFormElement): ReadonlySet<string> {
  return new Set(Array.from(form.elements, (control) => control.id).filter((id) => id !== ""));
}
