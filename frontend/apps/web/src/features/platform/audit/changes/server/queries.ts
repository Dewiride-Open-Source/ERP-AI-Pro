import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";
import type { ListApiParameters } from "@/shared/lists/list-api";

const changesPage = cache((page: number, pageSize: number, sort: string, filter: string | undefined) =>
  callApi((client) =>
    client.api.platform.audit.changes.get({
      queryParameters: filter === undefined ? { page, pageSize, sort } : { page, pageSize, sort, filter },
    }),
  ),
);

export function getChanges({ page, pageSize, sort, filter }: ListApiParameters) {
  return changesPage(page, pageSize, sort, filter);
}
