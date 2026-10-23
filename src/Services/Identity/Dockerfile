# Identity.Api — multi-stage chiseled non-root build (Phase 4, T010; US2 / SC-002).
#
# Build context MUST be the repo root (see deployments/docker-compose.yml
# `identity.build.context: ..`), so all COPY source paths below are
# relative to the repo root, not to this Dockerfile's directory.
#
# Stage 1 "build": full SDK restores and publishes Release with warnings
# as errors (TreatWarningsAsErrors is also repo-global via
# Directory.Build.props; repeated here so the image build stays strict
# even if that file ever changes). Framework-dependent publish
# (SelfContained=false) into a single output dir. The native apphost
# (`Identity.Api`) is the entrypoint — chiseled images have no shell,
# so only exec-form ENTRYPOINT is allowed.
# Stage 2 "runtime": aspnet noble-chiseled, non-root `app` (UID 1654).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central build wiring first so SDK resolution works before sources land.
COPY Directory.Build.props Directory.Packages.props ./

# Full transitive closure of Identity.Api project references
# (Identity.Api -> Application -> {Domain, Shared}; -> Infrastructure).
# src/Shared is included because Phase 5+ may add more Shared references;
# if a future ProjectReference ever points outside these trees, extend
# this COPY list (see report note for T013 coupling).
COPY src/Services/Identity/Identity.Api/ ./src/Services/Identity/Identity.Api/
COPY src/Services/Identity/Identity.Application/ ./src/Services/Identity/Identity.Application/
COPY src/Services/Identity/Identity.Domain/ ./src/Services/Identity/Identity.Domain/
COPY src/Services/Identity/Identity.Infrastructure/ ./src/Services/Identity/Identity.Infrastructure/
COPY src/Shared/ ./src/Shared/

RUN dotnet restore ./src/Services/Identity/Identity.Api/Identity.Api.csproj \
 && dotnet publish ./src/Services/Identity/Identity.Api/Identity.Api.csproj \
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

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["./Identity.Api"]
