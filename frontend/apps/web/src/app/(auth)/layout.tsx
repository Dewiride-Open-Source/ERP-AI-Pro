import type { ReactNode } from "react";

export default function AuthLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <main className="relative isolate flex min-h-dvh items-center justify-center overflow-hidden bg-background px-4 py-12">
      <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
        <div className="absolute -top-32 -left-24 size-96 animate-glow rounded-full bg-primary/25 blur-3xl" />
        <div className="absolute -right-32 -bottom-40 size-96 animate-glow rounded-full bg-chart-2/20 blur-3xl glow-offset" />
      </div>
      {children}
    </main>
  );
}
