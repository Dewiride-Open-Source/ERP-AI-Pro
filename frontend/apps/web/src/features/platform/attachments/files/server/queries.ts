import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";
import type { ListApiParameters } from "@/shared/lists/list-api";

const attachmentsPage = cache((page: number, pageSize: number, sort: string, filter: string | undefined) =>
  callApi((client) =>
    client.api.platform.attachments.get({
      queryParameters: filter === undefined ? { page, pageSize, sort } : { page, pageSize, sort, filter },
    }),
  ),
);

export function getAttachments({ page, pageSize, sort, filter }: ListApiParameters) {
  return attachmentsPage(page, pageSize, sort, filter);
}

export const getUploadPolicy = cache(() => callApi((client) => client.api.platform.attachments.policy.get()));
