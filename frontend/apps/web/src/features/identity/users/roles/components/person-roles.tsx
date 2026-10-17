import type { PersonRole } from "@dewiride/erp-api-client";
import { Badge } from "@dewiride/erp-ui/components/ui/badge";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { ShieldIcon } from "lucide-react";
import Link from "next/link";

import { AssignRolesDialog } from "./assign-roles-dialog";
import { orderedRoles, roleLabels } from "./role-labels";

const rolesHeadingId = "person-roles-heading";

export function PersonRoles({
  personId,
  personName,
  roles,
  version,
  canAssign,
}: {
  personId: string;
  personName: string;
  roles: readonly PersonRole[];
  version: string | undefined;
  canAssign: boolean;
}) {
  const held = orderedRoles(roles);

  return (
    <Card data-testid="person-roles">
      <CardHeader>
        <CardTitle>
          <h2 id={rolesHeadingId} className="flex items-center gap-2">
            <ShieldIcon className="size-4 text-muted-foreground" aria-hidden />
            Roles
          </h2>
        </CardTitle>
        <CardDescription>
          Everyone who signs in is an employee, and administrators are chosen in Microsoft Entra. These are
          the roles given in the ERP.
        </CardDescription>
        {canAssign && version !== undefined ? (
          <CardAction>
            <AssignRolesDialog personId={personId} personName={personName} roles={held} version={version} />
          </CardAction>
        ) : null}
      </CardHeader>
      <CardContent className="grid gap-3">
        {held.length === 0 ? (
          <p className="text-muted-foreground" data-testid="person-roles-none">
            No roles are given in the ERP.
          </p>
        ) : (
          <ul role="list" aria-labelledby={rolesHeadingId} className="flex flex-wrap gap-2">
            {held.map((role) => (
              <li key={role}>
                <Badge variant="secondary">{roleLabels[role]}</Badge>
              </li>
            ))}
          </ul>
        )}
        <Link
          href="/identity/users/permissions"
          className="w-fit rounded-sm text-primary underline-offset-4 focus-ring hover:underline"
        >
          See what each role may do
        </Link>
      </CardContent>
    </Card>
  );
}
