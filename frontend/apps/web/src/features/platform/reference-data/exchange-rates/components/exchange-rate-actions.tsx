"use client";

import { ConfirmDialog } from "@dewiride/erp-ui/components/feedback/confirm-dialog";
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
import { FieldGroup } from "@dewiride/erp-ui/components/ui/field";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { PencilIcon, RefreshCwIcon, Trash2Icon, XIcon, type LucideIcon } from "lucide-react";
import { unstable_isUnrecognizedActionError, unstable_rethrow, useRouter } from "next/navigation";
import { useCallback, useLayoutEffect, useState, useTransition, type ComponentProps } from "react";
import { Controller } from "react-hook-form";
import { toast } from "sonner";

import { rejectedActionState } from "@/shared/forms/client/rejected-action";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";

import { exchangeRatesNavigation } from "../../nav";
import {
  changedMeanwhileCode,
  correctExchangeRateSchema,
  correctExchangeRateValues,
  type CorrectExchangeRateValues,
} from "../forms/exchange-rate.schema";
import { correctExchangeRate, removeExchangeRate, type ExchangeRateRemoval } from "../server/actions";

import { correctedMessage, exchangeRateName, removal } from "./exchange-rate-display";
import { RateField, SourceField, UnitField } from "./exchange-rate-fields";

export interface CorrectableRate {
  readonly id: string;
  readonly currency: string;
  readonly rateDate: string;
  readonly rate: string;
  readonly unit: number;
  readonly source: string | null;
  readonly version: string;
}

type RemovalRefusal = Extract<ExchangeRateRemoval, { readonly done: false }>;

export function ExchangeRateActions({ rate }: { rate: CorrectableRate }) {
  return (
    <div className="flex flex-wrap items-start gap-2" data-testid="exchange-rate-actions">
      <CorrectExchangeRateDialog rate={rate} />
      <RemoveExchangeRateAction rate={rate} />
    </div>
  );
}

// While the correction saves, Escape, a click outside, the close button and Cancel leave the dialog open, so the answer always
// reaches the form that sent it; the form is keyed by the rate's version, so the dialog opens on the values the page shows.
function CorrectExchangeRateDialog({ rate }: { rate: CorrectableRate }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const { currency, rateDate } = rate;
  const saved = useCallback(() => {
    setOpen(false);
    toast.success(correctedMessage(currency, rateDate));
  }, [currency, rateDate]);
  const reload = useCallback(() => {
    setOpen(false);
    router.refresh();
  }, [router]);

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (next || !saving) setOpen(next);
      }}
    >
      <DialogTrigger asChild>
        <ActionButton pending={false} icon={PencilIcon} data-testid="exchange-rate-correct">
          Correct
        </ActionButton>
      </DialogTrigger>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg"
      >
        <DialogHeader className="pr-8">
          <DialogTitle className="wrap-anywhere">
            Correct the rate of {exchangeRateName(currency, rateDate)}
          </DialogTitle>
          <DialogDescription>
            Change the rate, the units it is for or its source. The currency and the day stay; for another
            currency or day, remove this rate and record a new one.
          </DialogDescription>
        </DialogHeader>
        <CorrectExchangeRateForm
          key={rate.version}
          rate={rate}
          onSaved={saved}
          onReload={reload}
          onSavingChange={setSaving}
        />
        <SavingClose saving={saving} variant="ghost" size="icon-sm" className="absolute top-2 right-2">
          <XIcon aria-hidden />
          <span className="sr-only">Close</span>
        </SavingClose>
      </DialogContent>
    </Dialog>
  );
}

function CorrectExchangeRateForm({
  rate,
  onSaved,
  onReload,
  onSavingChange,
}: {
  rate: CorrectableRate;
  onSaved: () => void;
  onReload: () => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const action = useCallback<FormAction<CorrectExchangeRateValues>>(
    async (previous, submission) => {
      const answer = await correctExchangeRate(previous, submission);
      if (answer.status === "succeeded") onSaved();
      return answer;
    },
    [onSaved],
  );
  const { form, state, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema: correctExchangeRateSchema,
    action,
    defaultValues: correctExchangeRateValues(rate),
  });
  const { control, register } = form;
  const changedMeanwhile = state.status === "failed" && state.code === changedMeanwhileCode;

  // The dialog learns that a save has started in the frame the form first shows it, before any key or click can reach the
  // dialog, and that it has ended when the form goes.
  useLayoutEffect(() => {
    onSavingChange(pending);
    return () => onSavingChange(false);
  }, [pending, onSavingChange]);

  return (
    <form
      noValidate
      method="post"
      onSubmit={onSubmit}
      className="grid gap-4"
      data-testid="exchange-rate-correct-form"
    >
      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}
      {changedMeanwhile ? (
        <Button
          type="button"
          variant="outline"
          className="justify-self-start"
          onClick={onReload}
          data-testid="exchange-rate-reload"
        >
          <RefreshCwIcon data-icon="inline-start" aria-hidden />
          Show the latest rate
        </Button>
      ) : null}

      <FieldGroup>
        <div className="grid min-w-0 gap-5 sm:grid-cols-2">
          <Controller
            control={control}
            name="rate"
            render={({ field, fieldState }) => (
              <RateField id={fieldId("rate")} field={field} error={fieldState.error} />
            )}
          />
          <Controller
            control={control}
            name="unit"
            render={({ field, fieldState }) => (
              <UnitField id={fieldId("unit")} field={field} error={fieldState.error} />
            )}
          />
        </div>
        <SourceField
          id={fieldId("source")}
          registration={register("source")}
          error={form.formState.errors.source}
        />
      </FieldGroup>

      <DialogFooter>
        <SavingClose saving={pending} variant="outline">
          Cancel
        </SavingClose>
        <SubmitButton pending={pending} ready={ready} data-testid="exchange-rate-correct-save">
          Save the correction
        </SubmitButton>
      </DialogFooter>
    </form>
  );
}

// A removed rate has no page any more, so the person goes back to the list, and the removed rate's address leaves the
// history rather than answering "not found" on Back; a refusal shows beside the button, which keeps focus.
function RemoveExchangeRateAction({ rate }: { rate: CorrectableRate }) {
  const router = useRouter();
  const [pending, startRemoval] = useTransition();
  const [refusal, setRefusal] = useState<RemovalRefusal>();
  const texts = removal(rate.currency, rate.rateDate);

  const confirm = () => {
    setRefusal(undefined);
    startRemoval(async () => {
      const outcome = await settleRemoval(() => removeExchangeRate(rate.id));
      if (!outcome.done) {
        setRefusal(outcome);
        return;
      }
      if (outcome.alreadyRemoved) toast.info(texts.done, { description: texts.gone });
      else toast.success(texts.done);
      router.replace(exchangeRatesNavigation.basePath);
    });
  };

  return (
    <div className="grid justify-items-start gap-1">
      <ConfirmDialog
        trigger={
          <ActionButton pending={pending} icon={Trash2Icon} data-testid="exchange-rate-remove">
            Remove
          </ActionButton>
        }
        title={texts.title}
        description={texts.description}
        cancelLabel={texts.cancel}
        confirmLabel={texts.confirm}
        tone="destructive"
        onConfirm={confirm}
      />
      {refusal === undefined ? null : (
        <p role="alert" className="text-sm text-destructive" data-testid="exchange-rate-remove-error">
          {refusal.message}
          {refusal.reference === undefined ? null : (
            <>
              {" "}
              Reference: <span className="font-mono break-all">{refusal.reference}</span>
            </>
          )}
        </p>
      )}
    </div>
  );
}

async function settleRemoval(remove: () => Promise<ExchangeRateRemoval>): Promise<ExchangeRateRemoval> {
  try {
    return await remove();
  } catch (error) {
    unstable_rethrow(error);
    const failed = rejectedActionState(error, unstable_isUnrecognizedActionError);
    return { done: false, message: failed.message, reference: failed.reference };
  }
}

// The button opens a dialog, so it stays disabled until the page is interactive, when the dialog can open; while the action
// it confirmed runs, it keeps focus and refuses a second one.
function ActionButton({
  pending,
  icon: Icon,
  className,
  onClick,
  children,
  ...props
}: Omit<ComponentProps<typeof Button>, "type" | "disabled" | "variant" | "size"> & {
  pending: boolean;
  icon: LucideIcon;
}) {
  const hydrated = useHydrated();

  return (
    <Button
      {...props}
      type="button"
      variant="outline"
      size="sm"
      disabled={!hydrated}
      aria-disabled={pending || undefined}
      aria-busy={pending || undefined}
      data-hydrating={hydrated ? undefined : ""}
      className={cn("aria-disabled:opacity-50", className)}
      onClick={(event) => {
        if (pending) {
          event.preventDefault();
          return;
        }
        onClick?.(event);
      }}
    >
      {pending ? (
        <Spinner data-icon="inline-start" aria-hidden="true" />
      ) : (
        <Icon data-icon="inline-start" aria-hidden />
      )}
      {children}
    </Button>
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
