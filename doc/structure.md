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
