import "@dewiride/erp-ui/globals.css";

import { ThemeProvider } from "@dewiride/erp-ui/components/theme/theme-provider";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { GeistMono } from "geist/font/mono";
import { GeistSans } from "geist/font/sans";
import type { Metadata } from "next";
import { headers } from "next/headers";
import type { ReactNode } from "react";

import { publicEnv } from "@/shared/config/public-env";

export const metadata: Metadata = {
  title: {
    default: publicEnv.appName,
    template: `%s · ${publicEnv.appName}`,
  },
  description: "Open-source ERP for Dewiride.",
  robots: { index: false, follow: false },
};

export default async function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  const nonce = (await headers()).get("x-nonce") ?? undefined;

  return (
    <html
      lang="en"
      suppressHydrationWarning
      className={cn("font-sans antialiased", GeistSans.variable, GeistMono.variable)}
    >
      <body className="min-h-dvh">
        <ThemeProvider {...(nonce === undefined ? {} : { nonce })}>{children}</ThemeProvider>
      </body>
    </html>
  );
}
