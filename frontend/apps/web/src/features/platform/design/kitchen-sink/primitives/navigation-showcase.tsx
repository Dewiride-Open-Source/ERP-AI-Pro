"use client";

import {
  Breadcrumb,
  BreadcrumbEllipsis,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@dewiride/erp-ui/components/ui/breadcrumb";
import {
  NavigationMenu,
  NavigationMenuContent,
  NavigationMenuItem,
  NavigationMenuLink,
  NavigationMenuList,
  NavigationMenuTrigger,
  navigationMenuTriggerStyle,
} from "@dewiride/erp-ui/components/ui/navigation-menu";
import {
  Pagination,
  PaginationContent,
  PaginationEllipsis,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@dewiride/erp-ui/components/ui/pagination";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@dewiride/erp-ui/components/ui/tabs";
import { useState, type MouseEvent } from "react";

import { Specimen, SpecimenGrid, SpecimenRow } from "../components/specimen";

const lastPage = 5;

const menuGroups = [
  {
    title: "Tokens",
    links: [
      { href: "#colours", title: "Colours", description: "Every colour token in both themes." },
      { href: "#typography", title: "Typography", description: "Families, weights and type roles." },
      { href: "#motion", title: "Motion", description: "Durations, easings and reduced motion." },
    ],
  },
  {
    title: "Primitives",
    links: [
      { href: "#actions", title: "Actions", description: "Buttons, toggles and shortcuts." },
      { href: "#inputs", title: "Inputs", description: "Text entry, selects and fields." },
      { href: "#overlays", title: "Overlays", description: "Dialogs, sheets and popovers." },
    ],
  },
] as const;

export function NavigationShowcase() {
  return (
    <SpecimenGrid>
      <Specimen title="Tabs" description="The default and line styles; one tab is disabled.">
        <SpecimenRow label="default">
          <Tabs defaultValue="overview" className="w-full" data-testid="navigation-tabs-default">
            <TabsList>
              <TabsTrigger value="overview">Overview</TabsTrigger>
              <TabsTrigger value="lines">Lines</TabsTrigger>
              <TabsTrigger value="payments" disabled>
                Payments
              </TabsTrigger>
            </TabsList>
            <TabsContent value="overview">Issued on 26 Sep 2026 to Acme Private Limited.</TabsContent>
            <TabsContent value="lines">Two lines, taxable value ₹1,00,000.00.</TabsContent>
            <TabsContent value="payments">No payments recorded.</TabsContent>
          </Tabs>
        </SpecimenRow>
        <SpecimenRow label="line">
          <Tabs defaultValue="details" className="w-full" data-testid="navigation-tabs-line">
            <TabsList variant="line">
              <TabsTrigger value="details">Details</TabsTrigger>
              <TabsTrigger value="activity">Activity</TabsTrigger>
              <TabsTrigger value="files">Files</TabsTrigger>
            </TabsList>
            <TabsContent value="details">Client since April 2024.</TabsContent>
            <TabsContent value="activity">Invoice INV-2026-00042 was sent today.</TabsContent>
            <TabsContent value="files">Three signed agreements.</TabsContent>
          </Tabs>
        </SpecimenRow>
      </Specimen>

      <Specimen title="Breadcrumb" description="Where the page sits; the middle levels can collapse.">
        <Breadcrumb data-testid="navigation-breadcrumb">
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink href="#colours">Tokens</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbEllipsis />
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbLink href="#actions">Primitives</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbPage>Navigation</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
      </Specimen>

      <Specimen title="Pagination" description="The current page is marked for assistive technology.">
        <PagedList />
      </Specimen>

      <Specimen title="Navigation menu" description="Menus of links that open on hover, click or keyboard.">
        <NavigationMenu data-testid="navigation-menu">
          <NavigationMenuList>
            {menuGroups.map((group) => (
              <NavigationMenuItem key={group.title}>
                <NavigationMenuTrigger>{group.title}</NavigationMenuTrigger>
                <NavigationMenuContent>
                  <ul className="grid gap-1 md:w-64">
                    {group.links.map((link) => (
                      <li key={link.href}>
                        <NavigationMenuLink asChild>
                          <a href={link.href} className="grid gap-0.5">
                            <span className="font-medium">{link.title}</span>
                            <span className="text-caption text-muted-foreground">{link.description}</span>
                          </a>
                        </NavigationMenuLink>
                      </li>
                    ))}
                  </ul>
                </NavigationMenuContent>
              </NavigationMenuItem>
            ))}
            <NavigationMenuItem>
              <NavigationMenuLink asChild className={navigationMenuTriggerStyle()}>
                <a href="#excluded">Excluded</a>
              </NavigationMenuLink>
            </NavigationMenuItem>
          </NavigationMenuList>
        </NavigationMenu>
      </Specimen>
    </SpecimenGrid>
  );
}

function PagedList() {
  const [page, setPage] = useState(1);

  const goTo = (target: number) => (event: MouseEvent<HTMLAnchorElement>) => {
    event.preventDefault();
    if (target >= 1 && target <= lastPage) setPage(target);
  };

  return (
    <div className="grid gap-3">
      <Pagination data-testid="navigation-pagination">
        <PaginationContent>
          <PaginationItem>
            <PaginationPrevious
              href="#navigation"
              aria-disabled={page === 1 || undefined}
              className="aria-disabled:pointer-events-none aria-disabled:opacity-50"
              onClick={goTo(page - 1)}
            />
          </PaginationItem>
          {visiblePages(page).map((entry, index) => (
            <PaginationItem key={entry === "gap" ? `gap-${index}` : entry}>
              {entry === "gap" ? (
                <PaginationEllipsis />
              ) : (
                <PaginationLink href="#navigation" isActive={entry === page} onClick={goTo(entry)}>
                  {entry}
                </PaginationLink>
              )}
            </PaginationItem>
          ))}
          <PaginationItem>
            <PaginationNext
              href="#navigation"
              aria-disabled={page === lastPage || undefined}
              className="aria-disabled:pointer-events-none aria-disabled:opacity-50"
              onClick={goTo(page + 1)}
            />
          </PaginationItem>
        </PaginationContent>
      </Pagination>
      <p
        aria-live="polite"
        data-testid="navigation-pagination-status"
        className="text-center text-caption text-muted-foreground"
      >
        Page {page} of {lastPage}
      </p>
    </div>
  );
}

function visiblePages(current: number): (number | "gap")[] {
  const candidates = [...new Set([1, current - 1, current, current + 1, lastPage])]
    .filter((page) => page >= 1 && page <= lastPage)
    .sort((a, b) => a - b);

  return candidates.flatMap<number | "gap">((page, index) => {
    const previous = candidates[index - 1];
    return previous !== undefined && page - previous > 1 ? ["gap", page] : [page];
  });
}
