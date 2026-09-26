import { fileURLToPath } from "node:url";

import type { NextConfig } from "next";
import { PHASE_DEVELOPMENT_SERVER, PHASE_PRODUCTION_SERVER } from "next/constants";

import { readPublicEnv } from "./src/shared/config/env.schema";

readPublicEnv(process.env);

const sourceTreeServerPhases: readonly string[] = [PHASE_DEVELOPMENT_SERVER, PHASE_PRODUCTION_SERVER];

export default function nextConfig(phase: string): NextConfig {
  return {
    output: "standalone",
    outputFileTracingRoot: fileURLToPath(new URL("../../", import.meta.url)),
    transpilePackages: ["@dewiride/erp-ui"],
    typedRoutes: true,
    poweredByHeader: false,
    reactStrictMode: true,
    cacheComponents: false,
    experimental: {
      // Above the API's longest permitted Erp:Platform:Host:RequestTimeout (10 minutes), so a slow /api call ends with the
      // API's own 504 problem rather than the rewrite proxy cutting it off at its 30-second default.
      proxyTimeout: 660_000,
      // Every request proxy.ts matches (pages and Server Functions as well as the /api rewrite) has its body held in memory
      // up to this size and silently cut off beyond it. `next dev` and `next start` load this file as they start and send
      // /api through the rewrite, so for them the limit covers the largest upload the API accepts
      // (AttachmentsOptions.MaxSizeBytesCeiling, 100 MiB) plus its multipart framing. The image's standalone server runs
      // with the configuration `next build` recorded and keeps the 10 MB default: there the edge proxy sends /api straight
      // to the API, and no other request needs a larger buffer.
      ...(sourceTreeServerPhases.includes(phase) && { proxyClientMaxBodySize: "101mb" }),
    },
  };
}
