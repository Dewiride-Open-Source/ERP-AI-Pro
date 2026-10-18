import "server-only";

import type { PersonResponse, PositionResponse } from "@dewiride/erp-api-client";
import { unstable_rethrow } from "next/navigation";
import { cache } from "react";
import { z } from "zod";

import { callApi } from "@/shared/api/client";
import { ApiError } from "@/shared/api/problem-details";

import { everyPage } from "../../organisation/server/queries";
import type { TeamMember } from "../components/team-handover-dialog";

export type PersonRead =
  | { readonly status: "read"; readonly person: PersonResponse }
  | { readonly status: "missing" }
  | { readonly status: "failed"; readonly reason: unknown };

const personId = z.uuid();

const positionsPageSize = 200;

export const getPerson = cache(async (userId: string): Promise<PersonRead> => {
  if (!personId.safeParse(userId).success) return { status: "missing" };
  try {
    return {
      status: "read",
      person: await callApi((client) => client.api.identity.users.byId(userId).get()),
    };
  } catch (error) {
    unstable_rethrow(error);
    return error instanceof ApiError && error.status === 404
      ? { status: "missing" }
      : { status: "failed", reason: error };
  }
});

export const getTeam = cache(async (userId: string): Promise<TeamMember[]> => {
  const members = await callApi((client) => client.api.identity.users.byId(userId).team.get());
  return members.flatMap(({ id, displayName }) => (id && displayName ? [{ id, displayName }] : []));
});

export const getPositions = cache((userId: string): Promise<PositionResponse[]> =>
  everyPage((page) =>
    callApi((client) =>
      client.api.identity.users.byId(userId).positions.get({
        queryParameters: { page, pageSize: positionsPageSize, sort: "effectiveFrom:desc" },
      }),
    ),
  ),
);
