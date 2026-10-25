FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY contracts/identity/ ./contracts/identity/
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
EXPOSE 8080
ENTRYPOINT ["./Progress.Api"]
