import "server-only";

import { cache } from "react";

import { callApi } from "@/shared/api/client";

export const attachmentsPageSize = 20;

export const getAttachments = cache((page: number) =>
  callApi((client) =>
    client.api.platform.attachments.get({ queryParameters: { page, pageSize: attachmentsPageSize } }),
  ),
);

export const getUploadPolicy = cache(() => callApi((client) => client.api.platform.attachments.policy.get()));
