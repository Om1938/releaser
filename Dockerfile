# syntax=docker/dockerfile:1.7
# Releaser: API + static dashboard in one image. Build stages run on the build platform; the runtime is multi-arch.

FROM --platform=$BUILDPLATFORM node:24.19.0-alpine AS web
WORKDIR /web
RUN corepack enable
COPY web/package.json web/pnpm-lock.yaml web/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY web/ ./
RUN pnpm build

# Framework-dependent, RID-neutral publish: one build output runs on every runtime architecture.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Releaser.Domain/*.csproj src/Releaser.Domain/packages.lock.json src/Releaser.Domain/
COPY src/Releaser.Electron/*.csproj src/Releaser.Electron/packages.lock.json src/Releaser.Electron/
COPY src/Releaser.Server/*.csproj src/Releaser.Server/packages.lock.json src/Releaser.Server/
RUN dotnet restore src/Releaser.Server/Releaser.Server.csproj --locked-mode
COPY src/ src/
RUN dotnet publish src/Releaser.Server/Releaser.Server.csproj -c Release --no-restore \
      -p:OpenApiGenerateDocuments=false -p:ContinuousIntegrationBuild=true -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
# krb5-libs: Npgsql probes GSSAPI during connection negotiation; without it the process can crash on Alpine.
RUN apk add --no-cache krb5-libs
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=30s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8080/health/ready > /dev/null || exit 1
ENTRYPOINT ["dotnet", "Releaser.Server.dll"]
