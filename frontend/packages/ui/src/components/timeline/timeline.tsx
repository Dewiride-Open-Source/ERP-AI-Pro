import { useId, type ComponentProps, type ReactNode } from "react";

import { cn } from "@dewiride/erp-ui/lib/utils";

export type TimelineHeadingLevel = 2 | 3 | 4 | 5 | 6;

export type TimelineProps = Omit<ComponentProps<"ol">, "role" | "aria-label" | "aria-labelledby"> & {
  label: string;
  labelledBy?: string | undefined;
};

export type TimelineGroupProps = Omit<ComponentProps<"li">, "children"> & {
  heading: ReactNode;
  headingLevel?: TimelineHeadingLevel | undefined;
  children: ReactNode;
};

export type TimelineTimeProps = Omit<ComponentProps<"time">, "dateTime"> & {
  dateTime: string;
};

// Tailwind's preflight removes the list markers, and WebKit then drops the list role from a list without them, so each list
// states its role.
export function Timeline({ label, labelledBy, className, ...props }: TimelineProps) {
  return (
    <ol
      {...props}
      role="list"
      aria-label={labelledBy === undefined ? label : undefined}
      aria-labelledby={labelledBy}
      data-slot="timeline"
      className={cn("grid min-w-0 gap-8 text-body", className)}
    />
  );
}

export function TimelineGroup({
  heading,
  headingLevel = 3,
  className,
  children,
  ...props
}: TimelineGroupProps) {
  const headingId = useId();
  const Heading = `h${headingLevel}` as const;

  return (
    <li {...props} data-slot="timeline-group" className={cn("grid min-w-0 gap-3", className)}>
      <Heading id={headingId} data-slot="timeline-group-heading" className="font-medium">
        {heading}
      </Heading>
      <ol role="list" aria-labelledby={headingId} data-slot="timeline-group-items" className="grid min-w-0">
        {children}
      </ol>
    </li>
  );
}

export function TimelineItem({ className, children, ...props }: ComponentProps<"li">) {
  return (
    <li
      {...props}
      data-slot="timeline-item"
      className={cn("group/timeline-item grid min-w-0 grid-cols-[auto_minmax(0,1fr)] gap-x-3", className)}
    >
      <span aria-hidden="true" data-slot="timeline-marker" className="flex w-3 flex-col items-center">
        <span className="mt-0.5 size-3 shrink-0 rounded-full border-2 border-primary bg-background" />
        <span className="mt-1 w-px flex-1 bg-border group-last/timeline-item:hidden" />
      </span>
      <div className="grid min-w-0 content-start gap-1 pb-5 group-last/timeline-item:pb-0">{children}</div>
    </li>
  );
}

export function TimelineTime({ className, ...props }: TimelineTimeProps) {
  return (
    <time
      {...props}
      data-slot="timeline-time"
      className={cn("text-caption text-muted-foreground", className)}
    />
  );
}

export function TimelineContent({ className, ...props }: ComponentProps<"div">) {
  return (
    <div
      {...props}
      data-slot="timeline-content"
      className={cn("grid min-w-0 grid-cols-[minmax(0,1fr)] gap-2", className)}
    />
  );
}
