"use client";

import { unstable_rethrow } from "next/navigation";
import {
  startTransition,
  useActionState,
  useCallback,
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
  const settle = useCallback(
    async (previous: FormState, submission: FormSubmission<z.input<TSchema>>): Promise<FormState> => {
      try {
        return await action(previous, submission);
      } catch (error) {
        unstable_rethrow(error);
        return rejectedActionState(error);
      }
    },
    [action],
  );
  const [state, dispatch, pending] = useActionState(settle, idleFormState);
  const ready = useSyncExternalStore(subscribeToNothing, hydrated, notHydrated);
  const formId = useId();
  const alertRef = useRef<HTMLDivElement>(null);
  const lastSentKey = useRef<SentIdempotencyKey | undefined>(undefined);
  const alertAwaitsFocus = useRef(false);
  const [idempotencyKey, setIdempotencyKey] = useState<string | undefined>(undefined);
  const {
    setError,
    formState: { errors },
  } = form;

  useEffect(() => {
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
          standing: (path) =>
            (get(errors, path) as { readonly type?: unknown } | undefined)?.type === "server",
          fieldId,
        });

  const send = () => {
    alertAwaitsFocus.current = false;
    const values = form.getValues();
    if (!idempotent) {
      startTransition(() => dispatch({ values }));
      return;
    }

    const sent = idempotencyKeyToSend(lastSentKey.current, state, () => crypto.randomUUID());
    lastSentKey.current = sent;
    setIdempotencyKey(sent.key);
    startTransition(() => dispatch({ values, idempotencyKey: sent.key }));
  };

  const refuse = (invalid: FieldErrors<z.input<TSchema>>) => {
    alertAwaitsFocus.current = !hasFieldErrors(invalid);
  };

  const onSubmit = (event: SubmitEvent<HTMLFormElement>) => {
    if (pending) {
      event.preventDefault();
      return;
    }
    void form.handleSubmit(send, refuse)(event);
  };

  return { form, state, pending, ready, onSubmit, alertRef, alert, fieldId, idempotencyKey };
}
