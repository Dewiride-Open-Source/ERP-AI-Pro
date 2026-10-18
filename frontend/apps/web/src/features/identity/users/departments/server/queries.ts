import "server-only";

import type { DepartmentResponse } from "@dewiride/erp-api-client";
import { cache } from "react";

import { callApi } from "@/shared/api/client";

import { everyPage } from "../../organisation/server/queries";

const departmentsPageSize = 200;

export const listDepartments = cache((): Promise<DepartmentResponse[]> =>
  everyPage((page) =>
    callApi((client) =>
      client.api.identity.users.departments.get({
        queryParameters: { page, pageSize: departmentsPageSize, sort: "name:asc" },
      }),
    ),
  ),
);
