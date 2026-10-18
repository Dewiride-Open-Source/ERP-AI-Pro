"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { ChevronRightIcon } from "lucide-react";

// The button keeps one name whatever its state, which aria-expanded reports, and stays disabled until the page is
// interactive, when it can show and hide the list it controls.
export function DisclosureButton({
  expanded,
  controls,
  label,
  onToggle,
  testId,
}: {
  expanded: boolean;
  controls: string;
  label: string;
  onToggle: () => void;
  testId: string;
}) {
  const hydrated = useHydrated();

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      aria-expanded={expanded}
      aria-controls={controls}
      aria-label={label}
      disabled={!hydrated}
      data-hydrating={hydrated ? undefined : ""}
      data-testid={testId}
      onClick={onToggle}
    >
      <ChevronRightIcon aria-hidden className={cn("transition-transform", expanded && "rotate-90")} />
    </Button>
  );
}

export function DisclosureSpacer() {
  return <span aria-hidden className="size-7 shrink-0" />;
}
