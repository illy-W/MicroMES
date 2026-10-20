# MicroMES - Structure

> Stub: fill in as patterns emerge.

## Monorepo layout

See `AGENTS.md` for the project table and dependency direction.

## Backend patterns

- Controller actions: name `{Verb}{Resource}{Action?}` in PascalCase (e.g. `GetOrders`, `CreateOrder`, `CancelOrderItem`) and set it explicitly in the HTTP verb attribute: `[HttpGet(Name = "GetOrders")]`. The name becomes the OpenAPI `operationId` (and the generated client function name).

## Frontend patterns

### Generated API client

- `orval.config.ts` (root) reads `openapi/Api.json` and generates into `libs/shared/api-client/src/generated/{services,model}`: one service per OpenAPI tag (kebab-case file names), URLs relative (`/api/...`).
- Flow: `npx nx run api-client:generate-api` builds `Api:build:release:doc` (writes `openapi/Api.json`), then runs Orval. `generate-api:no-build` skips the build.
- Never edit `generated/**` by hand; it is excluded from lint and prettier. Change the API and regenerate.
- Import via aliases: `@micromes/api-client/models` and `@micromes/api-client/services/<name>`. `web` provides `provideHttpClient()`.
- `web:serve` (development) proxies `/api` to `http://localhost:5190` (`apps/web/proxy.config.local.json`) and depends on `Api:serve`.
- Current state: no endpoints yet (`paths` empty), so `generated/` only contains an empty model index.

## Naming conventions

## Common commands

See `AGENTS.md`.
