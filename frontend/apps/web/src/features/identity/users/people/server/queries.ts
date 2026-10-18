import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";
import type { ListApiParameters } from "@/shared/lists/list-api";

const peoplePage = cache((page: number, pageSize: number, sort: string, filter: string | undefined) =>
  callApi((client) =>
    client.api.identity.users.get({
      queryParameters: filter === undefined ? { page, pageSize, sort } : { page, pageSize, sort, filter },
    }),
  ),
);

export function getPeople({ page, pageSize, sort, filter }: ListApiParameters) {
  return peoplePage(page, pageSize, sort, filter);
}
