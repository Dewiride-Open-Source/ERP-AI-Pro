"use client";

import { Button, buttonVariants } from "@dewiride/erp-ui/components/ui/button";
import type { Route } from "next";
import Form from "next/form";
import { useEffect, useRef, type FormEvent, type ReactNode } from "react";

import { listParameters, type ListDefinition } from "./list-definition.ts";
import { ListLink, useListNavigation } from "./list-navigation.tsx";
import { filterSignature, filtersFromForm, listLink, withFilters, type ListQuery } from "./list-query.ts";

function preservedParameters(
  query: ListQuery,
  definition: ListDefinition,
): readonly (readonly [string, string])[] {
  const { defaultSort, defaultPageSize } = definition;
  const preserved: (readonly [string, string])[] = [];
  if (query.sort.field !== defaultSort.field || query.sort.direction !== defaultSort.direction) {
    preserved.push([listParameters.sort, `${query.sort.field}:${query.sort.direction}`]);
  }
  if (query.pageSize !== defaultPageSize) preserved.push([listParameters.pageSize, String(query.pageSize)]);
  if (query.hiddenColumns.length > 0) {
    preserved.push([listParameters.hiddenColumns, query.hiddenColumns.join(",")]);
  }
  return preserved;
}

export function ListFilterForm({
  basePath,
  definition,
  query,
  label,
  focusFirstField,
  onApply,
  onClear,
  children,
}: {
  basePath: Route;
  definition: ListDefinition;
  query: ListQuery;
  label: string;
  focusFirstField: boolean;
  onApply: (signature: string) => void;
  onClear: () => void;
  children: ReactNode;
}) {
  const { navigate } = useListNavigation();
  const formRef = useRef<HTMLFormElement>(null);
  const filtered = Object.keys(query.filters).length > 0;

  useEffect(() => {
    if (focusFirstField)
      formRef.current?.querySelector<HTMLElement>("input:not([type=hidden]), button")?.focus();
  }, [focusFirstField]);

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const form = event.currentTarget;
    if (!form.checkValidity()) {
      form.querySelector<HTMLElement>(":invalid")?.focus();
      return;
    }
    const filters = filtersFromForm(new FormData(form), definition);
    onApply(filterSignature(filters, definition));
    navigate(listLink(basePath, withFilters(query, filters), definition));
  }

  return (
    <Form
      ref={formRef}
      action={basePath}
      role="search"
      aria-label={label}
      noValidate
      onSubmit={submit}
      className="flex flex-wrap items-end gap-3"
    >
      {children}
      {preservedParameters(query, definition).map(([name, value]) => (
        <input key={name} type="hidden" name={name} value={value} />
      ))}
      <div className="flex items-center gap-2">
        <Button type="submit" variant="secondary">
          Apply filters
        </Button>
        {filtered ? (
          <ListLink
            href={listLink(basePath, withFilters(query, {}), definition)}
            disabled={false}
            aria-label="Clear filters"
            className={buttonVariants({ variant: "ghost" })}
            onFollow={onClear}
          >
            Clear filters
          </ListLink>
        ) : null}
      </div>
    </Form>
  );
}
