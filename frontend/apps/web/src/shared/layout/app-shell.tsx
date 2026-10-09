import { SkipLink } from "@dewiride/erp-ui/components/layout/skip-link";
import { ThemeToggle } from "@dewiride/erp-ui/components/theme/theme-toggle";
import { Separator } from "@dewiride/erp-ui/components/ui/separator";
import { SidebarProvider } from "@dewiride/erp-ui/components/ui/sidebar";
import { HouseIcon } from "lucide-react";
import { cookies } from "next/headers";
import Link from "next/link";
import type { ReactNode } from "react";

import { WordmarkMark } from "@/shared/brand/wordmark";
import { publicEnv } from "@/shared/config/public-env";
import { isFeatureEnabled } from "@/shared/feature-flags/feature-flags";
import { getFeatureFlags } from "@/shared/feature-flags/queries";

import { mainContentId } from "./main-content";
import type { NavigationEntry } from "./navigation-entry";
import { AppBreadcrumbs } from "./navigation/app-breadcrumbs";
import { AppSidebar } from "./navigation/app-sidebar";
import { MenuButton } from "./navigation/menu-button";
import { homePath, homeTitle, type TrailSource } from "./navigation/navigation-trail";
import { sidebarStartsOpen, sidebarStateCookieName } from "./navigation/sidebar-state";
import { PageSearch, type PageSearchItem } from "./search/page-search";

export async function AppShell({
  navigation,
  account,
  children,
}: {
  navigation: readonly NavigationEntry[];
  account: ReactNode;
  children: ReactNode;
}) {
  const [flags, cookieStore] = await Promise.all([getFeatureFlags(), cookies()]);
  const entries = navigation.filter((entry) => isFeatureEnabled(flags, entry.featureFlag));
  const trailSources: TrailSource[] = entries.map((entry) => ({
    basePath: entry.basePath,
    title: entry.title,
    areaTitle: entry.area.title,
  }));
  const searchItems: PageSearchItem[] = [
    { id: "home", label: homeTitle, href: homePath, icon: <HouseIcon aria-hidden /> },
    ...entries.map((entry) => {
      const Icon = entry.icon;
      return {
        id: entry.id,
        label: entry.title,
        description: entry.description,
        group: entry.area.title,
        href: entry.basePath,
        icon: <Icon aria-hidden />,
      };
    }),
  ];

  return (
    <SidebarProvider defaultOpen={sidebarStartsOpen(cookieStore.get(sidebarStateCookieName)?.value)}>
      <SkipLink targetId={mainContentId} />
      <AppSidebar entries={entries} />
      <div className="flex min-w-0 flex-1 flex-col bg-background">
        <header className="sticky top-0 z-(--layer-sticky) flex h-header shrink-0 items-center gap-2 border-b bg-background/80 px-gutter backdrop-blur supports-[backdrop-filter]:bg-background/60">
          <MenuButton />
          <Link href={homePath} aria-label={publicEnv.appName} className="rounded-lg focus-ring md:hidden">
            <WordmarkMark />
          </Link>
          <Separator
            orientation="vertical"
            className="max-md:hidden data-vertical:h-4 data-vertical:self-center"
          />
          <AppBreadcrumbs sources={trailSources} className="min-w-0 max-md:hidden" />
          <div className="ml-auto flex items-center gap-2">
            <PageSearch items={searchItems} />
            <ThemeToggle />
            {account}
          </div>
        </header>
        <main
          id={mainContentId}
          tabIndex={-1}
          className="mx-auto w-full max-w-page flex-1 px-gutter py-8 outline-none"
        >
          {children}
        </main>
        <footer className="mx-auto w-full max-w-page px-gutter py-6 text-xs text-muted-foreground">
          Dewiride · English (India)
        </footer>
      </div>
    </SidebarProvider>
  );
}
