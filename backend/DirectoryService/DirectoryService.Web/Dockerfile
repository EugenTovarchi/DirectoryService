# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копируем файлы проектов
COPY ["backend/DirectoryService/DirectoryService.Web/DirectoryService.Web.csproj", "backend/DirectoryService/DirectoryService.Web/"]
COPY ["backend/DirectoryService/DirectoryService.Application/DirectoryService.Application.csproj", "backend/DirectoryService/DirectoryService.Application/"]
COPY ["backend/DirectoryService/DirectoryService.Contracts/DirectoryService.Contracts.csproj", "backend/DirectoryService/DirectoryService.Contracts/"]
COPY ["backend/DirectoryService/DirectoryService.Domain/DirectoryService.Domain.csproj", "backend/DirectoryService/DirectoryService.Domain/"]
COPY ["backend/DirectoryService/DirectoryService.Infrastructure.Postgres/DirectoryService.Infrastructure.Postgres.csproj", "backend/DirectoryService/DirectoryService.Infrastructure.Postgres/"]
RUN dotnet restore "./backend/DirectoryService/DirectoryService.Web/DirectoryService.Web.csproj"

COPY . .
WORKDIR "/src/backend/DirectoryService/DirectoryService.Web"

# Восстанавливаем user secrets во время сборки
RUN dotnet restore "./DirectoryService.Web.csproj" /p:Configuration=$BUILD_CONFIGURATION

RUN dotnet build "./DirectoryService.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DirectoryService.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Копируем конфиги
COPY ["backend/DirectoryService/DirectoryService.Web/appsettings.json", "./"]
COPY ["backend/DirectoryService/DirectoryService.Web/appsettings.Development.json", "./"]
ENTRYPOINT ["dotnet", "DirectoryService.Web.dll"]