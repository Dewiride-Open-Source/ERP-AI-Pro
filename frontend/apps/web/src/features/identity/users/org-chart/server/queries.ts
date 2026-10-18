import "server-only";

import type { OrgChartPersonResponse } from "@dewiride/erp-api-client";
import { cache } from "react";

import { callApi } from "@/shared/api/client";
import { queryDateOf } from "@/shared/api/dates/query-date";

import { everyPage } from "../../organisation/server/queries";

const chartPageSize = 200;

export const getOrgChart = cache((on: string | undefined): Promise<OrgChartPersonResponse[]> =>
  everyPage((page) =>
    callApi((client) =>
      client.api.identity.users.orgChart.get({
        queryParameters: {
          page,
          pageSize: chartPageSize,
          sort: "displayName:asc",
          ...(on === undefined ? {} : { on: queryDateOf(on) }),
        },
      }),
    ),
  ),
);
