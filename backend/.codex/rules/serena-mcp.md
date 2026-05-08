---
globs: ["backend/**/*.cs", "backend/AGENTS.md", "backend/.codex/rules/*.md"]
---

# Serena MCP

Use Serena MCP for semantic code navigation and safe refactoring in this repository.

## Active Projects

Serena is onboarded for these separate projects:

- `FileService`
- `DirectoryService`
- `SharedService`

Activate the project that owns the code being inspected or changed:

- Activate `FileService` for file/media upload, S3/MinIO, video processing, FileService contracts, and FileService tests.
- Activate `DirectoryService` for departments, locations, positions, hierarchy, DirectoryService contracts, messaging consumers, and DirectoryService tests.
- Activate `SharedService` for shared kernel/framework/core abstractions and NuGet package changes consumed by FileService or DirectoryService.

Do not activate or use `IntegrationEvents`. That project was removed and must be ignored.

## Startup Use

- After activating a Serena project, call `check_onboarding_performed`.
- If onboarding is missing, perform onboarding before project work.
- Read Serena memories when they are relevant to the current task, especially project overview, style/conventions, suggested commands, public API notes, package/versioning notes, and task completion checklists.

## Preferred Serena Tools

Prefer Serena tools before broad text search for:

- finding declarations
- finding implementations
- finding references
- symbol overview
- safe symbol-level edits and refactors

Use broad text search such as `rg` only when Serena is insufficient, unavailable for the target file type, or when searching non-code text/configuration.

## SharedService Care

`SharedService` is a shared NuGet/foundation library, not an application service. Changes there can affect both `FileService` and `DirectoryService`.

For `SharedService` changes:

- Keep APIs service-neutral and avoid FileService/DirectoryService business concepts.
- Treat public types, method signatures, namespaces, package IDs, response envelopes, error/failure behavior, endpoint helpers, middleware behavior, and messaging contracts as compatibility-sensitive.
- Prefer additive changes.
- If a public package API or behavior changes, consider package version updates and verify affected consumers.
