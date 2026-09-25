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
  },
};

export default nextConfig;
