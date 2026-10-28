FROM docker.io/library/node:24-slim@sha256:d6aa754f16b3197301076f047b5def2f02ea1dbbc2ca920407d46d7ec7f87b20 AS base
ENV PNPM_HOME=/pnpm \
    PATH=/pnpm:$PATH \
    NEXT_TELEMETRY_DISABLED=1
RUN npm install --global pnpm@12.4.2
WORKDIR /workspace

FROM base AS dependencies
COPY frontend/package.json frontend/pnpm-workspace.yaml frontend/pnpm-lock.yaml frontend/.npmrc ./
COPY frontend/apps/web/package.json ./apps/web/
COPY frontend/packages/api-client/package.json ./packages/api-client/
COPY frontend/packages/config/package.json ./packages/config/
COPY frontend/packages/ui/package.json ./packages/ui/
COPY frontend/e2e/package.json ./e2e/
RUN --mount=type=cache,id=pnpm,target=/pnpm/store \
    pnpm install --frozen-lockfile --filter @dewiride/erp-web...

FROM dependencies AS build
ARG NEXT_PUBLIC_APP_NAME=ERP-AI-Pro
ENV NEXT_PUBLIC_APP_NAME=$NEXT_PUBLIC_APP_NAME
COPY frontend/ ./
RUN pnpm --filter "@dewiride/erp-web..." build \
 && forbidden="$(find apps/web/.next/standalone \( -name sharp -o -name '@img' -o -iname '*libvips*' -o -name 'sharp-*.node' \
      -o -path '*/.pnpm/sharp@*' -o -path '*/.pnpm/@img+*' \) -print)" \
 && if [ -n "$forbidden" ]; then printf 'The standalone output holds sharp or libvips, which the web image keeps out:\n%s\n' "$forbidden" >&2; exit 1; fi

FROM docker.io/library/node:24-slim@sha256:d6aa754f16b3197301076f047b5def2f02ea1dbbc2ca920407d46d7ec7f87b20 AS runtime
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    HOSTNAME=0.0.0.0 \
    PORT=3000
WORKDIR /app
COPY COPYING COPYING.LESSER infra/docker/notices/web/THIRD-PARTY-NOTICES.txt infra/docker/notices/web/third-party-notices.json /usr/share/doc/erp-ai-pro/
COPY --from=build --chown=node:node /workspace/apps/web/.next/standalone ./
COPY --from=build --chown=node:node /workspace/apps/web/.next/static ./apps/web/.next/static
COPY --from=build --chown=node:node /workspace/apps/web/public ./apps/web/public
USER node
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD ["node", "-e", "fetch('http://127.0.0.1:3000/healthz').then((r) => process.exit(r.ok ? 0 : 1)).catch(() => process.exit(1))"]
CMD ["node", "apps/web/server.js"]
ARG ERP_VERSION=local
ARG ERP_REVISION=unknown
ARG ERP_CREATED
LABEL org.opencontainers.image.title="ERP-AI-Pro web app" \
      org.opencontainers.image.description="The ERP-AI-Pro web app, served by the Next.js standalone server" \
      org.opencontainers.image.version="$ERP_VERSION" \
      org.opencontainers.image.revision="$ERP_REVISION" \
      org.opencontainers.image.created="$ERP_CREATED" \
      org.opencontainers.image.source="https://github.com/Dewiride-Open-Source/ERP-AI-Pro" \
      org.opencontainers.image.url="https://github.com/Dewiride-Open-Source/ERP-AI-Pro" \
      org.opencontainers.image.documentation="https://github.com/Dewiride-Open-Source/ERP-AI-Pro/tree/main/docs" \
      org.opencontainers.image.vendor="Dewiride Technologies Private Limited" \
      org.opencontainers.image.licenses="LGPL-3.0-only"
