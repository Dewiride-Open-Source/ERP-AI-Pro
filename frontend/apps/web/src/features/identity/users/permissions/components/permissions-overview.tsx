import type { PermissionCatalogResponse } from "@dewiride/erp-api-client";
import { Button } from "@dewiride/erp-ui/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@dewiride/erp-ui/components/ui/card";
import { ShieldCheckIcon, UsersIcon } from "lucide-react";
import Link from "next/link";
import { unstable_rethrow } from "next/navigation";

import { ApiError } from "@/shared/api/problem-details";

import { peopleNavigation } from "../../nav";
import { getPermissionCatalog } from "../server/queries";

import { PermissionMatrix } from "./permission-matrix";
import { permissionMatrix } from "./permission-rows";

const matrixHeadingId = "permissions-matrix-heading";

type CatalogRead =
  | { readonly status: "read"; readonly catalog: PermissionCatalogResponse }
  | { readonly status: "failed"; readonly reason: unknown };

export async function PermissionsOverview() {
  const read = await readCatalog();

  return (
    <div className="grid gap-section">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="grid gap-2">
          <div>
            <p className="text-eyebrow text-primary uppercase">{peopleNavigation.area.title}</p>
            <h1 className="text-title">Roles and permissions</h1>
          </div>
          <p className="max-w-prose text-body text-muted-foreground">
            What each built-in role lets a person do. A person&apos;s access in Microsoft Entra makes them an
            employee or an administrator, and an administrator gives people the Finance, HR and Manager roles
            on their pages.
          </p>
        </div>
        <Button asChild variant="outline">
          <Link href={peopleNavigation.basePath} data-testid="permissions-people-link">
            <UsersIcon aria-hidden />
            All people
          </Link>
        </Button>
      </header>

      <Card data-testid="permissions-card">
        <CardHeader>
          <CardTitle>
            <h2 id={matrixHeadingId} className="flex items-center gap-2">
              <ShieldCheckIcon className="size-4 text-muted-foreground" aria-hidden />
              What each role may do
            </h2>
          </CardTitle>
          <CardDescription>
            The roles are built into the ERP, so what each one allows cannot be changed here.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {read.status === "read" ? (
            <Catalog catalog={read.catalog} />
          ) : (
            <Unavailable reason={describeFailure(read.reason)} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

async function readCatalog(): Promise<CatalogRead> {
  try {
    return { status: "read", catalog: await getPermissionCatalog() };
  } catch (error) {
    unstable_rethrow(error);
    return { status: "failed", reason: error };
  }
}

function describeFailure(reason: unknown): string {
  return reason instanceof ApiError ? reason.message : "The API did not respond.";
}

function Catalog({ catalog }: { catalog: PermissionCatalogResponse }) {
  const matrix = permissionMatrix(catalog);

  return matrix.rows.length === 0 ? (
    <p
      role="status"
      data-testid="permissions-empty"
      className="rounded-lg border bg-muted/40 p-4 text-sm text-muted-foreground"
    >
      No permissions are declared yet.
    </p>
  ) : (
    <PermissionMatrix matrix={matrix} labelledBy={matrixHeadingId} />
  );
}

function Unavailable({ reason }: { reason: string }) {
  return (
    <div
      role="status"
      data-testid="permissions-unavailable"
      className="rounded-lg border border-destructive/40 bg-destructive/5 p-4"
    >
      <p className="font-medium">The roles and permissions are not available.</p>
      <p className="mt-1 text-sm text-muted-foreground">{reason}</p>
    </div>
  );
}
