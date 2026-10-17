import type { Metadata } from "next";
import { notFound } from "next/navigation";

import { mayShow } from "@/shared/auth/permissions";
import { pageMetadata } from "@/shared/auth/require-permission";
import { requireSession } from "@/shared/auth/session";

import { peopleNavigation } from "../../nav";
import { getPerson } from "../server/queries";

const personTitle = "Person";

// The title is the person's name, read through the same cached request as the page, so it is ready when the page is; a
// person without access is never read for, and an unknown record answers the page's not-found.
export async function personPageTitle(userId: string): Promise<Metadata> {
  const { permission } = peopleNavigation;
  if (!mayShow(await requireSession(), permission)) return pageMetadata(personTitle, permission);
  const read = await getPerson(userId);
  if (read.status === "missing") notFound();
  return pageMetadata(
    read.status === "read" ? read.person.displayName || personTitle : personTitle,
    permission,
  );
}
