import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { referenceListsNavigation } from "../../nav";
import { listMessage, type ListRead } from "../../reference-lists/components/reference-options";

export function ReferenceListFrame({
  title,
  description,
  listHeading,
  listDescription,
  headingId,
  icon: Icon,
  testId,
  children,
}: {
  title: string;
  description: string;
  listHeading: string;
  listDescription: string;
  headingId: string;
  icon: LucideIcon;
  testId: string;
  children: ReactNode;
}) {
  return (
    <div className="grid min-w-0 gap-section">
      <header className="grid gap-2">
        <div>
          <p className="text-eyebrow text-primary uppercase">{referenceListsNavigation.title}</p>
          <h1 className="text-title">{title}</h1>
        </div>
        <p className="max-w-prose text-body text-muted-foreground">{description}</p>
      </header>

      <Card data-testid={testId}>
        <CardHeader>
          <CardTitle>
            <h2 id={headingId} className="flex items-center gap-2">
              <Icon className="size-4 text-muted-foreground" aria-hidden />
              {listHeading}
            </h2>
          </CardTitle>
          <CardDescription>{listDescription}</CardDescription>
        </CardHeader>
        <CardContent>{children}</CardContent>
      </Card>
    </div>
  );
}

export function ReferenceListUnavailable({
  list,
  listName,
}: {
  list: Exclude<ListRead<unknown>, { readonly status: "read" }>;
  listName: string;
}) {
  return (
    <div
      role="status"
      data-testid="reference-list-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">{listMessage(list, listName, "")}</p>
    </div>
  );
}
