FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY contracts/contracts.csproj contracts/
COPY db/db.csproj db/
COPY api/api.csproj api/
COPY backend.slnx .
RUN dotnet restore

# Copy source and publish
COPY . .
RUN dotnet publish api/api.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# Create uploads directory
RUN mkdir -p /app/uploads

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "api.dll"]
