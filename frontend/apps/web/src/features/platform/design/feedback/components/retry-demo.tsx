"use client";

import { ErrorState } from "@dewiride/erp-ui/components/feedback/error-state";
import { CircleCheckIcon } from "lucide-react";
import { useEffect, useRef, useState } from "react";

export function RetryDemo() {
  const [connected, setConnected] = useState(false);
  const message = useRef<HTMLParagraphElement>(null);

  useEffect(() => {
    if (connected) message.current?.focus();
  }, [connected]);

  if (connected) {
    return (
      <p
        ref={message}
        tabIndex={-1}
        data-testid="retry-demo-connected"
        className="flex items-center gap-2 rounded-lg border p-4 text-body focus-ring"
      >
        <CircleCheckIcon aria-hidden="true" className="size-4 text-primary" />
        The bank feed is connected and today&apos;s transactions are in.
      </p>
    );
  }

  return (
    <ErrorState
      title="The bank feed did not load"
      description="The bank did not answer in time. Nothing was changed."
      reference="BANK-FEED-TIMEOUT"
      onRetry={() => setConnected(true)}
      className="rounded-lg border"
    />
  );
}
