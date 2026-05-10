---
name: add-api-endpoint
description: Use when adding a new REST endpoint to FileService or DirectoryService.
---

# Add API Endpoint

## Goal

Add a new endpoint using existing project conventions.

## Steps

1. Identify the owning service.
2. Find similar endpoint in the same service.
3. Add or update request/response DTOs in Contracts.
4. Add validator if request has user input.
5. Add command/query/handler according to existing pattern.
6. Keep controller thin.
7. Add structured logs in handler.
8. Pass CancellationToken.
9. Add tests if the behavior is meaningful.

## Rules

- Do not put business logic in controller.
- Do not throw for normal business errors.
- Use existing `Result<T, Error>` conventions.
- Use value objects/factories for validated domain data.
- Do not return infrastructure models directly.
- Do not expose internal exception details in responses.

## Verification

```bash
dotnet build
dotnet test
```
