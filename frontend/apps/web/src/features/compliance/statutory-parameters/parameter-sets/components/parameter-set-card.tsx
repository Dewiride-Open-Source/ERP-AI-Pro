import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import Link from "next/link";
import type { ReactNode } from "react";

import { parameterSetPath } from "../../parameter-set/components/parameter-set-view";

import { SetBasisBadge } from "./set-basis-badge";
import type { ParameterSetSummary, VersionSummary } from "./set-items";
import { entryCountText, fromText, nothingInForce } from "./set-text";

export function ParameterSetCard({ set }: { set: ParameterSetSummary }) {
  return (
    <Card data-testid="parameter-set-card" className="h-full min-w-0">
      <CardHeader className="gap-2">
        <CardTitle>
          <h2 className="wrap-anywhere">
            <Link
              href={parameterSetPath(set.key)}
              prefetch={false}
              className="rounded-sm text-primary underline-offset-4 focus-ring hover:underline"
            >
              {set.title}
            </Link>
          </h2>
        </CardTitle>
        <div>
          <SetBasisBadge basis={set.basis} />
        </div>
        <CardDescription>
          <p>{set.description}</p>
        </CardDescription>
      </CardHeader>
      <CardContent>
        <dl className="grid gap-3">
          <Detail label="In force today" testId="parameter-set-current">
            {set.current === null ? nothingInForce : <Version version={set.current} />}
          </Detail>
          {set.next === null ? null : (
            <Detail label="Next change" testId="parameter-set-next">
              <Version version={set.next} />
            </Detail>
          )}
          <Detail label="Entries in force today" testId="parameter-set-entry-count">
            {entryCountText(set.entryCount)}
          </Detail>
        </dl>
      </CardContent>
    </Card>
  );
}

function Version({ version }: { version: VersionSummary }) {
  return (
    <>
      {fromText(version.from)}
      {version.instruments.length === 0 ? null : (
        <ul role="list" className="mt-1 grid gap-1 text-sm text-muted-foreground">
          {version.instruments.map((instrument) => (
            <li key={instrument}>{instrument}</li>
          ))}
        </ul>
      )}
    </>
  );
}

function Detail({ label, testId, children }: { label: string; testId: string; children: ReactNode }) {
  return (
    <div className="grid min-w-0 gap-0.5" data-testid={testId}>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="min-w-0 wrap-anywhere">{children}</dd>
    </div>
  );
}
