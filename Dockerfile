# Base images pinned by digest to prevent unreviewed image drift / supply-chain
# substitution. Refresh digests with Dependabot/Renovate on a schedule; do not
# bump blindly — validate the rebuilt image before promoting.

# =========================
# BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:732cd42c6f659814c9804ad7b05c7f761e83ef8379c5b2fdc3af673353caff73 AS build
WORKDIR /src

# 1. Copy solution + every project file before restoring so the restore
#    layer is only invalidated when dependencies actually change.
COPY backend.slnx ./
COPY api/api.csproj                                    ./api/
COPY contracts/contracts.csproj                        ./contracts/
COPY db/db.csproj                                      ./db/
COPY tests/Api.Tests/Api.Tests.csproj                  ./tests/Api.Tests/
COPY tests/IntegrationTests/IntegrationTests.csproj    ./tests/IntegrationTests/
COPY tools/Analyzers/Analyzers.csproj                  ./tools/Analyzers/

# 2. Restore only the publishable project — the solution file now includes
#    tests/IntegrationTests which pulls Testcontainers + Microsoft.AspNetCore.Mvc.Testing,
#    neither of which are needed to ship the API. Scoping the restore to api.csproj
#    keeps the runtime image slim and the build step fast.
RUN dotnet restore api/api.csproj

# 3. Copy the rest of the source
COPY . .

# 4. Publish — no restore, no native app host (smaller image, faster publish)
RUN dotnet publish api/api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false

# =========================
# RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:1201dde897ab436b7c6b386f6dbd4f9a3ca0245f9c5a8aac8f8bcdccb4c7d484 AS runtime

RUN apk add --no-cache krb5-libs

WORKDIR /app

# Ensure the app user has permission to write to /app and its subdirectories
# for local file storage (uploads) and logging.
RUN mkdir -p uploads logs && chown -R app:app /app

# .NET 10 Alpine images ship with a non-root `app` user (UID/GID 10001) pre-created.
COPY --from=build --chown=app:app /app/publish .
USER app

# Render provides PORT at runtime (typically 10000); fallback to 8000 for local Docker
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=10000
EXPOSE 10000

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT}/health/live || exit 1

ENTRYPOINT ["dotnet", "api.dll"]
