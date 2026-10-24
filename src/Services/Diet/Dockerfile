FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Shared/ ./src/Shared/
COPY src/Services/Diet/Diet.Domain/ ./src/Services/Diet/Diet.Domain/
COPY src/Services/Diet/Diet.Application/ ./src/Services/Diet/Diet.Application/
COPY src/Services/Diet/Diet.Infrastructure/ ./src/Services/Diet/Diet.Infrastructure/
COPY src/Services/Diet/Diet.Api/ ./src/Services/Diet/Diet.Api/
RUN dotnet publish src/Services/Diet/Diet.Api/Diet.Api.csproj -c Release -o /app/publish /p:SelfContained=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish ./
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["./Diet.Api"]
