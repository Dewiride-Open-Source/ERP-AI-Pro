import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@dewiride/erp-ui/components/ui/sidebar";
import { HouseIcon } from "lucide-react";
import Link from "next/link";

import { WordmarkMark } from "@/shared/brand/wordmark";
import { publicEnv } from "@/shared/config/public-env";

import type { NavigationEntry } from "../navigation-entry";

import { CloseNavigationButton } from "./close-navigation-button";
import { NavigationLink } from "./navigation-link";
import { homePath, homeTitle, navigationAreas } from "./navigation-trail";

export function AppSidebar({ entries }: { entries: readonly NavigationEntry[] }) {
  return (
    <Sidebar collapsible="icon">
      <SidebarHeader className="flex-row items-center gap-1">
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton asChild size="lg" tooltip={publicEnv.appName}>
              <Link href={homePath}>
                <WordmarkMark />
                <span className="font-semibold tracking-tight">{publicEnv.appName}</span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
        <CloseNavigationButton />
      </SidebarHeader>
      <SidebarContent>
        <nav aria-label="Primary">
          <SidebarGroup>
            <SidebarGroupContent>
              <SidebarMenu>
                <NavigationLink href={homePath} title={homeTitle} icon={<HouseIcon aria-hidden />} />
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
          {navigationAreas(entries).map((area) => {
            const labelId = `navigation-area-${area.id}`;
            return (
              <SidebarGroup key={area.id}>
                <SidebarGroupLabel id={labelId}>{area.title}</SidebarGroupLabel>
                <SidebarGroupContent>
                  <SidebarMenu aria-labelledby={labelId}>
                    {area.entries.map((entry) => {
                      const Icon = entry.icon;
                      return (
                        <NavigationLink
                          key={entry.id}
                          href={entry.basePath}
                          title={entry.title}
                          icon={<Icon aria-hidden />}
                        />
                      );
                    })}
                  </SidebarMenu>
                </SidebarGroupContent>
              </SidebarGroup>
            );
          })}
        </nav>
      </SidebarContent>
    </Sidebar>
  );
}
