import { ThemeToggle } from "@dewiride/erp-ui/components/theme/theme-toggle";
import Link from "next/link";
import type { ReactNode } from "react";

import { navigation } from "@/features/registry";
import { Wordmark } from "@/shared/brand/wordmark";
import { isFeatureEnabled } from "@/shared/feature-flags/feature-flags";
import { getFeatureFlags } from "@/shared/feature-flags/queries";

export async function AppShell({ children }: { children: ReactNode }) {
  const flags = await getFeatureFlags();
  const entries = navigation.filter((item) => isFeatureEnabled(flags, item.featureFlag));
  return (
    <div className="flex min-h-dvh flex-col bg-background">
      <header className="sticky top-0 z-(--layer-sticky) border-b bg-background/80 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="mx-auto flex h-header w-full max-w-page items-center justify-between gap-4 px-gutter">
          <div className="flex items-center gap-6">
            <Link href="/login" className="rounded-md focus-ring">
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
          <ThemeToggle />
        </div>
      </header>
      <main className="mx-auto w-full max-w-page flex-1 px-gutter py-8">{children}</main>
      <footer className="mx-auto w-full max-w-page px-gutter py-6 text-xs text-muted-foreground">
        Dewiride · English (India)
      </footer>
    </div>
  );
}
