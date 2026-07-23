# Build with context = src/. Example:
#   docker build --file metering/Metering.Api/Dockerfile -t metering-api:latest ./src

# --- build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for restore-layer caching.
COPY metering/Metering.Domain/*.csproj          metering/Metering.Domain/
COPY metering/Metering.Contracts/*.csproj        metering/Metering.Contracts/
COPY metering/Metering.Application/*.csproj       metering/Metering.Application/
COPY metering/Metering.Infrastructure/*.csproj    metering/Metering.Infrastructure/
COPY metering/Metering.Api/*.csproj               metering/Metering.Api/
COPY Aspire/Cicd.Aspire.ServiceDefaults/*.csproj  Aspire/Cicd.Aspire.ServiceDefaults/
RUN dotnet restore metering/Metering.Api/Metering.Api.csproj

# Copy the rest and publish.
COPY metering/ metering/
COPY Aspire/Cicd.Aspire.ServiceDefaults/ Aspire/Cicd.Aspire.ServiceDefaults/
RUN dotnet publish metering/Metering.Api/Metering.Api.csproj -c Release -o /app/publish --no-restore

# --- runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl for the HEALTHCHECK; the aspnet base image doesn't include it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=5 \
    CMD curl -fsS http://localhost:8080/ || exit 1

ENTRYPOINT ["dotnet", "Metering.Api.dll"]
