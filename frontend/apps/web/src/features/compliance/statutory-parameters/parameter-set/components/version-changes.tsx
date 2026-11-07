import { Fragment, type ReactNode } from "react";

import type { ParameterColumn, ParameterRow, ShownVersion } from "../../parameter-sets/components/set-items";

import { cellText, cellValue, entryTitle, sourceHeader, titleColumn } from "./cell-text";
import { changedCells } from "./changed-cells";

const firstVersionText = "This is the first version of the set.";

interface ShownValue {
  readonly label: string;
  readonly value: string;
  readonly was?: string | undefined;
}

// An added or removed entry shows every value it holds, beside the title that names it; a changed one only what differs.
export function VersionChanges({
  columns,
  shown,
  first,
}: {
  columns: readonly ParameterColumn[];
  shown: ShownVersion;
  first: boolean;
}) {
  if (first) return <p data-testid="parameter-set-first-version">{firstVersionText}</p>;

  return (
    <div className="grid min-w-0 gap-6">
      {shown.added.length === 0 ? null : (
        <ChangeSection title="Added" testId="parameter-set-added">
          {shown.added.map((row) => (
            <ChangedEntry key={row.key} title={entryTitle(columns, row)} values={entryValues(columns, row)} />
          ))}
        </ChangeSection>
      )}
      {shown.changed.length === 0 ? null : (
        <ChangeSection title="Changed" testId="parameter-set-changed">
          {shown.changed.map((change) => (
            <ChangedEntry
              key={change.after.key}
              title={entryTitle(columns, change.after)}
              values={changedCells(columns, change)}
            />
          ))}
        </ChangeSection>
      )}
      {shown.removed.length === 0 ? null : (
        <ChangeSection title="Removed" testId="parameter-set-removed">
          {shown.removed.map((row) => (
            <ChangedEntry key={row.key} title={entryTitle(columns, row)} values={entryValues(columns, row)} />
          ))}
        </ChangeSection>
      )}
    </div>
  );
}

function entryValues(columns: readonly ParameterColumn[], row: ParameterRow): readonly ShownValue[] {
  const title = titleColumn(columns);
  return [
    ...columns
      .filter((column) => column !== title)
      .map((column) => ({ label: column.title, value: cellText(column, cellValue(row, column.name)) })),
    { label: sourceHeader, value: row.source },
  ];
}

function ChangeSection({ title, testId, children }: { title: string; testId: string; children: ReactNode }) {
  return (
    <div className="grid min-w-0 gap-3" data-testid={testId}>
      <h3 className="font-medium">{title}</h3>
      <ul role="list" className="grid gap-3">
        {children}
      </ul>
    </div>
  );
}

function ChangedEntry({ title, values }: { title: string; values: readonly ShownValue[] }) {
  return (
    <li className="grid min-w-0 gap-2 rounded-lg border p-3" data-testid="parameter-set-change">
      <p className="font-medium wrap-anywhere">{title}</p>
      <dl className="grid grid-cols-[minmax(0,auto)_minmax(0,1fr)] gap-x-4 gap-y-1 text-sm">
        {values.map((value, index) => (
          <Fragment key={`${value.label}-${index}`}>
            <dt className="text-muted-foreground">{value.label}</dt>
            <dd className="min-w-0 wrap-anywhere">
              {value.value}
              {value.was === undefined ? null : (
                <>
                  {" "}
                  <span className="text-muted-foreground">(was {value.was})</span>
                </>
              )}
            </dd>
          </Fragment>
        ))}
      </dl>
    </li>
  );
}
