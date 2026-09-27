import type { ComponentProps } from "react";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";

export type SubmitButtonProps = Omit<ComponentProps<typeof Button>, "type" | "asChild"> & {
  pending: boolean;
  ready: boolean;
};

export function SubmitButton({
  pending,
  ready,
  disabled = false,
  children,
  ...buttonProps
}: SubmitButtonProps) {
  return (
    <Button
      {...buttonProps}
      type="submit"
      disabled={disabled || !ready || pending}
      aria-busy={pending || undefined}
      data-pending={pending || undefined}
    >
      {pending ? <Spinner data-icon="inline-start" aria-hidden="true" /> : null}
      {children}
    </Button>
  );
}
