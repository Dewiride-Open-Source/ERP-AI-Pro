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
      <header className="sticky top-0 z-40 border-b bg-background/80 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="mx-auto flex h-14 w-full max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex items-center gap-6">
            <Link
              href="/login"
              className="rounded-md focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
            >
              <Wordmark />
            </Link>
            <nav aria-label="Primary" className="hidden items-center gap-1 sm:flex">
              {entries.map((item) => (
                <Link
                  key={item.id}
                  href={item.basePath}
                  className="rounded-md px-3 py-1.5 text-sm font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                >
                  {item.title}
                </Link>
              ))}
            </nav>
          </div>
          <ThemeToggle />
        </div>
      </header>
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8 sm:px-6">{children}</main>
      <footer className="mx-auto w-full max-w-6xl px-4 py-6 text-xs text-muted-foreground sm:px-6">
        Dewiride · English (India)
      </footer>
    </div>
  );
}
