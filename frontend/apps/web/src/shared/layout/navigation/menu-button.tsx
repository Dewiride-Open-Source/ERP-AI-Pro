"use client";

import { SidebarTrigger, useSidebar } from "@dewiride/erp-ui/components/ui/sidebar";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { useEffect, useLayoutEffect, useRef } from "react";

// The server cannot know whether the drawer or the sidebar is shown, so the state is told only once the page runs. The
// drawer opens from state, not from a dialog trigger, so Radix would return focus to nothing when it closes: focus goes
// back to the element the person was using before it opened, or to this button when that was the page itself or is gone.
// A layout effect records that element before the drawer's own effect moves focus inside it, and a passive effect moves
// focus back, because the drawer releases its focus trap in a passive cleanup, which React runs before any passive setup.
export function MenuButton() {
  const { isMobile, open, openMobile } = useSidebar();
  const hydrated = useHydrated();
  const button = useRef<HTMLButtonElement>(null);
  const focusBeforeDrawer = useRef<Element | null>(null);
  const drawerWasOpen = useRef(false);
  const drawerOpen = isMobile && openMobile;

  useLayoutEffect(() => {
    if (drawerOpen && !drawerWasOpen.current) focusBeforeDrawer.current = document.activeElement;
  }, [drawerOpen]);

  useEffect(() => {
    if (!drawerOpen && drawerWasOpen.current) {
      const previous = focusBeforeDrawer.current;
      focusBeforeDrawer.current = null;
      const target =
        (previous instanceof HTMLElement || previous instanceof SVGElement) &&
        previous !== document.body &&
        previous.isConnected
          ? previous
          : button.current;
      target?.focus({ preventScroll: true });
    }
    drawerWasOpen.current = drawerOpen;
  }, [drawerOpen]);

  return (
    <SidebarTrigger
      ref={button}
      aria-label="Navigation"
      aria-expanded={hydrated ? (isMobile ? openMobile : open) : undefined}
      aria-haspopup={hydrated && isMobile ? "dialog" : undefined}
      aria-keyshortcuts="Control+B Meta+B"
      disabled={!hydrated}
      data-hydrating={hydrated ? undefined : ""}
    />
  );
}
