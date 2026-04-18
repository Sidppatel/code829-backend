# Base images pinned by digest to prevent unreviewed image drift / supply-chain
# substitution. Refresh digests with Dependabot/Renovate on a schedule; do not
# bump blindly — validate the rebuilt image before promoting.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:732cd42c6f659814c9804ad7b05c7f761e83ef8379c5b2fdc3af673353caff73 AS build
WORKDIR /src
COPY . .
RUN dotnet restore backend.slnx
RUN dotnet publish api/api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:1201dde897ab436b7c6b386f6dbd4f9a3ca0245f9c5a8aac8f8bcdccb4c7d484 AS runtime
WORKDIR /app

# .NET 10 Alpine images ship with a non-root `app` user (UID/GID 10001) pre-created,
# so creating it ourselves now errors with "group 'app' in use". Just reuse it and
# chown published files to it during COPY.
COPY --from=build --chown=app:app /app/publish .
USER app

# Render provides PORT at runtime (typically 10000); fallback to 8000 for local Docker
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000
EXPOSE 10000

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT}/health/live || exit 1

ENTRYPOINT ["dotnet", "api.dll"]
