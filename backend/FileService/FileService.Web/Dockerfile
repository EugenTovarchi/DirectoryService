# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["nuget.config", "."]
COPY [".editorconfig", "."]
COPY ["BannedSymbols.txt", "."]

COPY ["backend/FileService/FileService.Web/FileService.Web.csproj", "backend/FileService/FileService.Web/"]
COPY ["backend/FileService/FileService.Core/FileService.Core.csproj", "backend/FileService/FileService.Core/"]
COPY ["backend/FileService/FileService.Contracts/FileService.Contracts.csproj", "backend/FileService/FileService.Contracts/"]
COPY ["backend/FileService/FileService.Domain/FileService.Domain.csproj", "backend/FileService/FileService.Domain/"]
COPY ["backend/FileService/FileService.Infrastructure.Postgres/FileService.Infrastructure.Postgres.csproj", "backend/FileService/FileService.Infrastructure.Postgres/"]
COPY ["backend/FileService/FileService.Infrastructure.S3/FileService.Infrastructure.S3.csproj", "backend/FileService/FileService.Infrastructure.S3/"]
COPY ["backend/FileService/FileService.VideoProcessing/FileService.VideoProcessing.csproj", "backend/FileService/FileService.VideoProcessing/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "./backend/FileService/FileService.Web/FileService.Web.csproj"

COPY . .
WORKDIR "/src/backend/FileService/FileService.Web"

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "./FileService.Web.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FileService.Web.dll"]
