# Shared, generic runtime image for CI-built .NET app containers.
#
# Copy-only — NEVER compiles inside the container. The cicd-publish-nexus-docker job
# runs `dotnet publish` on the agent and passes the publish output as the build context,
# so this Dockerfile only layers those files onto the ASP.NET runtime and runs the app.
# One template serves every containerizable app; the only per-app difference is the
# entry assembly, passed as --build-arg ENTRY_ASSEMBLY=<AssemblyName>.
#
# Honours the project Docker Strategy: non-root (USER $APP_UID), HEALTHCHECK on /alive,
# port 8080.

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG ENTRY_ASSEMBLY

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Build context is the publish output directory (publish/<containerName>) — copy it all.
COPY . ./

USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
# Captured at build time so the exec-form ENTRYPOINT can run it (exec-form can't expand
# a build ARG directly).
ENV APP_ENTRY_DLL=${ENTRY_ASSEMBLY}.dll

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -fsS http://localhost:8080/alive || exit 1

# `exec` keeps dotnet as PID 1 so SIGTERM (Cloud Run / docker stop) reaches the app.
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_ENTRY_DLL\""]
