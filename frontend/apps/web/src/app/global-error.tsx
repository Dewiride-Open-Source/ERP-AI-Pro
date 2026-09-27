"use client";

import "@dewiride/erp-ui/globals.css";

import { cn } from "@dewiride/erp-ui/lib/utils";
import { GeistMono } from "geist/font/mono";
import { GeistSans } from "geist/font/sans";

import { ApplicationFailure } from "@/shared/layout/application-failure";

export default function GlobalError({
  retry,
}: Readonly<{ error: Error & { digest?: string }; retry: () => void }>) {
  return (
    <html lang="en" className={cn("font-sans antialiased", GeistSans.variable, GeistMono.variable)}>
      <body className="flex min-h-dvh flex-col items-center justify-center gap-4 text-center">
        <ApplicationFailure onReload={retry} />
      </body>
    </html>
  );
}
