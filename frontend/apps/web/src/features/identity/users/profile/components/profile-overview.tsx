import type { ProfileResponse } from "@dewiride/erp-api-client";
import { Avatar, AvatarFallback, AvatarImage } from "@dewiride/erp-ui/components/ui/avatar";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { formatDisplayDate } from "@dewiride/erp-ui/lib/calendar-date";
import { IdCardIcon, PaletteIcon, ShieldIcon, UserIcon } from "lucide-react";
import Link from "next/link";
import { notFound } from "next/navigation";

import { isoDateOf } from "@/shared/api/dates/date-only";
import { mayShow } from "@/shared/auth/permissions";
import { requireSession, type HeldSession } from "@/shared/auth/session";
import { personInitials } from "@/shared/format/person-initials";

import { getOwnProfile } from "../server/queries";

import { ownPhotoPath } from "./own-photo";
import { notRecorded, profileTitle, recordedText, roleNames } from "./profile-text";
import { ThemeChooser } from "./theme-chooser";

const rolesHeadingId = "profile-roles-heading";

const themeHeadingId = "profile-theme-heading";

export async function ProfileOverview() {
  const [session, read] = await Promise.all([requireSession(), getOwnProfile()]);
  if (read.status === "disabled") notFound();

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-2">
        <h1 className="text-title">{profileTitle}</h1>
        <p className="max-w-prose text-body text-muted-foreground">
          Your details in the ERP. Microsoft Entra keeps your name, sign-in name and photo; an administrator
          keeps the rest.
        </p>
      </header>

      {read.status === "read" ? (
        <Profile profile={read.profile} session={session} />
      ) : (
        <ProfileUnavailable missing={read.status === "missing"} />
      )}
    </div>
  );
}

function Profile({ profile, session }: { profile: ProfileResponse; session: HeldSession }) {
  const name = recordedText(profile.displayName);
  const signInName =
    session.status === "signed-in" && session.person.userName !== ""
      ? session.person.userName
      : profile.workEmail;
  const initials = personInitials(profile.displayName ?? "");
  const joined = isoDateOf(profile.dateOfJoining);
  const roles = roleNames(profile.roles);

  return (
    <>
      <Card data-testid="profile-person">
        <CardContent className="flex flex-wrap items-center gap-4">
          <Avatar className="size-24" data-testid="profile-photo">
            <AvatarImage src={ownPhotoPath} alt={`Photo of ${name}`} />
            <AvatarFallback className="text-2xl">
              {initials === "" ? <UserIcon className="size-10" aria-hidden /> : initials}
            </AvatarFallback>
          </Avatar>
          <div className="grid min-w-0 gap-1">
            <h2 className="text-heading wrap-anywhere">{name}</h2>
            <p className="wrap-anywhere text-muted-foreground">{recordedText(signInName)}</p>
          </div>
        </CardContent>
      </Card>

      <Card data-testid="profile-details">
        <CardHeader>
          <CardTitle>
            <h2 className="flex items-center gap-2">
              <IdCardIcon className="size-4 text-muted-foreground" aria-hidden />
              Your details
            </h2>
          </CardTitle>
          <CardDescription>
            The department, designation and manager are those of today. To change any of these, ask an
            administrator.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-2">
            <Detail label="Employee code" value={recordedText(profile.employeeCode)} />
            <Detail label="Phone number" value={recordedText(profile.phoneNumber)} />
            <Detail
              label="Date of joining"
              value={joined === undefined ? notRecorded : formatDisplayDate(joined)}
            />
            <Detail label="Department" value={recordedText(profile.departmentName)} />
            <Detail label="Designation" value={recordedText(profile.designationName)} />
            <Detail label="Manager" value={recordedText(profile.managerDisplayName)} />
          </dl>
        </CardContent>
      </Card>

      <Card data-testid="profile-roles">
        <CardHeader>
          <CardTitle>
            <h2 id={rolesHeadingId} className="flex items-center gap-2">
              <ShieldIcon className="size-4 text-muted-foreground" aria-hidden />
              Your roles
            </h2>
          </CardTitle>
          <CardDescription>
            Your access in Microsoft Entra makes you an employee or an administrator, and an administrator
            gives the Finance, HR and Manager roles in the ERP.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          {roles.length === 0 ? (
            <p className="text-muted-foreground">{notRecorded}</p>
          ) : (
            <ul role="list" aria-labelledby={rolesHeadingId} className="flex flex-wrap gap-2">
              {roles.map((role) => (
                <li key={role}>
                  <Badge variant="secondary">{role}</Badge>
                </li>
              ))}
            </ul>
          )}
          {mayShow(session, "identity.users.people.read") ? (
            <Link
              href="/identity/users/permissions"
              className="w-fit rounded-sm text-primary underline-offset-4 focus-ring hover:underline"
            >
              See what each role may do
            </Link>
          ) : null}
        </CardContent>
      </Card>

      <Card data-testid="profile-theme">
        <CardHeader>
          <CardTitle>
            <h2 id={themeHeadingId} className="flex items-center gap-2">
              <PaletteIcon className="size-4 text-muted-foreground" aria-hidden />
              Colour theme
            </h2>
          </CardTitle>
          <CardDescription>
            Your choice is saved to your account, so the ERP looks the same in every browser you sign in from.
            The theme button at the top of every page changes it too.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ThemeChooser labelledBy={themeHeadingId} />
        </CardContent>
      </Card>
    </>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0 rounded-lg border bg-muted/40 p-4">
      <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</dt>
      <dd className="mt-1 text-base font-medium wrap-anywhere">{value}</dd>
    </div>
  );
}

function ProfileUnavailable({ missing }: { missing: boolean }) {
  return (
    <div
      role="status"
      data-testid="profile-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">
        {missing ? "The ERP holds no record of you." : "Your profile cannot be shown right now."}
      </p>
      <p className="mt-1 text-sm text-muted-foreground">
        {missing
          ? "Sign out and sign in again. If this keeps happening, ask an administrator."
          : "The ERP could not read your details. Try again in a moment."}
      </p>
    </div>
  );
}
