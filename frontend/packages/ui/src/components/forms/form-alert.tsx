"use client";

import { CircleAlertIcon } from "lucide-react";
import type { MouseEvent, Ref } from "react";

import { Alert, AlertDescription, AlertTitle } from "@dewiride/erp-ui/components/ui/alert";
import { cn } from "@dewiride/erp-ui/lib/utils";

export type FormAlertMessage = {
  message: string;
  fieldId?: string | undefined;
};

export type FormAlertProps = {
  title: string;
  messages: readonly FormAlertMessage[];
  reference?: string | undefined;
  className?: string | undefined;
  ref?: Ref<HTMLDivElement> | undefined;
};

function focusField(event: MouseEvent<HTMLAnchorElement>, fieldId: string): void {
  const field = event.currentTarget.ownerDocument.getElementById(fieldId);
  if (!field) return;
  event.preventDefault();
  field.focus();
}

export function FormAlert({ title, messages, reference, className, ref }: FormAlertProps) {
  return (
    <Alert ref={ref} variant="destructive" tabIndex={-1} className={cn("outline-none", className)}>
      <CircleAlertIcon aria-hidden="true" />
      <AlertTitle>{title}</AlertTitle>
      {messages.length > 0 || reference ? (
        <AlertDescription className="grid gap-1">
          {messages.length > 0 ? (
            <ul className="flex list-disc flex-col gap-1 pl-4">
              {messages.map(({ message, fieldId }, index) => (
                <li key={`${index}-${message}`}>
                  {fieldId ? (
                    <a
                      href={`#${fieldId}`}
                      className="rounded-sm focus-ring"
                      onClick={(event) => focusField(event, fieldId)}
                    >
                      {message}
                    </a>
                  ) : (
                    message
                  )}
                </li>
              ))}
            </ul>
          ) : null}
          {reference ? (
            <p>
              Reference: <span className="font-mono break-all">{reference}</span>
            </p>
          ) : null}
        </AlertDescription>
      ) : null}
    </Alert>
  );
}
