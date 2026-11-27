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
import { Textarea } from "@dewiride/erp-ui/components/ui/textarea";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { cn } from "@dewiride/erp-ui/lib/utils";
import {
  ArchiveXIcon,
  CheckIcon,
  CircleXIcon,
  RefreshCwIcon,
  Undo2Icon,
  XIcon,
  type LucideIcon,
} from "lucide-react";
import { useRouter } from "next/navigation";
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ComponentProps,
} from "react";
import { toast } from "sonner";

import { fieldErrorMessages } from "@/shared/forms/client/form-feedback";
import { useActionForm, type FormAction } from "@/shared/forms/client/use-action-form";
import type { FormState } from "@/shared/forms/state/form-state";

import {
  requestActionSchema,
  requestActionValues,
  requestOutOfDateCodes,
  requiresReason,
  type RequestAction,
  type RequestActionValues,
} from "../../forms/request-action.schema";
import {
  approveApprovalRequest,
  rejectApprovalRequest,
  returnApprovalRequest,
  withdrawApprovalRequest,
} from "../../server/actions";

import { actionDescription, actionTexts, inPlaceNotice, showLatestLabel } from "./decision-text";
import type { RequestActionSubject } from "./request-detail";

export interface RequestActionRights {
  readonly decide: boolean;
  readonly withdraw: boolean;
}

type ServerFunction = (previous: FormState, submission: unknown) => Promise<FormState>;

interface ActionLook {
  readonly icon: LucideIcon;
  readonly variant: "default" | "outline" | "destructive";
}

const serverFunctions: Readonly<Record<RequestAction, ServerFunction>> = {
  approve: approveApprovalRequest,
  return: returnApprovalRequest,
  reject: rejectApprovalRequest,
  withdraw: withdrawApprovalRequest,
};

const looks: Readonly<Record<RequestAction, ActionLook>> = {
  approve: { icon: CheckIcon, variant: "default" },
  return: { icon: Undo2Icon, variant: "outline" },
  reject: { icon: CircleXIcon, variant: "destructive" },
  withdraw: { icon: ArchiveXIcon, variant: "outline" },
};

const decisions: readonly RequestAction[] = ["approve", "return", "reject"];

// A decision or a withdrawal, or the latest version of a request someone else decided, can take every button off the
// refreshed page, and the closing dialog would then return focus to a button that is gone; so once the refreshed request
// arrives without buttons, focus moves to the element focusAfterId names. A refreshed request that still offers them keeps
// focus on the button the dialog returns it to.
export function RequestActions({
  subject,
  rights,
  focusAfterId,
}: {
  subject: RequestActionSubject;
  rights: RequestActionRights;
  focusAfterId: string;
}) {
  const settling = useRef(false);
  const actions: readonly RequestAction[] = [
    ...(rights.decide ? decisions : []),
    ...(rights.withdraw ? (["withdraw"] as const) : []),
  ];
  const offered = actions.length > 0;

  useEffect(() => {
    if (!settling.current) return;
    settling.current = false;
    if (!offered) document.getElementById(focusAfterId)?.focus();
  }, [subject.version, offered, focusAfterId]);

  const settle = useCallback(() => {
    settling.current = true;
  }, []);
  if (!offered) return null;

  return (
    <div className="flex flex-wrap items-start gap-2" data-testid="approval-request-actions">
      {actions.map((action) => (
        <RequestActionDialog key={action} action={action} subject={subject} onSettle={settle} />
      ))}
    </div>
  );
}

// The dialog shows the request as it was when the dialog opened, so the refresh a decision sends never redraws the form
// while the dialog closes. While a decision is on its way, Escape, a click outside, the close button and Cancel leave the
// dialog open, so the answer reaches the form that sent it.
function RequestActionDialog({
  action,
  subject,
  onSettle,
}: {
  action: RequestAction;
  subject: RequestActionSubject;
  onSettle: () => void;
}) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [shown, setShown] = useState<RequestActionSubject>(subject);
  const [opening, setOpening] = useState(0);
  const text = actionTexts[action];
  const { icon: Icon, variant } = looks[action];

  const saved = useCallback(
    (message: string | undefined) => {
      onSettle();
      setOpen(false);
      if (message !== undefined) toast.success(message);
    },
    [onSettle],
  );
  const reload = useCallback(() => {
    onSettle();
    setOpen(false);
    router.refresh();
  }, [onSettle, router]);

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (next) {
          setShown(subject);
          setOpening((count) => count + 1);
          setOpen(true);
        } else if (!saving) {
          setOpen(false);
        }
      }}
    >
      <DialogTrigger asChild>
        <ActionTrigger variant={variant} data-testid={`approval-${action}`}>
          <Icon data-icon="inline-start" aria-hidden />
          {text.button}
        </ActionTrigger>
      </DialogTrigger>
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg"
        data-testid="approval-decision-dialog"
        data-action={action}
      >
        <DialogHeader className="pr-8">
          <DialogTitle>{text.title}</DialogTitle>
          <DialogDescription>{actionDescription(action, shown)}</DialogDescription>
        </DialogHeader>
        {action !== "withdraw" && shown.inPlaceOf !== undefined ? (
          <p
            className="rounded-lg border border-info/40 bg-info/10 p-3 text-sm text-info dark:bg-info/20"
            data-testid="approval-decision-in-place"
          >
            {inPlaceNotice(shown.inPlaceOf)}
          </p>
        ) : null}
        <RequestActionForm
          key={opening}
          action={action}
          subject={shown}
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

function RequestActionForm({
  action,
  subject,
  onSaved,
  onReload,
  onSavingChange,
}: {
  action: RequestAction;
  subject: RequestActionSubject;
  onSaved: (message: string | undefined) => void;
  onReload: () => void;
  onSavingChange: (saving: boolean) => void;
}) {
  const { id, version } = subject;
  const schema = useMemo(() => requestActionSchema(action), [action]);
  const defaultValues = useMemo(() => requestActionValues(id, version), [id, version]);
  const formAction = useCallback<FormAction<RequestActionValues>>(
    async (previous, submission) => {
      const answer = await serverFunctions[action](previous, submission);
      if (answer.status === "succeeded") onSaved(answer.message);
      return answer;
    },
    [action, onSaved],
  );
  const { form, state, pending, ready, onSubmit, alertRef, alert, fieldId } = useActionForm({
    schema,
    action: formAction,
    defaultValues,
  });
  useSavingReport(pending, onSavingChange);
  const text = actionTexts[action];
  const outOfDate =
    state.status === "failed" && state.code !== undefined && requestOutOfDateCodes.has(state.code);

  return (
    <form
      noValidate
      method="post"
      onSubmit={onSubmit}
      className="grid gap-4"
      data-testid="approval-decision-form"
    >
      {alert ? <FormAlert ref={alertRef} {...alert} /> : null}
      {outOfDate ? (
        <Button
          type="button"
          variant="outline"
          className="justify-self-start"
          onClick={onReload}
          data-testid="approval-decision-reload"
        >
          <RefreshCwIcon data-icon="inline-start" aria-hidden />
          {showLatestLabel}
        </Button>
      ) : null}
      <FormField
        id={fieldId("comment")}
        label={text.commentLabel}
        description={text.commentDescription}
        required={requiresReason(action)}
        errors={fieldErrorMessages(form.formState.errors.comment)}
      >
        {(frame) => (
          <Textarea
            {...frame}
            {...form.register("comment")}
            rows={4}
            data-testid="approval-decision-comment"
          />
        )}
      </FormField>
      <DialogFooter>
        <SavingClose saving={pending} variant="outline">
          Cancel
        </SavingClose>
        <SubmitButton pending={pending} ready={ready} data-testid="approval-decision-submit">
          {text.button}
        </SubmitButton>
      </DialogFooter>
    </form>
  );
}

// The dialog learns that a decision has started in the frame the form first shows it, before any key or click can reach the
// dialog, and that it has ended when the form goes.
function useSavingReport(pending: boolean, report: (saving: boolean) => void): void {
  useLayoutEffect(() => {
    report(pending);
    return () => report(false);
  }, [pending, report]);
}

// The button opens a dialog, so it stays disabled until the page is interactive, when the dialog can open.
function ActionTrigger(props: Omit<ComponentProps<typeof Button>, "type" | "disabled" | "size">) {
  const hydrated = useHydrated();

  return <Button {...props} type="button" disabled={!hydrated} data-hydrating={hydrated ? undefined : ""} />;
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
