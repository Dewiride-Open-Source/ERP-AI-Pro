"use client";

import { FormAlert } from "@dewiride/erp-ui/components/forms/form-alert";
import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
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
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { Switch } from "@dewiride/erp-ui/components/ui/switch";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { RefreshCwIcon, SlidersHorizontalIcon, XIcon } from "lucide-react";
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
import { Controller, FormProvider, useFormContext } from "react-hook-form";
import { toast } from "sonner";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";
import type { FormState } from "@/shared/forms/state/form-state";

import {
  changeKindSettingSchema,
  changeKindSettingValues,
  kindSettingOutOfDateCodes,
  setUpKindSettingSchema,
  setUpKindSettingValues,
  type ChangeKindSettingValues,
  type SetUpKindSettingValues,
} from "../forms/kind-setting.schema";
import { changeApprovalKindSetting, setUpApprovalKindSetting } from "../server/actions";

import type { ApprovalKindItem } from "./kind-items";
import {
  changeKindLabel,
  needsApprovalDescription,
  needsApprovalLabel,
  settingDialogDescription,
  settingSaved,
  showLatestSettingsLabel,
  workingDaysDescription,
  workingDaysLabel,
} from "./kind-text";

type KindSettingFieldPath = "required" | "workingDaysToDecide";

type KindSettingFieldValues = { required: boolean; workingDaysToDecide: string };

type FormProps = {
  readonly kind: ApprovalKindItem;
  readonly onSaved: (message: string | undefined) => void;
  readonly onReload: () => void;
  readonly onSavingChange: (saving: boolean) => void;
};

// The dialog shows the kind as it was when the dialog opened, so the refresh a save sends never redraws the form while the
// dialog closes; a kind nobody has set up is set up and one with a setting is changed, from the same trigger, so focus returns
// to it after either. While a save is on its way, Escape, a click outside, the close button and Cancel leave the dialog open,
// so the answer reaches the form that sent it.
export function KindSettingAction({ kind }: { kind: ApprovalKindItem }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [shown, setShown] = useState<ApprovalKindItem>(kind);
  const [opening, setOpening] = useState(0);

  const saved = useCallback((message: string | undefined) => {
    setOpen(false);
    toast.success(message ?? settingSaved);
  }, []);
  const reload = useCallback(() => {
    setOpen(false);
    router.refresh();
  }, [router]);

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (next) {
          setShown(kind);
          setOpening((count) => count + 1);
          setOpen(true);
        } else if (!saving) {
          setOpen(false);
        }
      }}
    >
      <DialogTrigger asChild>
        <KindTrigger aria-label={changeKindLabel(kind.title)} data-testid="approval-kind-change">
          <SlidersHorizontalIcon data-icon="inline-start" aria-hidden />
          Change
        </KindTrigger>
      </DialogTrigger>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg"
        data-testid="approval-kind-dialog"
      >
        <DialogHeader className="pr-8">
          <DialogTitle className="wrap-anywhere">{changeKindLabel(shown.title)}</DialogTitle>
          <DialogDescription>{settingDialogDescription}</DialogDescription>
        </DialogHeader>
        {shown.setting === null ? (
          <SetUpKindSettingForm
            key={opening}
            kind={shown}
            onSaved={saved}
            onReload={reload}
            onSavingChange={setSaving}
          />
        ) : (
          <ChangeKindSettingForm
            key={opening}
            kind={shown}
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

function SetUpKindSettingForm({ kind, onSaved, onReload, onSavingChange }: FormProps) {
  const defaultValues = useMemo(() => setUpKindSettingValues(kind), [kind]);
  const action = useCallback<FormAction<SetUpKindSettingValues>>(
    async (previous, submission) => {
      const answer = await setUpApprovalKindSetting(previous, submission);
      if (answer.status === "succeeded") onSaved(answer.message);
      return answer;
    },
    [onSaved],
  );
  const { form, state, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: setUpKindSettingSchema,
    action,
    defaultValues,
    idempotent: true,
  });
  useSavingReport(pending, onSavingChange);

  return (
    <FormProvider {...form}>
      <KindSettingForm
        onSubmit={onSubmit}
        alert={alert ? <FormAlert ref={alertRef} {...alert} /> : null}
        state={state}
        onReload={onReload}
        pending={pending}
        ready={ready}
      >
        <KindSettingFields fieldId={fieldId} />
      </KindSettingForm>
    </FormProvider>
  );
}

function ChangeKindSettingForm({ kind, onSaved, onReload, onSavingChange }: FormProps) {
  const defaultValues = useMemo(() => changeKindSettingValues(kind), [kind]);
  const action = useCallback<FormAction<ChangeKindSettingValues>>(
    async (previous, submission) => {
      const answer = await changeApprovalKindSetting(previous, submission);
      if (answer.status === "succeeded") onSaved(answer.message);
      return answer;
    },
    [onSaved],
  );
  const { form, state, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: changeKindSettingSchema,
    action,
    defaultValues,
  });
  useSavingReport(pending, onSavingChange);

  return (
    <FormProvider {...form}>
      <KindSettingForm
        onSubmit={onSubmit}
        alert={alert ? <FormAlert ref={alertRef} {...alert} /> : null}
        state={state}
        onReload={onReload}
        pending={pending}
        ready={ready}
      >
        <KindSettingFields fieldId={fieldId} />
      </KindSettingForm>
    </FormProvider>
  );
}

function KindSettingForm({
  onSubmit,
  alert,
  state,
  onReload,
  pending,
  ready,
  children,
}: {
  onSubmit: (event: SubmitEvent<HTMLFormElement>) => void;
  alert: ReactNode;
  state: FormState;
  onReload: () => void;
  pending: boolean;
  ready: boolean;
  children: ReactNode;
}) {
  const outOfDate =
    state.status === "failed" && state.code !== undefined && kindSettingOutOfDateCodes.has(state.code);

  return (
    <form
      noValidate
      method="post"
      onSubmit={onSubmit}
      className="grid gap-4"
      data-testid="approval-kind-form"
    >
      {alert}
      {outOfDate ? (
        <Button
          type="button"
          variant="outline"
          className="justify-self-start"
          onClick={onReload}
          data-testid="approval-kind-reload"
        >
          <RefreshCwIcon data-icon="inline-start" aria-hidden />
          {showLatestSettingsLabel}
        </Button>
      ) : null}
      {children}
      <DialogFooter>
        <SavingClose saving={pending} variant="outline">
          Cancel
        </SavingClose>
        <SubmitButton pending={pending} ready={ready} data-testid="approval-kind-save">
          Save the setting
        </SubmitButton>
      </DialogFooter>
    </form>
  );
}

// Both forms hold the choice and the working days under the same names, so the fields read them from the form they sit in.
function KindSettingFields({ fieldId }: { fieldId: (name: KindSettingFieldPath) => string }) {
  const { control, register, formState } = useFormContext<KindSettingFieldValues>();

  return (
    <FieldGroup>
      <Controller
        control={control}
        name="required"
        render={({ field, fieldState }) => (
          <FormField
            id={fieldId("required")}
            label={needsApprovalLabel}
            description={needsApprovalDescription}
            orientation="horizontal"
            errors={fieldErrorMessages(fieldState.error)}
          >
            {(frame) => (
              <Switch
                {...frame}
                ref={field.ref}
                checked={field.value}
                onCheckedChange={field.onChange}
                onBlur={field.onBlur}
                data-testid="approval-kind-required"
              />
            )}
          </FormField>
        )}
      />
      <FormField
        id={fieldId("workingDaysToDecide")}
        label={workingDaysLabel}
        description={workingDaysDescription}
        required
        errors={fieldErrorMessages(formState.errors.workingDaysToDecide)}
      >
        {(frame) => (
          <Input
            {...frame}
            {...register("workingDaysToDecide")}
            inputMode="numeric"
            autoComplete="off"
            className="w-24"
            data-testid="approval-kind-days"
          />
        )}
      </FormField>
    </FieldGroup>
  );
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
function KindTrigger(props: Omit<ComponentProps<typeof Button>, "type" | "disabled">) {
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
