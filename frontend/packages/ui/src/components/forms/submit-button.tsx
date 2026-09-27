"use client";

import type { ComponentProps, MouseEvent } from "react";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { cn } from "@dewiride/erp-ui/lib/utils";

export type SubmitButtonProps = Omit<ComponentProps<typeof Button>, "type" | "asChild"> & {
  pending: boolean;
  ready: boolean;
};

export function SubmitButton({
  pending,
  ready,
  disabled = false,
  className,
  onClick,
  children,
  ...buttonProps
}: SubmitButtonProps) {
  const handleClick = (event: MouseEvent<HTMLButtonElement>) => {
    if (pending) {
      event.preventDefault();
      return;
    }
    onClick?.(event);
  };

  return (
    <Button
      {...buttonProps}
      type="submit"
      disabled={disabled || !ready}
      aria-disabled={pending || undefined}
      aria-busy={pending || undefined}
      data-pending={pending || undefined}
      className={cn("aria-disabled:opacity-50", className)}
      onClick={handleClick}
    >
      {pending ? <Spinner data-icon="inline-start" aria-hidden="true" /> : null}
      {children}
    </Button>
  );
}
