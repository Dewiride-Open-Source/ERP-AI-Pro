FROM docker.io/library/caddy:2.11.7-alpine@sha256:eb4d35b7452d1ab6171fbdbe8d9275b2301dad6dad92115d91e19fb9b24d510f
COPY COPYING COPYING.LESSER infra/docker/notices/proxy/THIRD-PARTY-NOTICES.txt infra/docker/notices/proxy/third-party-notices.json /usr/share/doc/erp-ai-pro/
RUN { grep -q "\"pkg:github/caddyserver/caddy@${CADDY_VERSION}\"" /usr/share/doc/erp-ai-pro/third-party-notices.json \
      && test -s /usr/share/doc/erp-ai-pro/THIRD-PARTY-NOTICES.txt; } \
    || { echo "infra/docker/notices/proxy does not describe Caddy ${CADDY_VERSION}; run node scripts/notices/generate.ts" >&2; \
      exit 1; } \
    && setcap -r /usr/bin/caddy \
    && addgroup -S -g 10001 caddy \
    && adduser -S -D -H -h /data -s /sbin/nologin -u 10001 -G caddy caddy \
    && chown -R caddy:caddy /data /config \
    && chmod 0700 /data /data/caddy /config /config/caddy
COPY infra/proxy/Caddyfile /etc/caddy/Caddyfile
USER caddy
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 CMD ["wget", "-q", "-O", "/dev/null", "http://127.0.0.1:2019/config/"]
ARG ERP_VERSION=local
ARG ERP_REVISION=unknown
ARG ERP_CREATED
LABEL org.opencontainers.image.title="ERP-AI-Pro edge proxy" \
      org.opencontainers.image.description="The ERP-AI-Pro edge proxy: Caddy with the ERP's site configuration" \
      org.opencontainers.image.version="$ERP_VERSION" \
      org.opencontainers.image.revision="$ERP_REVISION" \
      org.opencontainers.image.created="$ERP_CREATED" \
      org.opencontainers.image.source="https://github.com/Dewiride-Open-Source/ERP-AI-Pro" \
      org.opencontainers.image.url="https://github.com/Dewiride-Open-Source/ERP-AI-Pro" \
      org.opencontainers.image.documentation="https://github.com/Dewiride-Open-Source/ERP-AI-Pro/tree/main/docs" \
      org.opencontainers.image.vendor="Dewiride Technologies Private Limited" \
      org.opencontainers.image.licenses="LGPL-3.0-only AND Apache-2.0"
