import type { Metadata } from "next";
import { notFound } from "next/navigation";

import { mayShow } from "@/shared/auth/permissions";
import { pageMetadata } from "@/shared/auth/require-permission";
import { requireSession } from "@/shared/auth/session";

import { jobsNavigation } from "../../nav";
import { getRun } from "../server/queries";

import { runRow } from "./run-rows";
import { runTitle } from "./run-text";

// The title is the name of the run's job, read through the same cached request as the page, so it is ready when the page is;
// a person without access is never read for, and an unknown run answers the page's not-found.
export async function runPageTitle(runId: string): Promise<Metadata> {
  const { permission } = jobsNavigation;
  if (!mayShow(await requireSession(), permission)) return pageMetadata(runTitle, permission);
  const read = await getRun(runId);
  if (read.status === "missing") notFound();
  const run = read.status === "read" && read.history.run ? runRow(read.history.run) : undefined;
  return pageMetadata(run?.jobLabel ?? runTitle, permission);
}
