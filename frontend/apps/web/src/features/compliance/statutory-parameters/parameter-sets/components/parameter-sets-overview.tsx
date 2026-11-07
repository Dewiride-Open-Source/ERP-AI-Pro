import { Empty, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";

import { parameterSetsNavigation } from "../../nav";
import { listParameterSets, type ParameterSetsRead } from "../server/queries";

import { ParameterSetCard } from "./parameter-set-card";
import { parameterSetsDescription, parameterSetsUnavailable } from "./set-text";

export async function ParameterSetsOverview() {
  const { title, area } = parameterSetsNavigation;
  const read = await listParameterSets();

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section" data-testid="parameter-sets-overview">
      <header className="grid gap-2">
        <div>
          <p className="text-eyebrow text-primary uppercase">{area.title}</p>
          <h1 className="text-title">{title}</h1>
        </div>
        <p className="max-w-prose text-body text-muted-foreground">{parameterSetsDescription}</p>
      </header>

      <ParameterSets read={read} />
    </div>
  );
}

function ParameterSets({ read }: { read: ParameterSetsRead }) {
  if (read.status === "failed") {
    return (
      <div
        role="status"
        data-testid="parameter-sets-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">{parameterSetsUnavailable}</p>
      </div>
    );
  }
  if (read.sets.length === 0) {
    return (
      <Empty className="border" data-testid="parameter-sets-empty">
        <EmptyHeader>
          <EmptyTitle>No statutory parameter sets are listed.</EmptyTitle>
        </EmptyHeader>
      </Empty>
    );
  }
  return (
    <ul role="list" aria-label="Parameter sets" className="grid gap-4 xl:grid-cols-2">
      {read.sets.map((set) => (
        <li key={set.key} className="min-w-0">
          <ParameterSetCard set={set} />
        </li>
      ))}
    </ul>
  );
}
