import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { notFound } from "next/navigation";

import { isoDateOf } from "@/shared/api/dates/date-only";
import { ApiError } from "@/shared/api/problem-details";

import { getPerson } from "../../details/server/queries";

import { EditPersonForm } from "./edit-person-form";

const formHeadingId = "edit-person-heading";

export async function EditPerson({ userId }: { userId: string }) {
  const read = await getPerson(userId);
  if (read.status === "missing") notFound();
  if (read.status === "failed") return <EditUnavailable reason={describeFailure(read.reason)} />;

  const { person } = read;
  const id = person.id ?? userId;
  const name = person.displayName || "this person";

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">People</p>
        <h1 className="text-title wrap-anywhere">Edit {name}</h1>
        <p className="max-w-prose text-body text-muted-foreground">
          The name and work email come from Microsoft Entra each time {name} signs in, so they are changed
          there.
        </p>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>
            <h2 id={formHeadingId}>Details kept in the ERP</h2>
          </CardTitle>
          <CardDescription>Clear a box to remove what it holds.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-6">
          <dl className="grid gap-4 sm:grid-cols-2" data-testid="edit-person-identity">
            <div className="min-w-0">
              <dt className="text-sm text-muted-foreground">Name</dt>
              <dd className="font-medium wrap-anywhere">{person.displayName}</dd>
            </div>
            <div className="min-w-0">
              <dt className="text-sm text-muted-foreground">Work email</dt>
              <dd className="font-medium wrap-anywhere">{person.workEmail}</dd>
            </div>
          </dl>
          <EditPersonForm
            labelledBy={formHeadingId}
            defaultValues={{
              id,
              employeeCode: person.employeeCode ?? "",
              phoneNumber: person.phoneNumber ?? "",
              dateOfJoining: isoDateOf(person.dateOfJoining) ?? "",
              version: person.version ?? "",
            }}
          />
        </CardContent>
      </Card>
    </div>
  );
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function EditUnavailable({ reason }: { reason: string }) {
  return (
    <div className="grid gap-section">
      <header className="grid gap-1">
        <p className="text-eyebrow text-primary uppercase">People</p>
        <h1 className="text-title">Edit a person</h1>
      </header>
      <div
        role="status"
        data-testid="edit-person-unavailable"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
      >
        <p className="font-medium">This person&apos;s details are not available.</p>
        <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
      </div>
    </div>
  );
}
