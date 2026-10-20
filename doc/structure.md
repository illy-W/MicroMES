# MicroMES - Structure

> Stub: fill in as patterns emerge.

## Monorepo layout

See `AGENTS.md` for the project table and dependency direction.

## Backend patterns

- Controller actions: name `{Verb}{Resource}{Action?}` in PascalCase (e.g. `GetOrders`, `CreateOrder`, `CancelOrderItem`) and set it explicitly in the HTTP verb attribute: `[HttpGet(Name = "GetOrders")]`. The name becomes the OpenAPI `operationId` (and the generated client function name).
- Feature structure: `libs/api-features/<Feature>Feature/{Controllers,Models,Services,Repositories}` plus a `Registrar.cs` that wires the feature (see `MaterialFeature`). Entities and `AppDbContext` live in `libs/api-core/Persistence`.
- Flow: Controller -> Service -> Repository -> `AppDbContext`. The `DbContext` is used only inside repositories.
- Services return `Result` / `Result<T>` (`illyW.Framework.Core.ResultPattern`); call `Fail()` before `AddError()`; no exceptions reach the controller.
- Repositories: `IXxxRepository : IGenericRepository<T, int>`, no extra methods unless needed, registered with `AddRepository` in the feature `Registrar.cs`.
- Request/response models are separate from entities, mapped by hand (no AutoMapper, no CQRS/Silverback). Primary keys are always `int`. Deletion is soft (`IsActive`), never `DeleteAsync`.
- `Program.cs`: `JsonStringEnumConverter`, strict `NumberHandling`, 422 on invalid model state.
- Domain vocabulary follows ISA-95 in reduced form: see `doc/glossary.md`.
- Migrations: `dotnet tool restore` (`dotnet-tools.json`, dotnet-ef), then `dotnet ef migrations add <Name> --project libs/api-core --startup-project apps/api --output-dir Persistence/Migrations`. They are applied at startup only in Development (skipped when generating OpenAPI).
- Local database: PostgreSQL via `docker-compose.yml` (container `micromes-db`). Start/stop with `npx nx run Api:db-up` / `Api:db-down`. Connection string: `ConnectionStrings:Default` in `appsettings.Development.json`.

## Frontend patterns

- CRUD example: `apps/web/src/app/features/materials` (signal store service, TanStack table, dialog form, alert-dialog confirmation, generated client). Reuse it as the reference for new CRUD pages.

### Generated API client

- `orval.config.ts` (root) reads `openapi/Api.json` and generates into `libs/shared/api-client/src/generated/{services,model}`: one service per OpenAPI tag (kebab-case file names), URLs relative (`/api/...`).
- Flow: `npx nx run api-client:generate-api` builds `Api:build:release:doc` (writes `openapi/Api.json`), then runs Orval. `generate-api:no-build` skips the build.
- Never edit `generated/**` by hand; it is excluded from lint and prettier. Change the API and regenerate.
- Import via aliases: `@micromes/api-client/models` and `@micromes/api-client/services/<name>`. `web` provides `provideHttpClient()`.
- `web:serve` (development) proxies `/api` to `http://localhost:5190` (`apps/web/proxy.config.local.json`) and depends on `Api:serve`.
- Current state: no endpoints yet (`paths` empty), so `generated/` only contains an empty model index.

### UI Styling Conventions

- Stack: Tailwind v4 (via `@tailwindcss/postcss`; the app needs its own `apps/web/.postcssrc.json`) and spartan-ng. `apps/web/src/styles.scss` imports the Tailwind layers and `@spartan-ng/brain/hlm-tailwind-preset.css`, which defines the semantic tokens (light, and `:root.dark`).
- Priority: spartan helm components (`@spartan-ng/helm/*`) -> semantic token classes (`bg-primary`, `text-muted-foreground`, `border`) -> `styles.scss` base -> Tailwind layout utilities.
- No hardcoded color classes and no `dark:` variants; use tokens.
- Icons: `@ng-icons/lucide` (`NgIcon` + `provideIcons`).
- Tables: `@tanstack/angular-table` is installed; no table component yet.

### UI library (`libs/ui`)

- One buildable library per component (`libs/ui/<name>`), config in `components.json` (style `nova`, alias `@spartan-ng/helm`). Today: `button`, `utils`.
- Add a component: `npx nx g @spartan-ng/cli:ui <name> --no-interactive`, then `npx nx lint <name> --fix` (peerDependencies) and prettier on the JSON files the generator rewrote (`nx.json`, `tsconfig.base.json`). Check the aliases in `tsconfig.base.json` keep the `./` prefix.
- Never hand-write components in `libs/ui`.

### Tables

- Same markup as the Spartan dashboard example (`table-section.ts` in spartan-ng/spartan), using only helm directives: `<div class="max-h-[700px] w-full overflow-auto rounded-md border"><div hlmTableContainer><table hlmTable>` with `hlmTHead` (`class="bg-muted sticky top-0 z-10"`), `hlmTBody`, `hlmTr`, `hlmTh`, `hlmTd`. No arbitrary-variant style hacks (`[&_th]`), no custom table wrapper.
- Type/status values are outline badges. Row actions: a ghost `icon-sm` `lucideEllipsis` button with `[hlmDropdownMenuTrigger]` and an `<ng-template #menu>` holding `hlm-dropdown-menu`; the items must be declared in the same component template as the `ng-template` (otherwise NG0201 `cdk-menu-stack`). Pattern: a small per-feature component like `features/materials/material-row-actions.component.ts`.
- The only shared component is `shared/components/table-paginator.component.ts` (copied from ptconnect-nx, Italian strings, TanStack v9 API via `table().atoms.pagination`, structural `PaginatedTable` input, page sizes 5/10/20): footer right side; the left side shows the row count. `@tanstack/angular-table` is on v9 (`injectTable` + `tableFeatures`, as in the upstream example); a table needs `rowPaginationFeature` + `paginatedRowModel` to use the paginator.
- Tree tables (e.g. BOM): same helm table with TanStack row expanding (`rowExpandingFeature`, `createExpandedRowModel`, `getSubRows`), chevron button with `aria-expanded`, indentation by `row.depth`. Reference: `features/bom` (page `/materials/:id/bom`, opened from the Materials row menu; it will become a tab of a material detail page).
- Reference implementation: `features/materials`.

### Layouts (`apps/web/src/app/core/layouts`)

- `blank`: only a `router-outlet`, for pages without shell. Currently unused.
- `full`: sidebar + header + `router-outlet`. Menu entries live in `full/sidebar-data.ts` (`sidebarItems`).
- `components`: `sidebar`, `nav-main`, `header`.
- `models`: `NavItem` (`id`, `displayName`, `iconName`, `route`).
- Assign a layout to a route: in `app.routes.ts` use a parent route with `loadComponent` of the layout and the pages as `children` (see the `full` parent route with `dashboard`).
- Add a menu item: add a `NavItem` to `full/sidebar-data.ts` and register its icon in `provideIcons` of `components/nav-main/nav-main.component.ts`.
- Pages live in `features/<name>`. `features/dashboard` is a placeholder page using `hlm-empty`.
- UI text is currently plain Italian strings (no i18n yet).

## Naming conventions

## Common commands

See `AGENTS.md`.
