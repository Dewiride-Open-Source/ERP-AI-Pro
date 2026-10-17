import type { PersonResponse } from "@dewiride/erp-api-client";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";
import { IdCardIcon } from "lucide-react";
import Link from "next/link";
import type { ReactNode } from "react";

import { isoDateOf } from "@/shared/api/dates/date-only";
import { formatDateTimeIst } from "@/shared/format/dates";

const notGiven = "Not given";

const noneHeld = "None";

export function PersonSummary({ person }: { person: PersonResponse }) {
  const joined = isoDateOf(person.dateOfJoining);
  const signedIn = person.lastSignedInAt;

  return (
    <Card data-testid="person-details">
      <CardHeader>
        <CardTitle>
          <h2 className="flex items-center gap-2">
            <IdCardIcon className="size-4 text-muted-foreground" aria-hidden />
            Details
          </h2>
        </CardTitle>
        <CardDescription>
          The name and work email come from Microsoft Entra each time the person signs in. The department,
          designation and manager are those of the move that holds today.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <dl className="grid gap-4 sm:grid-cols-2">
          <Detail label="Employee code" value={person.employeeCode ?? notGiven} />
          <Detail label="Phone number" value={person.phoneNumber ?? notGiven} />
          <Detail
            label="Date of joining"
            value={joined === undefined ? notGiven : formatDisplayDate(joined)}
          />
          <Detail
            label="Last signed in"
            value={
              signedIn === null || signedIn === undefined ? "Not signed in yet" : formatDateTimeIst(signedIn)
            }
          />
          <Detail label="Department" value={person.departmentName ?? noneHeld} />
          <Detail label="Designation" value={person.designationName ?? noneHeld} />
          <Detail label="Manager" value={<Manager person={person} />} />
        </dl>
      </CardContent>
    </Card>
  );
}

function Manager({ person }: { person: PersonResponse }) {
  const { managerId, managerDisplayName } = person;
  if (managerId === null || managerId === undefined) return noneHeld;
  return (
    <Link
      href={`/identity/users/${managerId}`}
      className="rounded-sm wrap-anywhere text-primary underline-offset-4 focus-ring hover:underline"
    >
      {managerDisplayName ?? "Their manager"}
    </Link>
  );
}

function Detail({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="min-w-0 rounded-lg border bg-muted/40 p-4">
      <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</dt>
      <dd className="mt-1 text-base font-medium wrap-anywhere">{value}</dd>
    </div>
  );
}
