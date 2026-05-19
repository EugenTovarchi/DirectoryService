---
globs: ["backend/**/Domain/**", "backend/**/Messaging/**", "backend/**/Application/**", "backend/**/WolverineConfiguration.cs", "AGENTS.md"]
---

# Documentation Maintenance

Primary docs live under `docs/`. Prefer updating focused docs there instead of duplicating detail in `AGENTS.md`.

When modifying domain entities, value objects, messaging configuration, cross-service contracts, or major features:

1. Check whether root `AGENTS.md` needs an update.
2. Check service-level documentation if it exists.
3. Update `docs/services/*` if service ownership, flows, dependencies, or public behavior changed.
4. Update `docs/patterns/*` if reusable implementation patterns changed.
5. Update `docs/rules/*` if conventions changed.
6. Update messaging docs if exchanges, queues, routing keys, or event contracts change.
7. Update local run instructions if Docker/env settings change.

This applies after implementation, not during exploratory reading.

## Service Pre-Commit Gate

For commits that include service changes, run the documentation check only when the final diff is ready and before creating the commit.

- Review the final service diff.
- Update the affected `docs/services/*` document as the main development documentation when behavior, contracts, endpoints, flows, configuration, verification, or backlog changed.
- Update the existing service learning notes section when the change adds learning context. Keep the note short: plan, what was done, and how the result affects the service.
- Do not add commit hashes, commit names, branch names, or long changelog entries to service documentation.
- If no documentation update is needed, report why before committing.
