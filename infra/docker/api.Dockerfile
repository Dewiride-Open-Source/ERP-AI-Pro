FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:6385dc0eaef704fad88d3f65c334e791a371bbe448f52ca39d83d2df49251e28 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    TZ=Asia/Kolkata
COPY --from=build --chown=app:app /app/api/ ./
COPY --from=build --chown=app:app /app/probe/ ./probe/
COPY --from=build --chown=app:app /app/migrator/ ./migrator/
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD ["dotnet", "/app/probe/Dewiride.Erp.Host.HealthProbe.dll"]
ENTRYPOINT ["dotnet", "Dewiride.Erp.Host.Api.dll"]
