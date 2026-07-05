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

4. Copy the committed source configuration, add a Deploy Token only to the ignored local copy, and push the package manually:

```powershell
Copy-Item nuget.config nuget.local.config

dotnet nuget update source gitlab-sharedservice `
    --configfile nuget.local.config `
    --username $env:NUGET_USERNAME `
    --password $env:NUGET_PASSWORD `
    --store-password-in-clear-text

dotnet nuget push <package>.nupkg `
    --source gitlab-sharedservice `
    --configfile nuget.local.config
```

The shared GitLab registry stores `IstredDev.Core`, `IstredDev.Framework`,
`IstredDev.SharedKernel`, and `IstredDev.FileService.Contracts`.

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

- Package publication is manual; GitLab CI release tags are not required.
- Use a Deploy Token with `write_package_registry` for push and `read_package_registry` for restore.
- Never add credentials to the committed root `nuget.config`; use a temporary or otherwise ignored local config for authenticated commands.
- Consumers do not update automatically.
- Update `PackageReference` explicitly.
- Push SharedService source code to Git too, so package source is not lost.
