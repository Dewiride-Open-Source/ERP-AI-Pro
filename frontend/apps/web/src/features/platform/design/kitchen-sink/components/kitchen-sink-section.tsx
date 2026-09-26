import type { ReactNode } from "react";

import type { KitchenSinkSectionId } from "./sections";

export function KitchenSinkSection({
  id,
  title,
  description,
  children,
}: {
  id: KitchenSinkSectionId;
  title: string;
  description: string;
  children: ReactNode;
}) {
  const headingId = `${id}-heading`;

  return (
    <section id={id} aria-labelledby={headingId} className="grid min-w-0 grid-cols-1 gap-4">
      <header className="grid gap-1">
        <h2 id={headingId} className="text-heading">
          {title}
        </h2>
        <p className="max-w-prose text-body text-muted-foreground">{description}</p>
      </header>
      {children}
    </section>
  );
}
