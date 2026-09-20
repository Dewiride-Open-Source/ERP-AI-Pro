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
};

export default nextConfig;
