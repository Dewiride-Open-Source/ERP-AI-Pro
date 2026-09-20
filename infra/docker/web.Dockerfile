FROM docker.io/library/node:24-slim@sha256:0e0ff40c39bc087845bfb27465a0df4ea419520094bc35842ff83dd8cbe6f9b6 AS base
ENV PNPM_HOME=/pnpm \
    PATH=/pnpm:$PATH \
    NEXT_TELEMETRY_DISABLED=1
RUN npm install --global pnpm@12.4.2
WORKDIR /workspace

FROM base AS dependencies
COPY frontend/package.json frontend/pnpm-workspace.yaml frontend/pnpm-lock.yaml frontend/.npmrc ./
COPY frontend/apps/web/package.json ./apps/web/
COPY frontend/packages/config/package.json ./packages/config/
COPY frontend/packages/ui/package.json ./packages/ui/
COPY frontend/e2e/package.json ./e2e/
RUN --mount=type=cache,id=pnpm,target=/pnpm/store \
    pnpm install --frozen-lockfile --filter @dewiride/erp-web...

FROM dependencies AS build
ARG NEXT_PUBLIC_APP_NAME=ERP-AI-Pro
ENV NEXT_PUBLIC_APP_NAME=$NEXT_PUBLIC_APP_NAME
COPY frontend/ ./
RUN pnpm --filter @dewiride/erp-web build

FROM docker.io/library/node:24-slim@sha256:0e0ff40c39bc087845bfb27465a0df4ea419520094bc35842ff83dd8cbe6f9b6 AS runtime
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    HOSTNAME=0.0.0.0 \
    PORT=3000
WORKDIR /app
COPY --from=build --chown=node:node /workspace/apps/web/.next/standalone ./
COPY --from=build --chown=node:node /workspace/apps/web/.next/static ./apps/web/.next/static
COPY --from=build --chown=node:node /workspace/apps/web/public ./apps/web/public
USER node
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD ["node", "-e", "fetch('http://127.0.0.1:3000/healthz').then((r) => process.exit(r.ok ? 0 : 1)).catch(() => process.exit(1))"]
CMD ["node", "apps/web/server.js"]
