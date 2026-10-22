# Worker — multi-stage chiseled non-root build (Phase 4, T011; US2 / SC-002).
#
# Build context MUST be the repo root (see deployments/docker-compose.yml
# `worker.build.context: ..`), so all COPY source paths below are
# relative to the repo root, not to this Dockerfile's directory.
#
# Same pattern as src/Services/Identity/Dockerfile: SDK build stage
# (restore + publish Release, warnings as errors, framework-dependent
# into a single output dir), then aspnet noble-chiseled runtime as
# non-root `app` (UID 1654). No shell in runtime — exec-form
# ENTRYPOINT only.
#
# Forward-compat (Phase 5, T013): src/Shared is copied up front so the
# Dockerfile keeps working once Worker.csproj gains ProjectReferences
# (Shared) and NuGet packages (MassTransit, MailKit — fetched from the
# feed, no COPY needed). Only a reference pointing outside
# src/Services/Worker + src/Shared would require extending the COPY list.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Services/Worker/ ./src/Services/Worker/
COPY src/Shared/ ./src/Shared/

RUN dotnet restore ./src/Services/Worker/Worker.csproj \
 && dotnet publish ./src/Services/Worker/Worker.csproj \
      -c Release --no-restore \
      /p:TreatWarningsAsErrors=true /p:SelfContained=false \
      -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app

# Copy only the published output — no SDK, no sources, no obj/bin.
COPY --from=build /app/publish ./

# Run as the image's built-in non-root user (UID 1654). Chiseled images
# ship no shell, so ENTRYPOINT must stay in exec form.
USER app

# Worker hosts no HTTP today (Phase 5, T018 adds the /metrics endpoint);
# keep the same Kestrel port convention as Identity so Prometheus
# scraping (Phase 6) needs no special-casing.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["./Worker"]
