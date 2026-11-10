FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
# This COPY list must cover every `Protobuf Include` of the projects copied
# below: Progress.Infrastructure -> identity, Progress.Api -> progress,
# Progress.Application -> recommendation + diet. Adding a proto to a project
# without adding its directory here breaks the image build, not the local build.
COPY contracts/identity/ ./contracts/identity/
COPY contracts/progress/ ./contracts/progress/
COPY contracts/recommendation/ ./contracts/recommendation/
COPY contracts/diet/ ./contracts/diet/
COPY src/Shared/ ./src/Shared/
COPY src/Services/Progress/Progress.Domain/ ./src/Services/Progress/Progress.Domain/
COPY src/Services/Progress/Progress.Application/ ./src/Services/Progress/Progress.Application/
COPY src/Services/Progress/Progress.Infrastructure/ ./src/Services/Progress/Progress.Infrastructure/
COPY src/Services/Progress/Progress.Api/ ./src/Services/Progress/Progress.Api/
RUN dotnet publish src/Services/Progress/Progress.Api/Progress.Api.csproj -c Release -o /app/publish /p:SelfContained=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish ./
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080 50051
ENTRYPOINT ["./Progress.Api"]
