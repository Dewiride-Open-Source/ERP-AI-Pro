import type { Metadata } from "next";
import { notFound } from "next/navigation";

import { mayShow } from "@/shared/auth/permissions";
import { pageMetadata } from "@/shared/auth/require-permission";
import { requireSession } from "@/shared/auth/session";

import { getPerson } from "../../details/server/queries";

const editPermission = "identity.users.people.update";

const editTitle = "Edit a person";

export async function editPersonPageTitle(userId: string): Promise<Metadata> {
  if (!mayShow(await requireSession(), editPermission)) return pageMetadata(editTitle, editPermission);
  const read = await getPerson(userId);
  if (read.status === "missing") notFound();
  const name = read.status === "read" ? read.person.displayName : undefined;
  return pageMetadata(name ? `Edit ${name}` : editTitle, editPermission);
}
