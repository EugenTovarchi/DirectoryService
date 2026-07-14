# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 50051

# ffmpeg/ffprobe выполняют HLS-транскодирование и извлечение метаданных внутри контейнера.
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt/lists,sharing=locked \
    sed -i 's|http://deb.debian.org|https://deb.debian.org|g' \
        /etc/apt/sources.list.d/debian.sources \
    && apt-get -o Acquire::Retries=5 -o Acquire::https::Timeout=30 update \
    && for attempt in 1 2 3; do \
        apt-get -o Acquire::Retries=5 -o Acquire::https::Timeout=30 \
            --fix-broken install -y \
        && apt-get -o Acquire::Retries=5 -o Acquire::https::Timeout=30 \
            install -y --no-install-recommends ffmpeg \
        && break; \
        if [ "$attempt" = 3 ]; then exit 1; fi; \
        sleep $((attempt * 5)); \
    done

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
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
    --mount=type=secret,id=nuget_config \
    --mount=type=secret,id=nuget_username \
    --mount=type=secret,id=nuget_password \
    cp /run/secrets/nuget_config /tmp/nuget.config && \
    dotnet nuget update source gitlab-sharedservice \
        --configfile /tmp/nuget.config \
        --username "$(cat /run/secrets/nuget_username)" \
        --password "$(cat /run/secrets/nuget_password)" \
        --store-password-in-clear-text && \
    dotnet restore "./backend/FileService/FileService.Web/FileService.Web.csproj" \
        --configfile /tmp/nuget.config && \
    rm /tmp/nuget.config

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
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FileService.Web.dll"]
