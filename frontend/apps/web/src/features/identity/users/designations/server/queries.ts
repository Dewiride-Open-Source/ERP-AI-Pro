import "server-only";

import type { DesignationResponse } from "@dewiride/erp-api-client";
import { cache } from "react";

import { callApi } from "@/shared/api/client";

import { everyPage } from "../../organisation/server/queries";

const designationsPageSize = 200;

export const listDesignations = cache((): Promise<DesignationResponse[]> =>
  everyPage((page) =>
    callApi((client) =>
      client.api.identity.users.designations.get({
        queryParameters: { page, pageSize: designationsPageSize, sort: "name:asc" },
      }),
    ),
  ),
);
