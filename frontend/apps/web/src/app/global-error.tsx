"use client";

import "@dewiride/erp-ui/globals.css";

import { useStoredTheme } from "@dewiride/erp-ui/components/theme/use-stored-theme";
import { cn } from "@dewiride/erp-ui/lib/utils";
import { GeistMono } from "geist/font/mono";
import { GeistSans } from "geist/font/sans";

import { ApplicationFailure } from "@/shared/layout/application-failure";

export default function GlobalError({
  error,
  retry,
}: Readonly<{ error: Error & { digest?: string }; retry: () => void }>) {
  const theme = useStoredTheme();
  return (
    <html
      lang="en"
      className={cn("font-sans antialiased", GeistSans.variable, GeistMono.variable, theme)}
      style={{ colorScheme: theme }}
    >
      <body className="min-h-dvh bg-background text-foreground">
        <ApplicationFailure reference={error.digest} onRetry={retry} />
      </body>
    </html>
  );
}
