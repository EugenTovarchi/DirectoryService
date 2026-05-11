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

WORKDIR /src/backend/AuthService/AuthService.Web

RUN --mount=type=cache,id=nuget-global,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "AuthService.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION} AS final
WORKDIR /app

COPY --link --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8003

USER app
EXPOSE 8003

ENTRYPOINT ["dotnet", "AuthService.Web.dll"]
