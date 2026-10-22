# Recommendation API - gRPC server nội bộ (không đi qua Nginx).
# Build context là thư mục gốc repository:
#   docker compose -f deployments/docker-compose.yml build recommendation
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Chỉ copy những gì cần để restore: props chung, thư viện shared và project của service.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Shared/ ./src/Shared/
COPY src/Services/Recommendation/ ./src/Services/Recommendation/

RUN dotnet restore src/Services/Recommendation/Recommendation.Api/Recommendation.Api.csproj
RUN dotnet publish src/Services/Recommendation/Recommendation.Api/Recommendation.Api.csproj \
        --configuration Release \
        --output /app \
        --no-restore \
        /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./

# Endpoint do Kestrel cấu hình trong Program.cs: 50051 (gRPC, HTTP/2 h2c) và
# 8080 (/health, HTTP/1.1).
EXPOSE 50051 8080
USER app

ENTRYPOINT ["dotnet", "Recommendation.Api.dll"]
