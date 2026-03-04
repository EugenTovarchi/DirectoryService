FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /src

ARG NUGET_USERNAME
ARG NUGET_PASSWORD

ENV NUGET_USERNAME=$NUGET_USERNAME
ENV NUGET_PASSWORD=$NUGET_PASSWORD

RUN dotnet tool install --global dotnet-watch
ENV PATH="${PATH}:/root/.dotnet/tools"

# --- shared configs ---
COPY nuget.config .
COPY backend/FileService/Directory.Build.props .
COPY backend/FileService/Directory.Packages.props .
COPY .editorconfig .

# --- csproj ---
COPY backend/FileService/FileService.Web/FileService.Web.csproj backend/FileService/FileService.Web/
COPY backend/FileService/FileService.Core/FileService.Core.csproj backend/FileService/FileService.Core/
COPY backend/FileService/FileService.Contracts/FileService.Contracts.csproj backend/FileService/FileService.Contracts/
COPY backend/FileService/FileService.Domain/FileService.Domain.csproj backend/FileService/FileService.Domain/
COPY backend/FileService/FileService.Infrastructure.Postgres/FileService.Infrastructure.Postgres.csproj backend/FileService/FileService.Infrastructure.Postgres/
COPY backend/FileService/FileService.Infrastructure.S3/FileService.Infrastructure.S3.csproj backend/FileService/FileService.Infrastructure.S3/

RUN dotnet restore backend/FileService/FileService.Web/FileService.Web.csproj

COPY . .

WORKDIR /src/backend/FileService/FileService.Web

ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile", "--urls", "http://0.0.0.0:8080"]