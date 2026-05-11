# syntax=docker/dockerfile:1.7

ARG DOTNET_SDK_VERSION=10.0.203
ARG DOTNET_RUNTIME_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS build
WORKDIR /src

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    NUGET_XMLDOC_MODE=skip

COPY --link ["Directory.Build.props", "."]
COPY --link ["Directory.Packages.props", "."]
COPY --link [".editorconfig", "."]
COPY --link ["BannedSymbols.txt", "."]

COPY --link ["backend/AuthService/AuthService.Web/AuthService.Web.csproj", "backend/AuthService/AuthService.Web/"]
COPY --link ["backend/AuthService/AuthService.Core/AuthService.Core.csproj", "backend/AuthService/AuthService.Core/"]
COPY --link ["backend/AuthService/AuthService.Contracts/AuthService.Contracts.csproj", "backend/AuthService/AuthService.Contracts/"]
COPY --link ["backend/AuthService/AuthService.Domain/AuthService.Domain.csproj", "backend/AuthService/AuthService.Domain/"]
COPY --link ["backend/AuthService/AuthService.Infrastructure.Postgres/AuthService.Infrastructure.Postgres.csproj", "backend/AuthService/AuthService.Infrastructure.Postgres/"]

RUN --mount=type=cache,id=nuget-global,target=/root/.nuget/packages,sharing=locked \
    --mount=type=secret,id=nuget_config \
    --mount=type=secret,id=nuget_username \
    --mount=type=secret,id=nuget_password \
    cp /run/secrets/nuget_config /tmp/nuget.config && \
    dotnet nuget update source github \
        --configfile /tmp/nuget.config \
        --username "$(cat /run/secrets/nuget_username)" \
        --password "$(cat /run/secrets/nuget_password)" \
        --store-password-in-clear-text && \
    dotnet restore "backend/AuthService/AuthService.Web/AuthService.Web.csproj" \
        --configfile /tmp/nuget.config && \
    rm /tmp/nuget.config

COPY --link ["backend/AuthService/", "backend/AuthService/"]

FROM build AS migration-bundle
WORKDIR /src

RUN --mount=type=cache,id=nuget-global,target=/root/.nuget/packages,sharing=locked \
    --mount=type=secret,id=nuget_config \
    --mount=type=secret,id=nuget_username \
    --mount=type=secret,id=nuget_password \
    cp /run/secrets/nuget_config /tmp/nuget.config && \
    dotnet nuget update source github \
        --configfile /tmp/nuget.config \
        --username "$(cat /run/secrets/nuget_username)" \
        --password "$(cat /run/secrets/nuget_password)" \
        --store-password-in-clear-text && \
    dotnet tool install dotnet-ef \
        --tool-path /tools \
        --version 9.0.10 \
        --configfile /tmp/nuget.config && \
    ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=auth_service;Username=postgres;Password=postgres" \
        /tools/dotnet-ef migrations bundle \
        --project "backend/AuthService/AuthService.Infrastructure.Postgres/AuthService.Infrastructure.Postgres.csproj" \
        --startup-project "backend/AuthService/AuthService.Web/AuthService.Web.csproj" \
        --context "AuthServiceDbContext" \
        --configuration Release \
        --output /app/migrations/efbundle \
        --force && \
    rm /tmp/nuget.config

FROM build AS publish
WORKDIR /src/backend/AuthService/AuthService.Web

RUN --mount=type=cache,id=nuget-global,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "AuthService.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION} AS app
WORKDIR /app

COPY --link --from=publish /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8003

USER app
EXPOSE 8003

ENTRYPOINT ["dotnet", "AuthService.Web.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION} AS migrator
WORKDIR /app

COPY --link --from=migration-bundle /app/migrations/efbundle /app/migrations/efbundle
COPY --link ["backend/AuthService/AuthService.Web/migrate.sh", "/app/migrate.sh"]

RUN chmod +x /app/migrate.sh /app/migrations/efbundle

USER app

ENTRYPOINT ["/app/migrate.sh"]
