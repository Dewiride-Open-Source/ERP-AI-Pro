"use client";

import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { SubmitButton } from "@dewiride/erp-ui/components/forms/submit-button";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@dewiride/erp-ui/components/ui/dialog";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { PencilIcon, PlusIcon, RefreshCwIcon, XIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import {
  useCallback,
  useLayoutEffect,
  useMemo,
  useState,
  type ComponentProps,
  type ReactNode,
  type SubmitEvent,
} from "react";
import { FormProvider } from "react-hook-form";
import { toast } from "sonner";

import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";
import type { FormState } from "@/shared/forms/state/form-state";

import {
  changeNumberSeriesSchema,
  changeNumberSeriesValues,
  nextNumberFixed,
  pageOutOfDateCodes,
  setUpNumberSeriesSchema,
  setUpNumberSeriesValues,
  type ChangeNumberSeriesValues,
  type SetUpNumberSeriesValues,
} from "../forms/number-series.schema";
import { changeNumberSeries, setUpNumberSeries } from "../server/actions";

import { NumberFormatFields, type FixedNextNumber } from "./number-format-fields";
import { runningNumberOf, type SerialNumberRule } from "./number-format";
import type { NumberedDocument } from "./numbering-items";
import { changedMessage, changeLabel, dialogDescription, setUpLabel, setUpMessage } from "./numbering-text";

type FormProps = {
  readonly document: NumberedDocument;
  readonly rule: SerialNumberRule;
  readonly onSaved: () => void;
  readonly onReload: () => void;
  readonly onSavingChange: (saving: boolean) => void;
};

// The dialog shows the kind of document as it was when the dialog opened, so the refresh a save sends never redraws the form
// while the dialog closes; one trigger serves both Set up and Change, so focus returns to it after either. While a save is on
// its way, Escape, a click outside, the close button and Cancel leave the dialog open, so the answer reaches the form that sent
// it.
export function NumberingAction({ document, rule }: { document: NumberedDocument; rule: SerialNumberRule }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [shown, setShown] = useState<NumberedDocument>(document);
  const [opening, setOpening] = useState(0);
  const settingUp = shown.series === null;
  const { title } = shown;

  const saved = useCallback(() => {
    setOpen(false);
    toast.success(settingUp ? setUpMessage(title) : changedMessage(title));
  }, [settingUp, title]);
  const reload = useCallback(() => {
    setOpen(false);
    router.refresh();
  }, [router]);

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (next) {
          setShown(document);
          setOpening((count) => count + 1);
          setOpen(true);
        } else if (!saving) {
          setOpen(false);
        }
      }}
    >
      <DialogTrigger asChild>
        <NumberingTrigger
          aria-label={document.series === null ? setUpLabel(document.title) : changeLabel(document.title)}
          data-testid="numbering-action"
        >
          {document.series === null ? (
            <PlusIcon data-icon="inline-start" aria-hidden />
          ) : (
            <PencilIcon data-icon="inline-start" aria-hidden />
          )}
          {document.series === null ? "Set up" : "Change"}
        </NumberingTrigger>
      </DialogTrigger>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg"
        data-testid="numbering-dialog"
      >
        <DialogHeader className="pr-8">
          <DialogTitle className="wrap-anywhere">
            {settingUp ? setUpLabel(title) : changeLabel(title)}
          </DialogTitle>
          <DialogDescription>{dialogDescription(shown, nextNumberFixed(shown))}</DialogDescription>
        </DialogHeader>
        {settingUp ? (
          <SetUpNumberSeriesForm
            key={opening}
            document={shown}
            rule={rule}
            onSaved={saved}
            onReload={reload}
            onSavingChange={setSaving}
          />
        ) : (
          <ChangeNumberSeriesForm
            key={opening}
            document={shown}
            rule={rule}
            onSaved={saved}
            onReload={reload}
            onSavingChange={setSaving}
          />
        )}
        <SavingClose saving={saving} variant="ghost" size="icon-sm" className="absolute top-2 right-2">
          <XIcon aria-hidden />
          <span className="sr-only">Close</span>
        </SavingClose>
      </DialogContent>
    </Dialog>
  );
}

function SetUpNumberSeriesForm({ document, rule, onSaved, onReload, onSavingChange }: FormProps) {
  const { currentYear, documentType } = document;
  const schema = useMemo(() => setUpNumberSeriesSchema({ rule, currentYear }), [rule, currentYear]);
  const defaultValues = useMemo(() => setUpNumberSeriesValues(documentType), [documentType]);
  const action = useCallback<FormAction<SetUpNumberSeriesValues>>(
    async (previous, submission) => {
      const answer = await setUpNumberSeries(previous, submission);
      if (answer.status === "succeeded") onSaved();
      return answer;
    },
    [onSaved],
  );
  const { form, state, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema,
    action,
    defaultValues,
    idempotent: true,
  });
  useSavingReport(pending, onSavingChange);

  return (
    <FormProvider {...form}>
      <NumberingForm
        onSubmit={onSubmit}
        alert={alert ? <FormAlert ref={alertRef} {...alert} /> : null}
        state={state}
        onReload={onReload}
        pending={pending}
        ready={ready}
        submitLabel="Set up the numbering"
      >
        <NumberFormatFields
          fieldId={fieldId}
          rule={rule}
          currentYear={currentYear}
          fixedNextNumber={undefined}
        />
      </NumberingForm>
    </FormProvider>
  );
}

function ChangeNumberSeriesForm({ document, rule, onSaved, onReload, onSavingChange }: FormProps) {
  const { currentYear } = document;
  const fixed = nextNumberFixed(document);
  const schema = useMemo(
    () => changeNumberSeriesSchema({ rule, currentYear, nextNumberFixed: fixed }),
    [rule, currentYear, fixed],
  );
  const defaultValues = useMemo(() => changeNumberSeriesValues(document), [document]);
  const action = useCallback<FormAction<ChangeNumberSeriesValues>>(
    async (previous, submission) => {
      const answer = await changeNumberSeries(previous, submission);
      if (answer.status === "succeeded") onSaved();
      return answer;
    },
    [onSaved],
  );
  const { form, state, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema,
    action,
    defaultValues,
  });
  useSavingReport(pending, onSavingChange);

  return (
    <FormProvider {...form}>
      <NumberingForm
        onSubmit={onSubmit}
        alert={alert ? <FormAlert ref={alertRef} {...alert} /> : null}
        state={state}
        onReload={onReload}
        pending={pending}
        ready={ready}
        submitLabel="Save the numbering"
      >
        <NumberFormatFields
          fieldId={fieldId}
          rule={rule}
          currentYear={currentYear}
          fixedNextNumber={fixed ? fixedNextNumberOf(document) : undefined}
        />
      </NumberingForm>
    </FormProvider>
  );
}

function NumberingForm({
  onSubmit,
  alert,
  state,
  onReload,
  pending,
  ready,
  submitLabel,
  children,
}: {
  onSubmit: (event: SubmitEvent<HTMLFormElement>) => void;
  alert: ReactNode;
  state: FormState;
  onReload: () => void;
  pending: boolean;
  ready: boolean;
  submitLabel: string;
  children: ReactNode;
}) {
  const outOfDate =
    state.status === "failed" && state.code !== undefined && pageOutOfDateCodes.has(state.code);

  return (
    <form noValidate method="post" onSubmit={onSubmit} className="grid gap-4" data-testid="numbering-form">
      {alert}
      {outOfDate ? (
        <Button
          type="button"
          variant="outline"
          className="justify-self-start"
          onClick={onReload}
          data-testid="numbering-reload"
        >
          <RefreshCwIcon data-icon="inline-start" aria-hidden />
          Show the latest numbering
        </Button>
      ) : null}
      {children}
      <DialogFooter>
        <SavingClose saving={pending} variant="outline">
          Cancel
        </SavingClose>
        <SubmitButton pending={pending} ready={ready} data-testid="numbering-save">
          {submitLabel}
        </SubmitButton>
      </DialogFooter>
    </form>
  );
}

function fixedNextNumberOf(document: NumberedDocument): FixedNextNumber {
  const current = document.series?.years.find((year) => year.year === document.currentYear);
  return {
    running: current?.nextText ? runningNumberOf(current.nextText, current.format).toString() : "",
    nextText: current?.nextText ?? null,
  };
}

// The dialog learns that a save has started in the frame the form first shows it, before any key or click can reach the
// dialog, and that it has ended when the form goes.
function useSavingReport(pending: boolean, report: (saving: boolean) => void): void {
  useLayoutEffect(() => {
    report(pending);
    return () => report(false);
  }, [pending, report]);
}

// The button opens a dialog, so it stays disabled until the page is interactive, when the dialog can open.
function NumberingTrigger(props: Omit<ComponentProps<typeof Button>, "type" | "disabled">) {
  const hydrated = useHydrated();

  return (
    <Button
      {...props}
      type="button"
      variant="outline"
      size="sm"
      disabled={!hydrated}
      data-hydrating={hydrated ? undefined : ""}
    />
  );
}

function SavingClose({
  saving,
  className,
  children,
  ...props
}: Omit<ComponentProps<typeof Button>, "type" | "asChild"> & { saving: boolean }) {
  return (
    <DialogClose asChild>
      <Button
        {...props}
        type="button"
        aria-disabled={saving || undefined}
        className={cn("aria-disabled:opacity-50", className)}
      >
        {children}
      </Button>
    </DialogClose>
  );
}
