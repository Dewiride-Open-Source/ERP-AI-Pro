import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";

export const listJobs = cache(() => callApi((client) => client.api.platform.jobs.get()));
