"use client";

import type { DataTableColumn, DataTablePage } from "@dewiride/erp-ui/components/data-table/data-table";
import { DataTableDateRangeFilter } from "@dewiride/erp-ui/components/data-table/filters/date-range-filter";
import { DataTableOptionsFilter } from "@dewiride/erp-ui/components/data-table/filters/options-filter";
import { DataTableTextFilter } from "@dewiride/erp-ui/components/data-table/filters/text-filter";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@dewiride/erp-ui/components/ui/empty";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";
import Link from "next/link";
import { useMemo } from "react";

import { isDateRange, type ListFilterValue, type ListQuery } from "@/shared/lists/list-query";
import { ListTable } from "@/shared/lists/list-table";

import { peopleNavigation } from "../../nav";
import {
  displayNameMaxLength,
  employeeCodeMaxLength,
  peopleList,
  personStatuses,
  workEmailMaxLength,
} from "../lists/people.list";

import { NeedsNewManagerBadge } from "./needs-new-manager-badge";
import { filterNote, filterOptions, type OrganisationLookup, type PersonRow } from "./person-rows";
import { PersonStatusBadge } from "./person-status-badge";
import {
  departmentNameUnavailable,
  departmentsUnavailable,
  designationNameUnavailable,
  designationsUnavailable,
  lastSignInText,
  notRecorded,
  rolesText,
  statusLabel,
  unknownDepartment,
  unknownDesignation,
} from "./person-text";

const statusOptions = personStatuses.map((status) => ({ value: status, label: statusLabel(status) }));

const departmentLabels = { unknown: unknownDepartment, unavailable: departmentNameUnavailable };

const designationLabels = { unknown: unknownDesignation, unavailable: designationNameUnavailable };

const noChoices: readonly string[] = [];

const personColumns: readonly DataTableColumn<PersonRow>[] = [
  {
    id: "displayName",
    header: "Name",
    cell: (person) => (
      <Link
        href={`/identity/users/${person.id}`}
        prefetch={false}
        className="rounded-sm font-medium break-words whitespace-normal text-primary underline-offset-4 focus-ring hover:underline"
        data-testid="people-name"
      >
        {person.displayName}
      </Link>
    ),
    sort: "text",
    hideable: false,
    role: "title",
    className: "max-w-64",
  },
  {
    id: "workEmail",
    header: "Work email",
    cell: (person) => person.workEmail ?? notRecorded,
    sort: "text",
  },
  {
    id: "employeeCode",
    header: "Employee code",
    cell: (person) => person.employeeCode ?? notRecorded,
    sort: "text",
  },
  {
    id: "department",
    header: "Department",
    cell: (person) => person.departmentName ?? notRecorded,
    sort: "text",
  },
  {
    id: "designation",
    header: "Designation",
    cell: (person) => person.designationName ?? notRecorded,
    sort: "text",
  },
  {
    id: "manager",
    header: "Manager",
    cell: (person) =>
      person.managerDisplayName === undefined ? (
        notRecorded
      ) : (
        <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
          {person.managerDisplayName}
          {person.needsNewManager ? (
            <>
              {" "}
              <NeedsNewManagerBadge />
            </>
          ) : null}
        </span>
      ),
    sort: "text",
  },
  {
    id: "dateOfJoining",
    header: "Joined",
    cell: (person) =>
      person.dateOfJoining === undefined ? notRecorded : formatDisplayDate(person.dateOfJoining),
    sort: "date",
  },
  {
    id: "status",
    header: "Status",
    cell: (person) =>
      person.status === undefined ? notRecorded : <PersonStatusBadge status={person.status} />,
    sort: "text",
  },
  {
    id: "roles",
    header: "Roles in the ERP",
    cell: (person) => rolesText(person.roles),
  },
  {
    id: "lastSignedInAt",
    header: "Last signed in",
    cell: (person) => lastSignInText(person.lastSignedInAt),
    sort: "date",
  },
];

export function PeopleList({
  query,
  page,
  people,
  departments,
  designations,
  labelledBy,
}: {
  query: ListQuery;
  page: DataTablePage;
  people: readonly PersonRow[];
  departments: OrganisationLookup;
  designations: OrganisationLookup;
  labelledBy: string;
}) {
  const { name, email, code, status, department, designation, joined } = query.filters;
  const chosenDepartments = choices(department);
  const chosenDesignations = choices(designation);
  const departmentOptions = useMemo(
    () => filterOptions(departments, chosenDepartments, departmentLabels),
    [departments, chosenDepartments],
  );
  const designationOptions = useMemo(
    () => filterOptions(designations, chosenDesignations, designationLabels),
    [designations, chosenDesignations],
  );
  const notes = [
    filterNote(departments, chosenDepartments, departmentsUnavailable),
    filterNote(designations, chosenDesignations, designationsUnavailable),
  ].filter((note) => note !== undefined);

  return (
    <>
      {notes.length > 0 ? (
        <div
          role="status"
          data-testid="people-filters-unavailable"
          className="mb-4 rounded-lg border bg-muted/40 p-4 text-sm text-muted-foreground"
        >
          {notes.map((note) => (
            <p key={note}>{note}</p>
          ))}
        </div>
      ) : null}
      <ListTable
        basePath={peopleNavigation.basePath}
        definition={peopleList}
        query={query}
        page={page}
        label="People"
        labelledBy={labelledBy}
        noun={{ one: "person", other: "people" }}
        columns={personColumns}
        rows={people}
        getRowId={(person) => person.id}
        noMatches="No people match these filters."
        empty={
          <Empty className="border" data-testid="people-empty">
            <EmptyHeader>
              <EmptyTitle>No people yet.</EmptyTitle>
              <EmptyDescription>
                People appear here once they are registered or sign in for the first time.
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        }
        filters={
          <>
            <DataTableTextFilter
              name="name"
              label="Name"
              defaultValue={text(name)}
              placeholder="Search by name"
              maxLength={displayNameMaxLength}
            />
            <DataTableTextFilter
              name="email"
              label="Work email"
              defaultValue={text(email)}
              placeholder="Search by email"
              maxLength={workEmailMaxLength}
            />
            <DataTableTextFilter
              name="code"
              label="Employee code"
              defaultValue={text(code)}
              placeholder="Search by code"
              maxLength={employeeCodeMaxLength}
            />
            <DataTableOptionsFilter
              name="status"
              label="Status"
              options={statusOptions}
              defaultValues={choices(status)}
            />
            {departmentOptions.length > 0 ? (
              <DataTableOptionsFilter
                name="department"
                label="Department"
                options={departmentOptions}
                defaultValues={chosenDepartments}
              />
            ) : null}
            {designationOptions.length > 0 ? (
              <DataTableOptionsFilter
                name="designation"
                label="Designation"
                options={designationOptions}
                defaultValues={chosenDesignations}
              />
            ) : null}
            <DataTableDateRangeFilter
              name="joined"
              label="Joined between"
              defaultValue={joined !== undefined && isDateRange(joined) ? joined : { from: "", to: "" }}
            />
          </>
        }
      />
    </>
  );
}

function text(value: ListFilterValue | undefined): string {
  return typeof value === "string" ? value : "";
}

function choices(value: ListFilterValue | undefined): readonly string[] {
  return Array.isArray(value) ? value : noChoices;
}
