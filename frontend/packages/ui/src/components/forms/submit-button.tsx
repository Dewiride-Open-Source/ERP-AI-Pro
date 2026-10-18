"use client";

import type { ComponentProps, MouseEvent } from "react";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
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
  onMouseDown,
  children,
  ...buttonProps
}: SubmitButtonProps) {
  const hydrated = useHydrated();
  const handleClick = (event: MouseEvent<HTMLButtonElement>) => {
    if (pending) {
      event.preventDefault();
      return;
    }
    onClick?.(event);
  };
  const handleMouseDown = (event: MouseEvent<HTMLButtonElement>) => {
    onMouseDown?.(event);
    event.preventDefault();
  };

  return (
    <Button
      {...buttonProps}
      type="submit"
      disabled={disabled || !ready}
      aria-disabled={pending || undefined}
      aria-busy={pending || undefined}
      data-pending={pending || undefined}
      data-hydrating={hydrated ? undefined : ""}
      className={cn("aria-disabled:opacity-50", className)}
      onClick={handleClick}
      onMouseDown={handleMouseDown}
    >
      {pending ? <Spinner data-icon="inline-start" aria-hidden="true" /> : null}
      {children}
    </Button>
  );
}
