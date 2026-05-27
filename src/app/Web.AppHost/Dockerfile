# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -fsS http://localhost:8080/alive || exit 1


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Web.AppHost/Web.AppHost.csproj", "Web.AppHost/"]
COPY ["Aspire/Aspire.ServiceDefaults/Aspire.ServiceDefaults.csproj", "./Aspire/Aspire.ServiceDefaults/"]
COPY ["Model.Weather/Model.Weather.csproj", "./Model.Weather/"]
COPY ["Web.Components.Weather/Web.Components.Weather.csproj", "./Web.Components.Weather/"]
COPY ["Web.Shared.Components.Layout/Web.Shared.Components.Layout.csproj", "./Web.Shared.Components.Layout/"]
COPY ["Web.Shared.Components.Pages/Web.Shared.Components.Pages.csproj", "./Web.Shared.Components.Pages/"]
COPY ["Web.Shared/Web.Shared.csproj", "./Web.Shared/"]
COPY ["Web.Shared.Assets/Web.Shared.Assets.csproj", "./Web.Shared.Assets/"]
COPY ["Web.Shared.Components/Web.Shared.Components.csproj", "./Web.Shared.Components/"]
RUN pwd
RUN ls .
RUN dotnet restore "./Web.AppHost/Web.AppHost.csproj"
COPY . .
WORKDIR "/src/Web.AppHost"
RUN dotnet build "./Web.AppHost.csproj" --no-restore -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Web.AppHost.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Web.AppHost.dll"]