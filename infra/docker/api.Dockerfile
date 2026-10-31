FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /source

COPY .editorconfig ./
COPY backend/global.json backend/nuget.config backend/Directory.Build.props backend/Directory.Build.targets backend/Directory.Packages.props backend/testconfig.json ./
COPY backend/build/ ./build/
COPY backend/BuildingBlocks/ ./BuildingBlocks/
COPY backend/Modules/ ./Modules/
COPY backend/Hosts/ ./Hosts/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore Hosts/Api/Dewiride.Erp.Host.Api/Dewiride.Erp.Host.Api.csproj --locked-mode \
 && dotnet restore Hosts/HealthProbe/Dewiride.Erp.Host.HealthProbe/Dewiride.Erp.Host.HealthProbe.csproj --locked-mode \
 && dotnet restore Hosts/Migrator/Dewiride.Erp.Host.Migrator/Dewiride.Erp.Host.Migrator.csproj --locked-mode

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish Hosts/Api/Dewiride.Erp.Host.Api/Dewiride.Erp.Host.Api.csproj \
      --configuration "$BUILD_CONFIGURATION" --no-restore --output /app/api -p:ContinuousIntegrationBuild=true \
 && dotnet publish Hosts/HealthProbe/Dewiride.Erp.Host.HealthProbe/Dewiride.Erp.Host.HealthProbe.csproj \
      --configuration "$BUILD_CONFIGURATION" --no-restore --output /app/probe -p:ContinuousIntegrationBuild=true \
 && dotnet publish Hosts/Migrator/Dewiride.Erp.Host.Migrator/Dewiride.Erp.Host.Migrator.csproj \
      --configuration "$BUILD_CONFIGURATION" --no-restore --output /app/migrator -p:ContinuousIntegrationBuild=true

RUN forbidden="$(find /app -iname '*msalruntime*' -o -iname 'Microsoft.Identity.Client.NativeInterop.dll')" \
 && if [ -n "$forbidden" ]; then printf 'The published hosts hold the MSAL native runtime, whose licence forbids distributing it:\n%s\n' "$forbidden" >&2; exit 1; fi

RUN mkdir /data-protection-keys

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:9b755ea799ae649141a073e30d8b62d8095a5465af2b7694d17866e8c05131d8 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    TZ=Asia/Kolkata \
    XDG_DATA_HOME=/home/app/.local/share
COPY COPYING COPYING.LESSER infra/docker/notices/api/THIRD-PARTY-NOTICES.txt infra/docker/notices/api/third-party-notices.json /usr/share/doc/erp-ai-pro/
COPY --from=build --chown=app:app /app/api/ ./
COPY --from=build --chown=app:app /app/probe/ ./probe/
COPY --from=build --chown=app:app /app/migrator/ ./migrator/
COPY --from=build --chown=app:app --chmod=700 /data-protection-keys/ /home/app/.local/share/ERP-AI-Pro/DataProtection-Keys/
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD ["dotnet", "/app/probe/Dewiride.Erp.Host.HealthProbe.dll"]
ENTRYPOINT ["dotnet", "Dewiride.Erp.Host.Api.dll"]
ARG ERP_VERSION=local
ARG ERP_REVISION=unknown
ARG ERP_CREATED
LABEL org.opencontainers.image.title="ERP-AI-Pro API" \
      org.opencontainers.image.description="The ERP-AI-Pro API host, with its database migrator and health probe" \
      org.opencontainers.image.version="$ERP_VERSION" \
      org.opencontainers.image.revision="$ERP_REVISION" \
      org.opencontainers.image.created="$ERP_CREATED" \
      org.opencontainers.image.source="https://github.com/Dewiride-Open-Source/ERP-AI-Pro" \
      org.opencontainers.image.url="https://github.com/Dewiride-Open-Source/ERP-AI-Pro" \
      org.opencontainers.image.documentation="https://github.com/Dewiride-Open-Source/ERP-AI-Pro/tree/main/docs" \
      org.opencontainers.image.vendor="Dewiride Technologies Private Limited" \
      org.opencontainers.image.licenses="LGPL-3.0-only"
