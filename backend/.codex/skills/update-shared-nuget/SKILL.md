---
name: update-shared-nuget
description: Use when SharedService NuGet package changes and FileService/DirectoryService must consume the new version.
---

# Update SharedService NuGet

## Steps

1. Modify SharedService source.
2. Increase package version.
3. Build and pack:

```bash
dotnet pack -c Release
```

4. Push package:

```bash
dotnet nuget push <package>.nupkg --source github --api-key <token>
```

5. Update consuming services:

```bash
dotnet add package IstredDev.Framework --version <version>
```

or edit `.csproj`:

```xml
<PackageReference Include="IstredDev.Framework" Version="<version>" />
```

6. Restore/build:

```bash
dotnet restore
dotnet build
```

7. If Docker builds consume NuGet, rebuild containers:

```bash
docker compose -f docker-compose-dev.yml up -d --build
```

## Important

- NuGet push makes the package available.
- Consumers do not update automatically.
- Update `PackageReference` explicitly.
- Push SharedService source code to Git too, so package source is not lost.
