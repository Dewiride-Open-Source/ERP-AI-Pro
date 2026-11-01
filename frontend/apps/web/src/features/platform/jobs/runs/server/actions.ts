"use server";

import { refresh } from "next/cache";
import { unstable_rethrow } from "next/navigation";
import { z } from "zod";

import { callApi } from "@/shared/api/client";
import { holdsPermission } from "@/shared/auth/permissions";
import { requireSignedInPerson } from "@/shared/auth/session";
import { formMessages } from "@/shared/forms/errors/form-messages";
import { problemToFormState } from "@/shared/forms/errors/problem-to-form-state";

import {
  cancelProblemMessages,
  refusedOutcome,
  rerunProblemMessages,
  runOutOfDate,
  type RunActionOutcome,
} from "../components/run-text";

const runId = z.uuid();

export async function rerunJobRun(id: string): Promise<RunActionOutcome> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.jobs.runs.rerun"))
    return { done: false, message: formMessages.forbidden };
  const parsed = runId.safeParse(id);
  if (!parsed.success) return { done: false, message: runOutOfDate };

  try {
    await callApi((client) => client.api.platform.jobs.runs.byId(parsed.data).rerun.post());
  } catch (error) {
    unstable_rethrow(error);
    return refusedOutcome(problemToFormState(error, { messages: rerunProblemMessages }));
  }
  refresh();
  return { done: true, stopRequested: false };
}

export async function cancelJobRun(id: string): Promise<RunActionOutcome> {
  const person = await requireSignedInPerson();
  if (!holdsPermission(person, "platform.jobs.runs.cancel"))
    return { done: false, message: formMessages.forbidden };
  const parsed = runId.safeParse(id);
  if (!parsed.success) return { done: false, message: runOutOfDate };

  let stopRequested: boolean;
  try {
    const run = await callApi((client) => client.api.platform.jobs.runs.byId(parsed.data).cancel.post());
    stopRequested = run.status === "running";
  } catch (error) {
    unstable_rethrow(error);
    return refusedOutcome(problemToFormState(error, { messages: cancelProblemMessages }));
  }
  refresh();
  return { done: true, stopRequested };
}
