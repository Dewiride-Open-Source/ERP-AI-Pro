import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { ShieldCheckIcon, UserPlusIcon, UsersIcon } from "lucide-react";
import Link from "next/link";
import { redirect, unstable_rethrow } from "next/navigation";

import { ApiError } from "@/shared/api/problem-details";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";
import { listApiParameters } from "@/shared/lists/list-api";
import { listHref, readListQuery, withPage, type SearchParameters } from "@/shared/lists/list-query";

import { peopleNavigation } from "../../nav";
import { listDepartmentOptions, listDesignationOptions } from "../../organisation/server/queries";
import { peopleList, withWellFormedIds } from "../lists/people.list";
import { getPeople } from "../server/queries";

import { PeopleList } from "./people-list";
import { personRows } from "./person-rows";

const listHeadingId = "people-list-heading";

export async function PeopleOverview({ searchParameters }: { searchParameters: SearchParameters }) {
  const { basePath } = peopleNavigation;
  const read = readListQuery(searchParameters, peopleList);
  const query = withWellFormedIds(read.query);
  if (!read.canonical || query !== read.query) redirect(listHref(basePath, query, peopleList));

  const session = await requireSession();
  const canRegister = mayShow(session, "identity.users.people.register");

  const [people, departments, designations] = await Promise.allSettled([
    getPeople(listApiParameters(query, peopleList)),
    listDepartmentOptions(),
    listDesignationOptions(),
  ]);
  for (const result of [people, departments, designations]) {
    if (result.status === "rejected") unstable_rethrow(result.reason);
  }
  if (people.status === "fulfilled") {
    const totalPages = people.value.totalPages ?? 0;
    if (query.page > 1 && query.page > totalPages) {
      redirect(listHref(basePath, withPage(query, Math.max(1, totalPages)), peopleList));
    }
  }

  return (
    <div className="grid gap-section">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-eyebrow text-primary uppercase">{peopleNavigation.area.title}</p>
          <h1 className="text-title">People</h1>
        </div>
        <div className="flex flex-wrap gap-2">
          {canRegister ? (
            <Button asChild>
              <Link href="/identity/users/new">
                <UserPlusIcon aria-hidden />
                Register a person
              </Link>
            </Button>
          ) : null}
          <Button asChild variant="outline">
            <Link href="/identity/users/permissions">
              <ShieldCheckIcon aria-hidden />
              Roles and permissions
            </Link>
          </Button>
        </div>
      </header>

      <Card data-testid="people-list-card">
        <CardHeader>
          <CardTitle>
            <h2 id={listHeadingId} className="flex items-center gap-2">
              <UsersIcon className="size-4 text-muted-foreground" aria-hidden />
              All people
            </h2>
          </CardTitle>
          <CardDescription>
            Each person&apos;s department, designation and manager are those of the position they hold today.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {people.status === "fulfilled" ? (
            <PeopleList
              query={query}
              page={{
                page: people.value.page ?? query.page,
                pageSize: people.value.pageSize ?? query.pageSize,
                totalCount: people.value.totalCount ?? 0,
                pageCount: people.value.totalPages ?? 0,
              }}
              people={personRows(people.value.items ?? [])}
              departments={departments.status === "fulfilled" ? departments.value : undefined}
              designations={designations.status === "fulfilled" ? designations.value : undefined}
              labelledBy={listHeadingId}
            />
          ) : (
            <Unavailable reason={describeFailure(people.reason)} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function Unavailable({ reason }: { reason: string }) {
  return (
    <div
      role="status"
      data-testid="people-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">People are not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
    </div>
  );
}
