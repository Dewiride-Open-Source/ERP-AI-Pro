import { buttonVariants } from "@dewiride/erp-ui/components/ui/button";

import { kitchenSinkGroups, kitchenSinkSections } from "./sections";

export function KitchenSinkIndex() {
  return (
    <nav
      aria-label="Design system sections"
      data-testid="kitchen-sink-index"
      className="grid gap-4 rounded-xl border bg-card p-4"
    >
      {kitchenSinkGroups.map((group) => {
        const labelId = `kitchen-sink-index-${group.id}`;
        return (
          <div key={group.id} className="grid gap-2">
            <p id={labelId} className="text-eyebrow text-muted-foreground uppercase">
              {group.title}
            </p>
            <ul aria-labelledby={labelId} className="flex flex-wrap gap-2">
              {kitchenSinkSections
                .filter((section) => section.group === group.id)
                .map((section) => (
                  <li key={section.id}>
                    <a href={`#${section.id}`} className={buttonVariants({ variant: "outline", size: "sm" })}>
                      {section.title}
                    </a>
                  </li>
                ))}
            </ul>
          </div>
        );
      })}
    </nav>
  );
}
