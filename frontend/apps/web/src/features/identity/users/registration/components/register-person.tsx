import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";

import { mayShow } from "@/shared/auth/permissions";
import { requireSession } from "@/shared/auth/session";

import { RegisterPersonForm } from "./register-person-form";

const formHeadingId = "register-person-heading";

export async function RegisterPerson() {
  const canAssignRoles = mayShow(await requireSession(), "identity.users.people.assign-roles");

  return (
    <div className="grid min-w-0 grid-cols-1 gap-section">
      <header className="grid gap-2">
        <p className="text-eyebrow text-primary uppercase">People</p>
        <h1 className="text-title">Register a person</h1>
        <p className="max-w-prose text-body text-muted-foreground">
          Register someone before their first sign-in. When they sign in with their Microsoft account, the ERP
          links it to this record by their work email.
        </p>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>
            <h2 id={formHeadingId}>Their details</h2>
          </CardTitle>
          <CardDescription>
            The name and the work email are needed now; the rest can be added later.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <RegisterPersonForm labelledBy={formHeadingId} canAssignRoles={canAssignRoles} />
        </CardContent>
      </Card>
    </div>
  );
}
