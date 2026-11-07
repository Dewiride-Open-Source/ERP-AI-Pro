"use client";

import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { HistoryIcon } from "lucide-react";

import { ListLink, useListNavigation } from "@/shared/lists/list-navigation";

import type { VersionStanding } from "./parameter-set-view";

export interface VersionItem {
  readonly from: string;
  readonly label: string;
  readonly href: string;
  readonly shown: boolean;
  readonly standing: VersionStanding;
  readonly instruments: readonly string[];
  readonly changes: string;
}

export interface VersionListProps {
  readonly headingId: string;
  readonly focusOnFollow: string;
  readonly items: readonly VersionItem[];
}

export function VersionList({ headingId, focusOnFollow, items }: VersionListProps) {
  const { pending } = useListNavigation();

  return (
    <Card data-testid="parameter-set-versions-card">
      <CardHeader>
        <CardTitle>
          <h2 id={headingId} className="flex items-center gap-2">
            <HistoryIcon className="size-4 text-muted-foreground" aria-hidden />
            Versions
          </h2>
        </CardTitle>
        <CardDescription>
          Newest first, each with the notifications that made it. Open a version to see its entries and what
          it changed.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ol
          aria-labelledby={headingId}
          aria-busy={pending || undefined}
          data-testid="parameter-set-versions"
          className="grid gap-3"
        >
          {items.map((item) => (
            <li
              key={item.from}
              data-testid="parameter-set-version"
              data-shown={item.shown || undefined}
              className="grid min-w-0 gap-2 rounded-lg border p-3 data-shown:border-primary data-shown:bg-primary/5"
            >
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                <ListLink
                  href={item.href}
                  disabled={false}
                  aria-label={item.label}
                  aria-current={item.shown ? "page" : undefined}
                  focusOnFollow={focusOnFollow}
                  className="rounded-sm font-medium text-primary underline-offset-4 focus-ring hover:underline aria-[current=page]:text-foreground"
                >
                  {item.label}
                </ListLink>
                <StandingBadge standing={item.standing} />
              </div>
              {item.instruments.length === 0 ? null : (
                <ul role="list" className="grid gap-1 text-sm wrap-anywhere text-muted-foreground">
                  {item.instruments.map((instrument) => (
                    <li key={instrument}>{instrument}</li>
                  ))}
                </ul>
              )}
              <p className="text-sm">{item.changes}</p>
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  );
}

function StandingBadge({ standing }: { standing: VersionStanding }) {
  switch (standing) {
    case "inForceToday":
      return (
        <Badge variant="outline" className="border-transparent bg-success/10 text-success dark:bg-success/20">
          In force today
        </Badge>
      );
    case "upcoming":
      return <Badge variant="outline">Upcoming</Badge>;
    case "past":
      return null;
  }
}
