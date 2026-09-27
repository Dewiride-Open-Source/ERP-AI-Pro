"use client";

import { ApplicationFailure } from "@/shared/layout/application-failure";

export default function GlobalError({
  reset,
}: Readonly<{ error: Error & { digest?: string }; reset: () => void }>) {
  return (
    <html lang="en">
      <body className="flex min-h-dvh flex-col items-center justify-center gap-4 text-center font-sans">
        <ApplicationFailure onReload={reset} />
      </body>
    </html>
  );
}
