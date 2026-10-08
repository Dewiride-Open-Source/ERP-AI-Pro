import { SkipLink } from "@dewiride/erp-ui/components/layout/skip-link";
import { ThemeToggle } from "@dewiride/erp-ui/components/theme/theme-toggle";
import Link from "next/link";
import type { ReactNode } from "react";

import { Wordmark } from "@/shared/brand/wordmark";
import { isFeatureEnabled } from "@/shared/feature-flags/feature-flags";
import { getFeatureFlags } from "@/shared/feature-flags/queries";

import { mainContentId } from "./main-content";
import type { NavigationEntry } from "./navigation-entry";

export async function AppShell({
  navigation,
  account,
  children,
}: {
  navigation: readonly NavigationEntry[];
  account: ReactNode;
  children: ReactNode;
}) {
  const flags = await getFeatureFlags();
  const entries = navigation.filter((item) => isFeatureEnabled(flags, item.featureFlag));
  return (
    <div className="flex min-h-dvh flex-col bg-background">
      <SkipLink targetId={mainContentId} />
      <header className="sticky top-0 z-(--layer-sticky) h-header border-b bg-background/80 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="mx-auto flex h-full w-full max-w-page items-center justify-between gap-4 px-gutter">
          <div className="flex items-center gap-6">
            <Link href="/" className="rounded-md focus-ring">
              <Wordmark />
            </Link>
            <nav aria-label="Primary" className="hidden items-center gap-1 sm:flex">
              {entries.map((item) => (
                <Link
                  key={item.id}
                  href={item.basePath}
                  className="rounded-md px-3 py-1.5 text-sm font-medium text-muted-foreground focus-ring transition-colors hover:bg-accent hover:text-foreground"
                >
                  {item.title}
                </Link>
              ))}
            </nav>
          </div>
          <div className="flex items-center gap-2">
            {account}
            <ThemeToggle />
          </div>
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
  );
}
