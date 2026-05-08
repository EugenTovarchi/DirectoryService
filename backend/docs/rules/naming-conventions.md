# Naming Conventions

Use this with [coding-style.md](coding-style.md).

## Projects and Folders

- Service projects use `<Service>.<Layer>` naming: `FileService.Web`, `DirectoryService.Application`, `SharedService.Core`.
- Commands live under `Commands/<Area>/<Action>`.
- Queries live under `Queries/<Area>/<Action>`.
- FileService endpoints live under `FileService.Core/Features`.
- Messaging consumers live under `Messaging` or event handler folders.

## Types

- Commands: `CreateDepartmentCommand`, `MoveDepartmentCommand`.
- Queries: `GetDepartmentChildrenQuery`.
- Handlers: `CreateDepartmentHandler`, `GetLocationsHandler`.
- Validators: `<CommandName>Validator`.
- Requests/responses: `<Action>Request`, `<Action>Response`.
- Options: `<Feature>Options`.
- Repositories: `I<Aggregate>Repository`, `<Aggregate>Repository`.
- Value object factories use `Create(...)`.
- Domain creation methods use `Create`, `CreateForUpload`, `CreateRoot`, `CreateChild`.

## Methods

- Async methods end with `Async`.
- Handler entrypoints are named `Handle`.
- FileService endpoint mapping methods are named `MapEndpoint`.
- Domain transition methods use verbs: `MarkUploaded`, `StartProcessing`, `CompleteProcessing`, `MoveTo`, `Delete`.

## Constants and Enums

- Follow existing local style.
- FileService domain enum values currently use uppercase values such as `VIDEO`, `UPLOADED`.
- Do not introduce a different enum naming style inside the same domain.

Related docs:

- [coding-style.md](coding-style.md)
- [domain-rules.md](domain-rules.md)
- [../architecture/how-to-add-service.md](../architecture/how-to-add-service.md)
