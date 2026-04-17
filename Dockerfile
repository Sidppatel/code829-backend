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
COPY --from=build /app/publish .

# Run as a non-root user. The uid/gid are fixed so bind-mounted volumes get
# predictable ownership across hosts; alpine's `adduser -D` creates the matching
# group automatically.
RUN addgroup -g 10001 app && adduser -u 10001 -G app -S -H -D app \
    && chown -R app:app /app
USER app

# Render provides PORT at runtime (typically 10000); fallback to 8000 for local Docker
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000
EXPOSE 10000

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT}/health/live || exit 1

ENTRYPOINT ["dotnet", "api.dll"]
