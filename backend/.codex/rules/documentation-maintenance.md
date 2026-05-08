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
