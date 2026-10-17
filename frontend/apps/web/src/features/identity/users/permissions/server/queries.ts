import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";

export const getPermissionCatalog = cache(() =>
  callApi((client) => client.api.identity.users.permissions.get()),
);
