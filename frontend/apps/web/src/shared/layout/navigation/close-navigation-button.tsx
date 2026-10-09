"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import { useSidebar } from "@dewiride/erp-ui/components/ui/sidebar";
import { XIcon } from "lucide-react";

// The drawer the sidebar becomes on a phone hides the sheet's own close button, so it gets this one beside the wordmark.
export function CloseNavigationButton() {
  const { isMobile, setOpenMobile } = useSidebar();
  if (!isMobile) return null;

  return (
    <Button variant="ghost" size="icon-sm" aria-label="Close navigation" onClick={() => setOpenMobile(false)}>
      <XIcon aria-hidden />
    </Button>
  );
}
