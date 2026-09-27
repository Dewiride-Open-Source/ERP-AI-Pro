"use client";

import {
  startTransition,
  useActionState,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
  type RefObject,
  type SubmitEvent,
} from "react";
import {
  get,
  useForm,
  type DefaultValues,
  type FieldPath,
  type FieldValues,
  type UseFormReturn,
} from "react-hook-form";
import type { z } from "zod";

import type { FormSubmission } from "../schemas/parse-submission.ts";
import { schemaResolver } from "../schemas/schema-resolver.ts";
import { idleFormState, type FormState } from "../state/form-state.ts";
import { keepsIdempotencyKey } from "../state/idempotency-key.ts";
import { formAlertContent, formFieldId, serverErrorMessage, type FormAlertContent } from "./form-feedback.ts";

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

const serverErrorName = "root.server";

const subscribeToNothing = () => () => {};
const hydrated = () => true;
const notHydrated = () => false;

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
  const [state, dispatch, pending] = useActionState(action, idleFormState);
  const ready = useSyncExternalStore(subscribeToNothing, hydrated, notHydrated);
  const formId = useId();
  const alertRef = useRef<HTMLDivElement>(null);
  const nextIdempotencyKey = useRef<string | undefined>(undefined);
  const [idempotencyKey, setIdempotencyKey] = useState<string | undefined>(undefined);
  const {
    setError,
    formState: { errors },
  } = form;

  useEffect(() => {
    if (!keepsIdempotencyKey(state)) nextIdempotencyKey.current = undefined;
    if (state.status === "invalid") {
      for (const [path, messages] of Object.entries(state.fieldErrors)) {
        setError(path as FieldPath<z.input<TSchema>>, { type: "server", message: messages.join(" ") });
      }
    }
    const message = serverErrorMessage(state);
    if (message !== undefined) setError(serverErrorName, { type: "server", message });
  }, [state, setError]);

  const serverError = errors.root?.server;
  useEffect(() => {
    if (serverError !== undefined) alertRef.current?.focus();
  }, [serverError]);

  const fieldId = (name: string) => formFieldId(formId, name);
  const alert =
    serverError === undefined
      ? undefined
      : formAlertContent(state, {
          standing: (path) =>
            (get(errors, path) as { readonly type?: unknown } | undefined)?.type === "server",
          fieldId,
        });

  const send = () => {
    const values = form.getValues();
    if (!idempotent) {
      startTransition(() => dispatch({ values }));
      return;
    }

    nextIdempotencyKey.current ??= crypto.randomUUID();
    const key = nextIdempotencyKey.current;
    setIdempotencyKey(key);
    startTransition(() => dispatch({ values, idempotencyKey: key }));
  };

  const onSubmit = (event: SubmitEvent<HTMLFormElement>) => {
    if (pending) {
      event.preventDefault();
      return;
    }
    void form.handleSubmit(send)(event);
  };

  return { form, state, pending, ready, onSubmit, alertRef, alert, fieldId, idempotencyKey };
}
