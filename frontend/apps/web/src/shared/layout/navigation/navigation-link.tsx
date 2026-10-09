"use client";

import { SidebarMenuButton, SidebarMenuItem, useSidebar } from "@dewiride/erp-ui/components/ui/sidebar";
import type { Route } from "next";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

import { currentPage } from "./navigation-trail";

export function NavigationLink({ href, title, icon }: { href: Route; title: string; icon: ReactNode }) {
  const pathname = usePathname();
  const { isMobile, setOpenMobile } = useSidebar();
  const current = currentPage(pathname, href);

  return (
    <SidebarMenuItem>
      <SidebarMenuButton asChild isActive={current !== undefined} tooltip={title}>
        <Link
          href={href}
          aria-current={current === "page" ? "page" : current === "section" ? "true" : undefined}
          onClick={() => {
            if (isMobile) setOpenMobile(false);
          }}
        >
          {icon}
          <span>{title}</span>
        </Link>
      </SidebarMenuButton>
    </SidebarMenuItem>
  );
}
