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

### Routing editor (`features/routing`)

- Page `/materials/:id/routing`, opened from the Materials row menu. Diagram built with `ng-diagram` (exact version in `package.json`): custom Angular node component (ng-diagram nodes are unstyled), styles imported in `apps/web/src/styles.scss` with the `--ngd-*` variables mapped to the semantic tokens.
- The whole graph is saved with one `PUT /api/materials/{id}/routing` (steps keyed by `key`, existing steps keep their id). Client-side validation mirrors the backend rules (acyclic, connected, one final step, unique operation numbers); the backend is the authority.
- Process segments are managed in `features/process-segments` (same structure as `features/materials`).

### Local sample data (Development)

- `MasterDataDevelopmentSeeder` (`libs/api-features/SampleDataFeature`) fills an empty database at startup with a small steel and oak furniture workshop: materials, multilevel BOM, process segments and four routings (the chair has parallel branches). It never runs outside Development or when `Materials` already has rows.
- To reset: stop the dev server, `docker compose down -v`, then `npx nx run Api:db-up` and restart the API.

### Production orders and work orders

- `ProductionOrderFeature` (backend): `ProductionOrderService` and `WorkOrderService`, pure `ProductionOrderPlanner` for the release, `ProductionOrderStatusUpdater` firing `Start`/`Complete` on the order after work order commands.
- **Release = snapshot**: copies routing steps (effective times), dependencies and BOM requirements into work orders; creates child orders for `Intermediate`/`FinishedGood` components that have a routing (depth cap 10, cyclic BOM -> 422). Changing routing or BOM later does not affect released orders.
- **State machines (`Stateless` 5.20.1)** in `MachineStates/`, same pattern as `ptconnect-nx` (state read/written on the entity, `TimeProvider`). Status changes happen only through the machines. Internal enum with abstract superstates (`Prepared`, `Open`, `Closed`) mapped to the persisted leaf-state enums; effects (`OnEntry`) only on leaf states. Invalid transitions return 409 with an Italian message; `allowedActions` (from `GetPermittedTriggers`) is the single source for the UI commands.
- Work order: `Waiting` -> (`PredecessorsEnded`) -> `Ready` -> `Start` -> `Running` <-> `Interrupted` (`Pause`/`Resume`); `Stop` -> `Ended`; `Abort` from any non-terminal state -> `Aborted`.
- Production order: `Planned` -> `Release` -> `Released` -> (first `Start`) -> `InProgress` -> (`Complete`) -> `Completed`; `Cancel` from `Planned` or from `Released` with no started work order; `Abort` from `Released`/`InProgress`. Child orders follow the parent (no own actions).
- Both machines export their graph: `ExportMermaid()` / `ExportDot()` (Stateless `MermaidGraph` / `UmlDotGraph`).
- Frontend: `features/production-orders` (list, detail with children tree) and `features/work-orders` (operational list and commands). Pages show only the actions in `allowedActions`.
- Sample data: `ProductionOrderDevelopmentSeeder` (Development only, empty `ProductionOrders` table, uses the services). To recreate: stop the dev server, truncate the order tables (`ProductionOrders` with `restart identity cascade`) or reset the DB (see "Local sample data") and restart the API.

### Order overview (`features/production-orders/overview`)

- Endpoint `GET /api/production-orders/{id}/overview` (`ProductionOrderOverviewService`, read-only, repositories injected directly): the whole family of the order (order + descendants) flat: `orders`, `workOrders`, `dependencies` (including child last step -> parent start steps), `summary` (counters per status, minimum duration, `criticalPathWorkOrderIds`). The requested order has `parentOrderId = null` even if it is a child; a `Planned` order answers 200 with empty work orders.
- Tab "Panoramica" (first tab of the order detail): read-only ng-diagram. Work orders are node cards; child orders (BOM semi-finished) are **group nodes** (`isGroup`, nested by `groupId` for multilevel BOM); cross-order edges are dashed; the longest path is highlighted (toggle). Selecting a node/group shows a side panel with details and links to `/work-orders?order=<id>` / the child order; no commands.
- Layout: **ELK.js** (`elkjs@0.11.1`, imported only dynamically from `elkjs/lib/elk.bundled.js`: plain `elkjs` fails with `Could not resolve "web-worker"`, a static import breaks the 1 MB initial budget). Groups are sent to ELK as **nested graphs** (`elk.hierarchyHandling: INCLUDE_CHILDREN`), child positions are relative to the group (add the group's global position), group `size` is written from ELK's result because groups do not grow. Positions are never stored. "Riordina" re-runs the layout, "Adatta" fits the view (capped at 1:1).
- "Aggiorna" keeps viewport and selection when only statuses/quantities change (`updateNodes`/`updateEdges` on `data`); it re-layouts only when the set of nodes/edges changes. The model is created once (`initializeModel`), never per tab switch; the inactive diagram is hidden with `invisible`/`inert`, never `display:none`.
- ng-diagram skills (`.agents/skills/ng-diagram-*`) are the guide for diagram work; confirm API names in `node_modules/ng-diagram/ng-diagram.d.ts`.

### BOM-step link

- `BomItem.ConsumedInStepId` (nullable FK to `RoutingStep`, `SetNull`): null means the first start step. Editing happens only on the BOM page; the routing page and the order overview show the components per step read-only.
- Release: `WorkOrderMaterial` goes on the work order of the effective step; a child order's last work order gates only the consuming work order. Routing `PUT` resets the links of deleted steps in the same transaction and returns `unassignedComponents`; the editor asks confirmation before saving.

### Scrap management

- `ScrapFeature`: catalog (`/api/scrap-reasons`, soft-delete, optional phase filter) and report (`GET /api/scrap/report?from&to`, aggregations on the backend, always with a *Senza motivo* row). Closing a session (`scrapEntries`, optional) and corrections (`POST /api/work-orders/{id}/responses/{responseId}/corrections`) live in `ProductionOrderFeature`, one transaction, shared validation (sum = scrap, active reason valid for the phase).
- Menu: sidebar sections (`sidebarSections`): Dashboard, *Produzione* (orders, work orders, scrap), *Anagrafiche* (materials, process segments, scrap reasons). BOM and routing stay under the material page.

### Equipment

- `EquipmentFeature`: `/api/equipment` (soft-delete, filters `includeInactive`, `search`, `processSegmentId`). The machine <-> process segment link is edited from both sides with the same linker (`EquipmentSegmentLinker`): machine `processSegmentIds` always replaces the set; segment `equipmentIds` replaces it only when present (omitted = unchanged). Only active items can be newly linked; existing links to deactivated items are kept. `AddEquipmentFeature()` must be registered together with `AddProcessSegmentFeature()`.
- Web: `features/equipment`, shared `LinkedItemsPickerComponent` (checkboxes with search) also used in the process segment dialog; menu section *Anagrafiche*.

### Equipment assignment

- Rules in `EquipmentSelector` (pure `Pick`/`Check`): a machine must be active, existing and linked to the work order's process segment; a phase without machines accepts none. Release assigns the segment's active default machine, else the only active linked machine, else none. `PUT /api/work-orders/{id}/equipment` (Waiting/Ready/Interrupted) is not a state-machine transition.
- `POST start/resume` accept an optional body `OpenSessionRequest {equipmentId}`: without body start uses the assigned machine and resume the last session's; required only if the phase has active linked machines; the work order's assignment follows the machine used; the session keeps a code/name snapshot. Corrections may change the machine (trace in the correction record).
- Web: one dialog (`work-order-machine-dialog`) for start, resume and assign; shown only when the phase has machines; "in uso" is computed client side from the Running work orders (no endpoint).

### Work calendars

- `CalendarFeature`: `WorkingTimeCalculator` is pure (no database) and works on an availability abstraction (`IAvailabilitySource` / `EffectiveAvailability` built by `AvailabilityResolver` from the calendar slots plus calendar and equipment exceptions); it handles the company time zone, DST (a slot inside the non-existent spring hour counts 0) and a 731-day horizon with an explicit error. Efficiency is applied only in calculations (preview, estimate), never to stored planned minutes.
- The migration creates the default calendar `STANDARD` (08-12/13-17 Mon-Fri) if missing; a filtered unique index keeps exactly one default. `CalendarDevelopmentSeeder` runs in its own scope after the master data and fills `DOPPIO-TURNO`, Italian holidays (current and next year, Easter computed), a summer closure and machine examples.
- `estimatedEnd` (order detail and overview summary) comes from `ProductionEndEstimator`: open work orders only, each starts when its open predecessors end (not before now), running ones count the remaining minutes, machine calendar and efficiency applied, the latest end wins. It ignores capacity conflicts and is never stored.
- Web: `features/calendars` (list, weekly editor, exceptions with the spartan calendar, month/week read-only view built with helm and own grid, Italian holidays import) and the machine detail inside `features/equipment`. The initial bundle is above the 500 kB warning (CSS from Tailwind grows with utilities); new pages stay in lazy chunks.

### Planning

- `PlanningFeature/Engine`: `IPlanningEngine` (pure input/output models) and `ForwardFiniteCapacityEngine`: deterministic processing order (priority, requested end, requested start, order code, operation), earliest start = max(now, requested start, predecessors' ends, first working instant of the machine), machine = assigned if valid else the one that finishes first (tie: segment default, then code), capacity N, continuous non-preemptive intervals (breaks and closures inside), efficiency applied, Running jobs fixed, 6-month horizon, cycle detection. All `DateTime` entering the engine are normalized to UTC (SQLite returns `Unspecified`). Engine messages carry placeholders; `PlanningService` formats them in the company time zone (`PlanningDateFormat`).
- Services: `PlanningInputBuilder` builds the input per scope (`Order`, `Unplanned`, `All`; already planned jobs out of scope are fixed occupancy), `PlanningService` previews (no writes, returns a token = hash of the proposal), applies in one transaction (writes dates and `PlannedAt`, assigns a machine only where missing or no longer valid) and removes a plan (dates only). `PlanAlertsCalculator` and `PlanStatusCalculator` are pure; `TimelineCalculator` builds the timeline (lanes, non-working bands, load).
- Bench: `PlanningBench` (libs/api-tests) measures total lateness, last end and utilization on fixed scenarios; the baselines are regression ceilings for any future engine.
- Web: `features/planning` (page, preview dialog reusable from the order detail, timeline with an own CSS grid in the company time zone, work panel, alerts); plan columns in work orders; a "Piano" block and a banner in the order detail. The toaster is deferred (`@defer (on idle)`) to keep the initial bundle under 500 kB.

### Plan lock (specs/planning-lock)

- `WorkOrder.IsPlanLocked/PlanLockedAt`; `allowedActions` `Lock`/`Move`/`Unlock` (work order) and `LockAll`/`UnlockAll` (order). The lock is cleared in `WorkOrderService.ApplyAsync` when the job starts, ends or is aborted, and when the order is closed or cancelled.
- Engine: locked jobs are fixed intervals (reason `Locked`); conflicts (`PredecessorAfterLockedStart`, `LockedOverCapacity`) are in the result, `notMoved[]` and `conflicts[]` in preview/apply, `keptLocked` in remove-plan. Bench scenario 5 covers locked jobs.
- `PlanLockService` + `ManualMoveCalculator` (pure): `move/preview` and `move` (start UTC + optional machine; end computed with the calendar; 422 for past or non-working start with the next working instant; conflicts `PredecessorEndsAfterStart`, `SuccessorsStartBeforeEnd`, `MachineOverCapacity`, `Late` never block; `expectedUpdatedAt` for 409). Alerts `LockedPredecessorConflict`, `LockedMachineUnavailable`, `LockedOverCapacity`, `LockedLate`; `planStatusReason` (`StaleCause`) with locked causes; filter `locked=true` on timeline and work orders.
- Web: `features/planning` `plan-lock.ts` (company time zone conversion, labels), `plan-lock-actions.ts`, `plan-move-dialog.component.ts`; padlock on bars, legend and 'Solo fissati' filter (`locked=1`); order actions and badge in `features/production-orders`.

### Backend data access rules

- Never use `AppDbContext`/`DbContext` in product code. It appears only in repository constructors (`GenericRepository<T, int, AppDbContext>`), DbContext registration (interceptors) and migrations/design-time factory. Services, planners, seeders and controllers use repositories or `IUnitOfWork`.
- A service that needs transactions injects `IUnitOfWork` and resolves its repositories from it (`uow.GetRepository<IXRepository>()`), never next to the UoW; services without transactions inject the repositories directly.
- Repository interfaces stay empty extensions of `IGenericRepository<T, int>`.
- Dependency direction `Api -> ApiFeatures -> ApiCore`: code needing repositories (seeders included) lives in `ApiFeatures` or `Api`. Sample data: `SampleDataFeature` (master data) and `ProductionOrderFeature` (orders), both Development-only.
- Tests may use the DbContext for arrange/assert.

### Constraint violations (409)

- PostgreSQL unique (23505) and foreign-key (23503) violations never surface as 500. illyW's `GenericRepository` swallows the `SaveChanges` exception into a failed `Result`, so a `SaveChangesInterceptor` records the violation in a per-request `ConstraintViolationTracker`; a global result filter turns a 500 with a recorded violation into a 409 `ProblemDetails` (Italian message from the constraint-name map in `ConstraintViolation.Message`; the constraint name only goes to the logs). An `IExceptionHandler` is the safety net for violations that escape as exceptions.
- Service-level checks (explicit 409 before saving) stay; the mechanism is for races and inconsistent data. New unique indexes need an entry in the message map.
- Order code collisions (top-level race or inconsistent child codes) answer 409 with a rollback; there is no retry loop. A failed `SaveChanges` leaves the entity tracked (state `Added`) on the scoped DbContext, so retrying in the same scope would re-insert it; the clean way to retry would be a fresh DI scope per attempt, not touching the DbContext.

### Backend tests

- `libs/api-tests` (`ApiTests`, xUnit + FluentAssertions 7 + NSubstitute): fast unit tests (~1 s). Pure logic (Stateless machines, `ProductionOrderPlanner`) and services on in-memory SQLite (`BaseFeatureFixture`, foreign keys on). Folders per feature, files named `<Subject>Fixture.cs`, test names `Subject_Scenario_Expected`.
- `libs/api-tests-integration` (`ApiTests.Integration`): a few high-value tests on a **real PostgreSQL** (Testcontainers, `postgres:17.6-alpine`, one container per run, Respawn between tests) through `WebApplicationFactory<Program>`. They cover what SQLite/InMemory cannot: transactions (`IUnitOfWork`), unique indexes, FK `Restrict`, migrations from scratch and the Development seeders. Docker required; the local `micromes-db` container is never used.
- Run: `npx nx run ApiTests:unit-test`, `npx nx run ApiTests.Integration:unit-test`. Rule: business rules go in unit tests; add an integration test only when the behaviour depends on PostgreSQL or on a transaction.
- Note: `AllowedActions` of the machines follows the enum order (Stateless 5.20.1 has no non-obsolete sync API to list permitted triggers).

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
