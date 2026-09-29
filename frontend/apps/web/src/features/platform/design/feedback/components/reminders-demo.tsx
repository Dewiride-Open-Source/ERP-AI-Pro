"use client";

import { FormField } from "@dewiride/erp-ui/components/forms/form-field";
import { AnimatedList, AnimatedListItem } from "@dewiride/erp-ui/components/motion/animated-list";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Input } from "@dewiride/erp-ui/components/ui/input";
import { PlusIcon, XIcon } from "lucide-react";
import { useRef, useState, type FormEvent } from "react";
import { toast } from "sonner";

type Reminder = { readonly id: string; readonly text: string };

const firstReminders: readonly Reminder[] = [
  { id: "reminder-1", text: "Send the September payslips" },
  { id: "reminder-2", text: "Reconcile the bank statement" },
  { id: "reminder-3", text: "Renew the office lease" },
];

const maxLength = 80;

export function RemindersDemo({ labelledBy }: { labelledBy: string }) {
  const [reminders, setReminders] = useState<readonly Reminder[]>(firstReminders);
  const [draft, setDraft] = useState("");
  const [error, setError] = useState<string>();
  const nextId = useRef(firstReminders.length + 1);
  const input = useRef<HTMLInputElement>(null);

  const add = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const text = draft.trim();
    if (text === "") {
      setError("Enter a reminder.");
      input.current?.focus();
      return;
    }
    setReminders((current) => [...current, { id: `reminder-${nextId.current}`, text }]);
    nextId.current += 1;
    setDraft("");
    setError(undefined);
  };

  const remove = (reminder: Reminder) => {
    setReminders((current) => current.filter((candidate) => candidate.id !== reminder.id));
    toast.success(`Removed “${reminder.text}”.`);
    input.current?.focus();
  };

  return (
    <div className="grid gap-4">
      <form noValidate onSubmit={add} className="flex flex-wrap items-end gap-2" aria-label="Add a reminder">
        <FormField
          label="Reminder"
          errors={error === undefined ? undefined : [error]}
          className="w-full sm:w-80"
        >
          {(control) => (
            <Input
              {...control}
              ref={input}
              value={draft}
              maxLength={maxLength}
              autoComplete="off"
              onChange={(event) => setDraft(event.target.value)}
            />
          )}
        </FormField>
        <Button type="submit">
          <PlusIcon data-icon="inline-start" aria-hidden="true" />
          Add reminder
        </Button>
      </form>
      {reminders.length === 0 ? (
        <p className="text-body text-muted-foreground" data-testid="reminders-empty">
          No reminders left.
        </p>
      ) : null}
      <AnimatedList
        aria-labelledby={labelledBy}
        data-testid="reminders"
        className="rounded-lg border empty:hidden"
      >
        {reminders.map((reminder) => (
          <AnimatedListItem key={reminder.id} data-testid="reminder" className="border-b last:border-b-0">
            <div className="flex items-center justify-between gap-3 px-3 py-2">
              <span className="min-w-0 text-body break-words">{reminder.text}</span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                aria-label={`Remove ${reminder.text}`}
                onClick={() => remove(reminder)}
              >
                <XIcon aria-hidden="true" />
                <span className="max-sm:sr-only">Remove</span>
              </Button>
            </div>
          </AnimatedListItem>
        ))}
      </AnimatedList>
    </div>
  );
}
