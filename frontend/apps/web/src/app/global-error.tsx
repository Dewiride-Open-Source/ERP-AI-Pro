"use client";

export default function GlobalError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <html lang="en">
      <body className="flex min-h-dvh flex-col items-center justify-center gap-4 text-center font-sans">
        <h1 className="text-2xl font-semibold">The application failed to load</h1>
        <button type="button" onClick={reset} className="rounded-md border px-4 py-2 text-sm">
          Reload
        </button>
      </body>
    </html>
  );
}
