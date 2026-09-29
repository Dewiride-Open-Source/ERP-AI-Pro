"use client";

import type { ReactNode, Ref } from "react";

import { cn } from "@dewiride/erp-ui/lib/utils";

import { DataTableSelectCheckbox } from "./data-table-selection";

export interface DataTableCardDetail {
  readonly columnId: string;
  readonly header: string;
  readonly content: ReactNode;
}

export interface DataTableCard {
  readonly id: string;
  readonly selected: boolean;
  readonly selectLabel: string | undefined;
  readonly title: ReactNode;
  readonly details: readonly DataTableCardDetail[];
  readonly actions: ReactNode;
}

export function DataTableCards({
  cards,
  label,
  labelledBy,
  pending,
  onSelectedChange,
  interactive,
  listRef,
  className,
}: {
  cards: readonly DataTableCard[];
  label: string;
  labelledBy: string | undefined;
  pending: boolean;
  onSelectedChange: ((id: string, selected: boolean) => void) | undefined;
  interactive: boolean;
  listRef: Ref<HTMLUListElement>;
  className: string;
}) {
  return (
    <ul
      ref={listRef}
      tabIndex={-1}
      aria-label={labelledBy === undefined ? label : undefined}
      aria-labelledby={labelledBy}
      aria-busy={pending || undefined}
      data-testid="data-table-cards"
      className={cn("grid gap-2 outline-none", className)}
    >
      {cards.map((card) => (
        <li
          key={card.id}
          data-testid="data-table-card"
          data-state={card.selected ? "selected" : undefined}
          className="grid gap-3 rounded-lg border bg-card p-3 data-[state=selected]:bg-muted/60"
        >
          <div className="flex items-start gap-3">
            {onSelectedChange !== undefined && card.selectLabel !== undefined ? (
              <div className="pt-0.5">
                <DataTableSelectCheckbox
                  checked={card.selected}
                  label={card.selectLabel}
                  disabled={!interactive}
                  onCheckedChange={(checked) => onSelectedChange(card.id, checked)}
                />
              </div>
            ) : null}
            <div data-testid="data-table-card-title" className="min-w-0 flex-1 font-medium break-words">
              {card.title}
            </div>
          </div>
          {card.details.length > 0 ? (
            <dl className="grid grid-cols-[minmax(0,auto)_minmax(0,1fr)] gap-x-4 gap-y-1.5 text-sm">
              {card.details.map((detail) => (
                <div key={detail.columnId} className="contents">
                  <dt className="text-muted-foreground">{detail.header}</dt>
                  <dd className="min-w-0 break-words">{detail.content}</dd>
                </div>
              ))}
            </dl>
          ) : null}
          {card.actions ? <div className="flex flex-wrap justify-end gap-2">{card.actions}</div> : null}
        </li>
      ))}
    </ul>
  );
}
