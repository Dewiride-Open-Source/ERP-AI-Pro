"use client";

import { CircleAlertIcon, RotateCcwIcon } from "lucide-react";
import { useEffect, useRef, useTransition, type ReactNode } from "react";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Spinner } from "@dewiride/erp-ui/components/ui/spinner";
import { cn } from "@dewiride/erp-ui/lib/utils";

export type ErrorStateProps = {
  title: string;
  description: ReactNode;
  reference?: string | undefined;
  onRetry?: (() => void | Promise<void>) | undefined;
  retryLabel?: string | undefined;
  headingLevel?: 1 | 2 | undefined;
  focusOnMount?: boolean | undefined;
  className?: string | undefined;
};

export function ErrorState({
  title,
  description,
  reference,
  onRetry,
  retryLabel = "Try again",
  headingLevel = 2,
  focusOnMount = false,
  className,
}: ErrorStateProps) {
  const heading = useRef<HTMLHeadingElement>(null);
  const [retrying, startRetry] = useTransition();

  useEffect(() => {
    if (focusOnMount) heading.current?.focus();
  }, [focusOnMount]);

  const retry = () => {
    if (retrying || onRetry === undefined) return;
    startRetry(onRetry);
  };

  return (
    <div
      data-slot="error-state"
      className={cn("flex flex-col items-center gap-4 px-gutter py-12 text-center", className)}
    >
      <span className="flex size-10 items-center justify-center rounded-full bg-destructive/10 text-destructive">
        <CircleAlertIcon aria-hidden="true" className="size-5" />
      </span>
      <div className="grid max-w-md gap-2">
        {headingLevel === 1 ? (
          <h1 ref={heading} tabIndex={-1} className="rounded-sm text-title outline-none">
            {title}
          </h1>
        ) : (
          <h2 ref={heading} tabIndex={-1} className="rounded-sm text-heading outline-none">
            {title}
          </h2>
        )}
        <div className="text-body text-muted-foreground">{description}</div>
        {reference ? (
          <p className="text-caption text-muted-foreground">
            Reference: <span className="font-mono break-all">{reference}</span>
          </p>
        ) : null}
      </div>
      {onRetry ? (
        <Button
          type="button"
          onClick={retry}
          aria-disabled={retrying || undefined}
          aria-busy={retrying || undefined}
          className="aria-disabled:opacity-50"
        >
          {retrying ? (
            <Spinner data-icon="inline-start" aria-hidden="true" />
          ) : (
            <RotateCcwIcon data-icon="inline-start" aria-hidden="true" />
          )}
          {retryLabel}
        </Button>
      ) : null}
    </div>
  );
}
