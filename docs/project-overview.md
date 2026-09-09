# TestShield AI — Project Overview

Use this file as project context for Cursor and for Gate 1 work. Do not treat it as application code.

## Project name

TestShield AI

## Problem statement

API changes often break consumers without a clear, repeatable check. Teams may have an OpenAPI/Swagger spec, but they lack a simple way to generate tests from that contract, run them against a live base URL, and see whether responses still match the spec and the last known-good run. Failures are hard to explain, and AI is not a substitute for a deterministic pass/fail.

## Goal

Build an AI-powered API regression and contract protection tool. TestShield AI ingests an OpenAPI/Swagger spec, generates and executes API tests, validates responses against the contract, detects regressions against a baseline, and reports results. AI may explain failures or suggest extra cases; it must never decide pass or fail.

## Target users

- Backend and API developers protecting a service before release
- QA engineers running contract and regression checks
- Capstone reviewers who need a demoable .NET 8 + Angular flow

## Gate 1 scope

- ASP.NET Core 8 Web API + Angular SPA + SQLite
- Import OpenAPI 3 / Swagger and list operations
- Deterministic test generation (happy path per operation; at least one negative case where a request body applies)
- HTTP execution against a configured base URL, with timeouts
- Contract validation: documented status codes and response body vs JSON Schema
- Persist the last run; compare a later run to the last passing baseline
- JSON and on-screen report (counts, failing path, schema error)
- TestShield’s own API documented with Swagger
- Engine unit tests for ingest and validation
- `IAiAssistant` with a no-op provider; optional Azure OpenAI / OpenAI explanation if a key is present

## Out-of-scope items

- Node.js, React, Fastify, or TypeScript as the primary application stack
- Azure DevOps pipeline as a Gate 1 quality gate (add after the engine is stable)
- CLI / GitHub Action
- AI-generated tests as the primary suite
- Pact, GraphQL, async APIs
- Multi-user auth, Postgres, message bus
- Full auth-matrix testing of target APIs

## Approved technology stack

| Area | Choice |
|---|---|
| Backend | .NET 8 (ASP.NET Core Web API) |
| Frontend | Angular |
| API standard | Swagger / OpenAPI |
| Database | SQLite |
| AI | Azure OpenAI / OpenAI API (optional adapter) |
| CI/CD | Azure DevOps (after Gate 1) |
| Development | Cursor |

Solution file: `TestShieldAI.sln` at the repo root. .NET project creation waits until the .NET 8 SDK is available.

## High-level workflow

```text
OpenAPI/Swagger Import
  -> Test Generation
  -> Test Execution
  -> Contract Validation
  -> Regression Detection
  -> Report
```

1. **OpenAPI/Swagger Import** — Load a spec; extract operations (method, path, parameters, schemas).
2. **Test Generation** — Create deterministic cases first; AI extras are optional and must match the same test-case shape.
3. **Test Execution** — Send HTTP requests to the project base URL; capture status, headers, body, duration.
4. **Contract Validation** — Check status against the spec and body against JSON Schema. This is the source of pass/fail.
5. **Regression Detection** — Compare this run to the last passing baseline (removed fields, type changes, new required fields).
6. **Report** — JSON as source of truth; Angular shows counts and failures; optional AI explanation after validation.
