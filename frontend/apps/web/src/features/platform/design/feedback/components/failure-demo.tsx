"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { TriangleAlertIcon } from "lucide-react";
import { useState } from "react";

export function FailureDemo() {
  const [failing, setFailing] = useState(false);
  if (failing) throw new Error("The feedback page failed on purpose.");

  return (
    <Button type="button" variant="outline" onClick={() => setFailing(true)}>
      <TriangleAlertIcon data-icon="inline-start" aria-hidden="true" />
      Simulate a page failure
    </Button>
  );
}
