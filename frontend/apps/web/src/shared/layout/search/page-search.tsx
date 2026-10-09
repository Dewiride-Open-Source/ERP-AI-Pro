"use client";

import { CommandPalette, type CommandPaletteItem } from "@dewiride/erp-ui/components/command/command-palette";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import { Kbd } from "@dewiride/erp-ui/components/ui/kbd";
import { useHydrated } from "@dewiride/erp-ui/lib/use-hydrated";
import { SearchIcon } from "lucide-react";
import type { Route } from "next";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useSyncExternalStore } from "react";

import { opensPageSearch, pageSearchShortcutLabel } from "./shortcut-keys";

export type PageSearchItem = CommandPaletteItem & { readonly href: Route };

const noChanges = () => () => undefined;

export function PageSearch({ items }: { items: readonly PageSearchItem[] }) {
  const router = useRouter();
  const hydrated = useHydrated();
  const [open, setOpen] = useState(false);
  const button = useRef<HTMLButtonElement>(null);
  const shortcut = useSyncExternalStore(
    noChanges,
    () => pageSearchShortcutLabel(navigator.userAgent),
    () => pageSearchShortcutLabel(""),
  );

  useEffect(() => {
    const toggle = (event: KeyboardEvent) => {
      if (!opensPageSearch(event)) return;
      event.preventDefault();
      setOpen((shown) => !shown);
    };
    window.addEventListener("keydown", toggle);
    return () => window.removeEventListener("keydown", toggle);
  }, []);

  return (
    <>
      <Button
        ref={button}
        variant="outline"
        size="sm"
        aria-keyshortcuts="Control+K Meta+K"
        disabled={!hydrated}
        data-hydrating={hydrated ? undefined : ""}
        onClick={() => setOpen(true)}
        className="text-muted-foreground"
      >
        <SearchIcon aria-hidden />
        <span className="max-md:sr-only">Search</span>
        <Kbd aria-hidden className="max-md:hidden">
          {shortcut}
        </Kbd>
      </Button>
      <CommandPalette
        open={open}
        onOpenChange={setOpen}
        items={items}
        onSelect={(item) => {
          setOpen(false);
          router.push(item.href);
        }}
        title="Go to a page"
        inputLabel="Search pages"
        placeholder="Search pages…"
        listLabel="Pages"
        emptyMessage="No page matches your search."
        countMessage={(count) => (count === 1 ? "1 page" : `${count} pages`)}
        returnFocusTo={button}
      />
    </>
  );
}
