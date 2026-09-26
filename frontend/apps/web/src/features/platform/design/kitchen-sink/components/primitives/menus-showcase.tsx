"use client";

import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  ContextMenu,
  ContextMenuCheckboxItem,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuLabel,
  ContextMenuRadioGroup,
  ContextMenuRadioItem,
  ContextMenuSeparator,
  ContextMenuShortcut,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuTrigger,
} from "@dewiride/erp-ui/components/ui/context-menu";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from "@dewiride/erp-ui/components/ui/dropdown-menu";
import {
  Menubar,
  MenubarCheckboxItem,
  MenubarContent,
  MenubarItem,
  MenubarMenu,
  MenubarRadioGroup,
  MenubarRadioItem,
  MenubarSeparator,
  MenubarShortcut,
  MenubarSub,
  MenubarSubContent,
  MenubarSubTrigger,
  MenubarTrigger,
} from "@dewiride/erp-ui/components/ui/menubar";
import { ScrollArea, ScrollBar } from "@dewiride/erp-ui/components/ui/scroll-area";
import { ChevronDownIcon, CopyIcon, DownloadIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";

import { Specimen, SpecimenGrid } from "../specimen";

const contextLabelId = "ks-context-menu-label";

export function MenusShowcase() {
  return (
    <SpecimenGrid>
      <Specimen
        title="Dropdown menu"
        description="Shortcuts, a checkbox item, radio items, a submenu, a destructive item and a disabled item."
      >
        <InvoiceDropdownMenu />
      </Specimen>
      <Specimen
        title="Context menu"
        description="Right-click or long-press the area. With a keyboard on Windows or Linux, focus it and press the context-menu key or Shift+F10."
      >
        <RowContextMenu />
      </Specimen>
      <Specimen
        title="Menu bar"
        description="A row of menus; it scrolls sideways on a narrow screen instead of wrapping."
        wide
      >
        <EditorMenubar />
      </Specimen>
    </SpecimenGrid>
  );
}

function InvoiceDropdownMenu() {
  const [showPaid, setShowPaid] = useState(true);
  const [sortOrder, setSortOrder] = useState("newest");

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button type="button" variant="outline" className="w-fit">
          Invoice actions
          <ChevronDownIcon data-icon="inline-end" aria-hidden />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-48 max-w-(--radix-dropdown-menu-content-available-width) sm:w-56">
        <DropdownMenuLabel>INV-2026-00042</DropdownMenuLabel>
        <DropdownMenuGroup>
          <DropdownMenuItem>
            <PencilIcon aria-hidden />
            Edit
            <DropdownMenuShortcut>Ctrl E</DropdownMenuShortcut>
          </DropdownMenuItem>
          <DropdownMenuItem>
            <CopyIcon aria-hidden />
            Duplicate
            <DropdownMenuShortcut>Ctrl D</DropdownMenuShortcut>
          </DropdownMenuItem>
          <DropdownMenuSub>
            <DropdownMenuSubTrigger>
              <DownloadIcon aria-hidden />
              Download
            </DropdownMenuSubTrigger>
            <DropdownMenuSubContent className="max-w-(--radix-dropdown-menu-content-available-width)">
              <DropdownMenuItem>PDF</DropdownMenuItem>
              <DropdownMenuItem>E-invoice JSON</DropdownMenuItem>
            </DropdownMenuSubContent>
          </DropdownMenuSub>
          <DropdownMenuItem disabled>Cancel e-invoice (after 24 hours)</DropdownMenuItem>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuCheckboxItem checked={showPaid} onCheckedChange={setShowPaid}>
          Show paid invoices
        </DropdownMenuCheckboxItem>
        <DropdownMenuSeparator />
        <DropdownMenuLabel>Sort by</DropdownMenuLabel>
        <DropdownMenuRadioGroup value={sortOrder} onValueChange={setSortOrder}>
          <DropdownMenuRadioItem value="newest">Newest first</DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="due">Due date</DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="amount">Amount</DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive">
          <Trash2Icon aria-hidden />
          Delete draft
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function RowContextMenu() {
  const [pinned, setPinned] = useState(false);
  const [density, setDensity] = useState("comfortable");

  return (
    <ContextMenu>
      <ContextMenuTrigger
        role="group"
        tabIndex={0}
        aria-labelledby={contextLabelId}
        data-testid="menus-context-trigger"
        className="flex h-28 items-center justify-center rounded-lg border border-dashed bg-muted/40 p-4 text-center focus-ring"
      >
        <span id={contextLabelId} className="text-body text-muted-foreground">
          Invoice row with a context menu
        </span>
      </ContextMenuTrigger>
      <ContextMenuContent
        className="w-56 max-w-(--radix-context-menu-content-available-width)"
        data-testid="menus-context"
      >
        <ContextMenuItem>
          Open
          <ContextMenuShortcut>Enter</ContextMenuShortcut>
        </ContextMenuItem>
        <ContextMenuItem>
          Copy link
          <ContextMenuShortcut>Ctrl C</ContextMenuShortcut>
        </ContextMenuItem>
        <ContextMenuSub>
          <ContextMenuSubTrigger>Move to</ContextMenuSubTrigger>
          <ContextMenuSubContent className="max-w-(--radix-context-menu-content-available-width)">
            <ContextMenuItem>Pending approval</ContextMenuItem>
            <ContextMenuItem>Archived</ContextMenuItem>
          </ContextMenuSubContent>
        </ContextMenuSub>
        <ContextMenuItem disabled>Restore (nothing deleted)</ContextMenuItem>
        <ContextMenuSeparator />
        <ContextMenuCheckboxItem checked={pinned} onCheckedChange={setPinned}>
          Pin to top
        </ContextMenuCheckboxItem>
        <ContextMenuSeparator />
        <ContextMenuLabel>Row density</ContextMenuLabel>
        <ContextMenuRadioGroup value={density} onValueChange={setDensity}>
          <ContextMenuRadioItem value="comfortable">Comfortable</ContextMenuRadioItem>
          <ContextMenuRadioItem value="compact">Compact</ContextMenuRadioItem>
        </ContextMenuRadioGroup>
        <ContextMenuSeparator />
        <ContextMenuItem variant="destructive">Remove</ContextMenuItem>
      </ContextMenuContent>
    </ContextMenu>
  );
}

function EditorMenubar() {
  const [showGrid, setShowGrid] = useState(true);
  const [zoom, setZoom] = useState("100");

  return (
    <ScrollArea className="w-full">
      <div className="w-max pb-3">
        <Menubar>
          <MenubarMenu>
            <MenubarTrigger>File</MenubarTrigger>
            <MenubarContent>
              <MenubarItem>
                New invoice
                <MenubarShortcut>Ctrl N</MenubarShortcut>
              </MenubarItem>
              <MenubarSub>
                <MenubarSubTrigger>Export</MenubarSubTrigger>
                <MenubarSubContent>
                  <MenubarItem>PDF</MenubarItem>
                  <MenubarItem>Spreadsheet</MenubarItem>
                </MenubarSubContent>
              </MenubarSub>
              <MenubarSeparator />
              <MenubarItem disabled>Print (no printer)</MenubarItem>
            </MenubarContent>
          </MenubarMenu>
          <MenubarMenu>
            <MenubarTrigger>Edit</MenubarTrigger>
            <MenubarContent>
              <MenubarItem>
                Undo
                <MenubarShortcut>Ctrl Z</MenubarShortcut>
              </MenubarItem>
              <MenubarItem>
                Redo
                <MenubarShortcut>Ctrl Y</MenubarShortcut>
              </MenubarItem>
              <MenubarSeparator />
              <MenubarItem variant="destructive">Clear lines</MenubarItem>
            </MenubarContent>
          </MenubarMenu>
          <MenubarMenu>
            <MenubarTrigger>View</MenubarTrigger>
            <MenubarContent>
              <MenubarCheckboxItem checked={showGrid} onCheckedChange={setShowGrid}>
                Show grid lines
              </MenubarCheckboxItem>
              <MenubarSeparator />
              <MenubarRadioGroup value={zoom} onValueChange={setZoom}>
                <MenubarRadioItem value="75">75 %</MenubarRadioItem>
                <MenubarRadioItem value="100">100 %</MenubarRadioItem>
                <MenubarRadioItem value="125">125 %</MenubarRadioItem>
              </MenubarRadioGroup>
            </MenubarContent>
          </MenubarMenu>
          <MenubarMenu>
            <MenubarTrigger>Taxes</MenubarTrigger>
            <MenubarContent>
              <MenubarItem>Recalculate GST</MenubarItem>
              <MenubarItem>Apply TDS</MenubarItem>
            </MenubarContent>
          </MenubarMenu>
          <MenubarMenu>
            <MenubarTrigger>Help</MenubarTrigger>
            <MenubarContent>
              <MenubarItem>Keyboard shortcuts</MenubarItem>
            </MenubarContent>
          </MenubarMenu>
        </Menubar>
      </div>
      <ScrollBar orientation="horizontal" />
    </ScrollArea>
  );
}
