"use client";

import { SidebarMenuButton, SidebarMenuItem } from "@dewiride/erp-ui/components/ui/sidebar";
import type { Route } from "next";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

import { currentPage } from "./navigation-trail";

export function NavigationLink({ href, title, icon }: { href: Route; title: string; icon: ReactNode }) {
  const current = currentPage(usePathname(), href);

  return (
    <SidebarMenuItem>
      <SidebarMenuButton asChild isActive={current !== undefined} tooltip={title}>
        <Link
          href={href}
          aria-current={current === "page" ? "page" : current === "section" ? "true" : undefined}
        >
          {icon}
          <span>{title}</span>
        </Link>
      </SidebarMenuButton>
    </SidebarMenuItem>
  );
}
