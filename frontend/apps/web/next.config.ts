import { fileURLToPath } from "node:url";

import type { NextConfig } from "next";

import { readPublicEnv } from "./src/shared/config/env.schema";

readPublicEnv(process.env);

const nextConfig: NextConfig = {
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
    // proxy.ts rewrites /api at run time, and a request that passes through it is buffered up to this size and silently cut
    // off beyond it; the ceiling covers the largest upload the API accepts (AttachmentsOptions.MaxSizeBytesCeiling, 100 MiB)
    // plus its multipart framing. Production is unaffected: the edge proxy sends /api straight to the API.
    proxyClientMaxBodySize: "101mb",
  },
};

export default nextConfig;
