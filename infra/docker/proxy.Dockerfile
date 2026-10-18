FROM docker.io/library/caddy:2.11.7-alpine@sha256:d8542f48d34a9cf4e4c11a478865229840e87e4c96ea3f439101f31a5d35f75f
RUN setcap -r /usr/bin/caddy \
    && addgroup -S -g 10001 caddy \
    && adduser -S -D -H -h /data -s /sbin/nologin -u 10001 -G caddy caddy \
    && chown -R caddy:caddy /data /config \
    && chmod 0700 /data /data/caddy /config /config/caddy
COPY infra/proxy/Caddyfile /etc/caddy/Caddyfile
USER caddy
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 CMD ["wget", "-q", "-O", "/dev/null", "http://127.0.0.1:2019/config/"]
