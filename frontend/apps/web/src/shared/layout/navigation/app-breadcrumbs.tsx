"use client";

import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@dewiride/erp-ui/components/ui/breadcrumb";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Fragment } from "react";

import { breadcrumbTrail, type TrailSource, type TrailStep } from "./navigation-trail";

function Step({ step }: { step: TrailStep }) {
  if (step.kind === "page") return <BreadcrumbPage>{step.label}</BreadcrumbPage>;
  if (step.kind === "area") return <span>{step.label}</span>;
  return (
    <BreadcrumbLink asChild>
      <Link href={step.href} className="rounded-sm focus-ring">
        {step.label}
      </Link>
    </BreadcrumbLink>
  );
}

export function AppBreadcrumbs({
  sources,
  className,
}: {
  sources: readonly TrailSource[];
  className?: string;
}) {
  const trail = breadcrumbTrail(usePathname(), sources);

  return (
    <Breadcrumb aria-label="Breadcrumb" className={className}>
      <BreadcrumbList>
        {trail.map((step, index) => (
          <Fragment key={`${step.kind}-${step.label}`}>
            {index > 0 ? <BreadcrumbSeparator /> : null}
            <BreadcrumbItem>
              <Step step={step} />
            </BreadcrumbItem>
          </Fragment>
        ))}
      </BreadcrumbList>
    </Breadcrumb>
  );
}
