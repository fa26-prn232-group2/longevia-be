# Recommendation API - gRPC server nội bộ (không đi qua Nginx).
# Build context là thư mục gốc repository:
#   docker compose -f deployments/docker-compose.yml build recommendation
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Chỉ copy những gì cần để restore: props chung và các file csproj. Nhờ vậy
# lớp restore không bị invalidate mỗi khi source code thay đổi.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Shared/Longevia.Shared.csproj ./src/Shared/
COPY src/Services/Recommendation/Recommendation.Domain/Recommendation.Domain.csproj ./src/Services/Recommendation/Recommendation.Domain/
COPY src/Services/Recommendation/Recommendation.Application/Recommendation.Application.csproj ./src/Services/Recommendation/Recommendation.Application/
COPY src/Services/Recommendation/Recommendation.Infrastructure/Recommendation.Infrastructure.csproj ./src/Services/Recommendation/Recommendation.Infrastructure/
COPY src/Services/Recommendation/Recommendation.Api/Recommendation.Api.csproj ./src/Services/Recommendation/Recommendation.Api/

RUN dotnet restore src/Services/Recommendation/Recommendation.Api/Recommendation.Api.csproj

# Recommendation.Api.csproj trỏ tới ../../../../contracts/recommendation/v1/recommendation.proto,
# nên build context phải chứa cả thư mục contracts/ thì codegen gRPC mới chạy được.
COPY contracts/ ./contracts/
COPY src/Shared/ ./src/Shared/
COPY src/Services/Recommendation/ ./src/Services/Recommendation/

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
