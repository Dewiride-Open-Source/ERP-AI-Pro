import type { ReactNode } from "react";

export function Specimen({
  title,
  description,
  wide = false,
  children,
}: {
  title: string;
  description?: string;
  wide?: boolean;
  children: ReactNode;
}) {
  return (
    <div
      className={`grid min-w-0 grid-cols-1 content-start gap-3 rounded-xl border bg-card p-4 ${wide ? "lg:col-span-2" : ""}`}
    >
      <div className="grid gap-1">
        <h3 className="text-body font-medium">{title}</h3>
        {description ? <p className="text-caption text-muted-foreground">{description}</p> : null}
      </div>
      {children}
    </div>
  );
}

export function SpecimenRow({ label, children }: { label?: string; children: ReactNode }) {
  return (
    <div className="grid min-w-0 grid-cols-1 gap-2">
      {label ? <p className="text-caption text-muted-foreground">{label}</p> : null}
      <div className="flex min-w-0 flex-wrap items-center gap-3">{children}</div>
    </div>
  );
}

export function SpecimenGrid({ children }: { children: ReactNode }) {
  return <div className="grid min-w-0 grid-cols-1 gap-4 lg:grid-cols-2">{children}</div>;
}
