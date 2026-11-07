import type { Metadata } from "next";
import { notFound } from "next/navigation";

import { mayShow } from "@/shared/auth/permissions";
import { pageMetadata } from "@/shared/auth/require-permission";
import { requireSession } from "@/shared/auth/session";
import type { SearchParameters } from "@/shared/lists/list-query";

import { parameterSetsNavigation } from "../../nav";
import { getParameterSet } from "../../parameter-sets/server/queries";

import { isParameterSetKey, readParameterSetView } from "./parameter-set-view";

export const parameterSetTitle = "Statutory parameter set";

// The title is the set's own, read through the same cached request as the page, for the same version, so it is ready when the
// page is; a person without access is never read for, and an unknown set answers the page's not-found.
export async function parameterSetPageTitle(
  setKey: string,
  searchParameters: SearchParameters,
): Promise<Metadata> {
  const { permission } = parameterSetsNavigation;
  if (!mayShow(await requireSession(), permission)) return pageMetadata(parameterSetTitle, permission);
  if (!isParameterSetKey(setKey)) notFound();
  const read = await getParameterSet(setKey, readParameterSetView(searchParameters).view.version);
  if (read.status === "missing") notFound();
  return pageMetadata(read.status === "read" ? read.set.title : parameterSetTitle, permission);
}
