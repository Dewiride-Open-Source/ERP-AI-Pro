import type { PagedResponseOfChangeRecordResponse } from "@dewiride/erp-api-client";
import { DataTableDateRangeFilter } from "@dewiride/erp-ui/components/data-table/filters/date-range-filter";
import { DataTableOptionsFilter } from "@dewiride/erp-ui/components/data-table/filters/options-filter";
import { DataTableTextFilter } from "@dewiride/erp-ui/components/data-table/filters/text-filter";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";

import { ListFeed } from "@/shared/lists/feed/list-feed";
import { isDateRange, type ListFilterValue, type ListQuery } from "@/shared/lists/list-query";

import { auditNavigation } from "../../nav";
import { changeKeys, changeOperations, changesList, uuidLength, uuidPattern } from "../lists/changes.list";

import { operationLabel, recordTypeLabel } from "./change-text";
import { ChangeTimeline } from "./change-timeline";

const idPattern = {
  source: uuidPattern,
  message: "Enter a complete id, such as 0192f3c4-5b6a-7d8e-9f01-23456789abcd.",
};

const recordTypeOptions = changeKeys.map((key) => ({ value: key, label: recordTypeLabel(key) }));

const actionOptions = changeOperations.map((operation) => ({
  value: operation,
  label: operationLabel(operation),
}));

const noChoices: readonly string[] = [];

export function ChangeFeed({
  query,
  changes,
  labelledBy,
}: {
  query: ListQuery;
  changes: PagedResponseOfChangeRecordResponse;
  labelledBy: string;
}) {
  const items = changes.items ?? [];
  const { type, action, changed, actor, record } = query.filters;

  return (
    <ListFeed
      basePath={auditNavigation.basePath}
      definition={changesList}
      query={query}
      page={{
        page: changes.page ?? query.page,
        pageSize: changes.pageSize ?? query.pageSize,
        totalCount: changes.totalCount ?? 0,
        pageCount: changes.totalPages ?? 0,
      }}
      label="Changes"
      noun={{ one: "change", other: "changes" }}
      itemCount={items.length}
      noMatches="No changes match these filters."
      empty={
        <Empty className="border" data-testid="audit-empty">
          <EmptyHeader>
            <EmptyTitle>No changes are recorded yet.</EmptyTitle>
            <EmptyDescription>
              Changes to people, departments, designations, moves and files appear here.
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      }
      filters={
        <>
          <DataTableOptionsFilter
            name="type"
            label="Record type"
            options={recordTypeOptions}
            defaultValues={choices(type)}
          />
          <DataTableOptionsFilter
            name="action"
            label="Action"
            options={actionOptions}
            defaultValues={choices(action)}
          />
          <DataTableDateRangeFilter
            name="changed"
            label="Changed between"
            defaultValue={changed !== undefined && isDateRange(changed) ? changed : { from: "", to: "" }}
          />
          <DataTableTextFilter
            name="actor"
            label="Changed by id"
            defaultValue={typeof actor === "string" ? actor : ""}
            placeholder="Paste an id"
            maxLength={uuidLength}
            pattern={idPattern}
          />
          <DataTableTextFilter
            name="record"
            label="Subject id"
            defaultValue={typeof record === "string" ? record : ""}
            placeholder="Paste an id"
            maxLength={uuidLength}
            pattern={idPattern}
          />
        </>
      }
    >
      <ChangeTimeline changes={items} query={query} labelledBy={labelledBy} />
    </ListFeed>
  );
}

function choices(value: ListFilterValue | undefined): readonly string[] {
  return Array.isArray(value) ? value : noChoices;
}
