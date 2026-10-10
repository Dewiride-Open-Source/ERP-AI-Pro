"use client";

import { SidebarTrigger, useSidebar } from "@dewiride/erp-ui/components/ui/sidebar";
import { useIsMobile } from "@dewiride/erp-ui/hooks/use-mobile";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";

// The server cannot know whether the drawer or the sidebar is shown, so the state is told only once the page runs.
export function MenuButton() {
  const isMobile = useIsMobile();
  const { open, openMobile } = useSidebar();
  const hydrated = useHydrated();

  return (
    <SidebarTrigger
      aria-label="Navigation"
      aria-expanded={hydrated ? (isMobile ? openMobile : open) : undefined}
      aria-haspopup={hydrated && isMobile ? "dialog" : undefined}
      aria-keyshortcuts="Control+B Meta+B"
      disabled={!hydrated}
      data-hydrating={hydrated ? undefined : ""}
    />
  );
}
