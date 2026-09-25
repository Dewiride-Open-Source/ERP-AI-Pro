import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";

export const getSystemInfo = cache(() => callApi((client) => client.api.platform.systemInfo.get()));
