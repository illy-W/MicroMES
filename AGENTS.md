# MicroMES

Monorepo Nx (pnpm) con frontend Angular e backend .NET.

## Struttura

| Percorso | Progetto Nx | Descrizione |
|---|---|---|
| `apps/web` | `web` | Frontend Angular (SCSS) |
| `apps/api` | `Api` | API ASP.NET Core (`MicroMES.Api`) |
| `libs/api-features` | `ApiFeatures` | Lib .NET con le feature (`MicroMES.ApiFeatures`) |
| `libs/api-core` | `ApiCore` | Lib .NET con il core (`MicroMES.ApiCore`) |
| `libs/api-tests` | `ApiTests` | Test unitari .NET (xUnit, `MicroMES.ApiTests`) |
| `libs/api-tests-integration` | `ApiTests.Integration` | Test di integrazione .NET (Testcontainers PostgreSQL, richiede Docker) |
| `libs/ui/*` | `<nome>` (es. `button`) | Componenti spartan helm, generati (alias `@spartan-ng/helm/*`, mai a mano) |
| `libs/shared/api-client` | `api-client` | Client Angular generato da Orval (`src/generated/**`, mai modificare a mano) |
| `docker-compose.yml` | - | Database PostgreSQL locale (container `micromes-db`) |
| `dotnet-tools.json` | - | Tool .NET locali (`dotnet-ef`) |
| `openapi/Api.json` | - | Contratto OpenAPI generato dall'API |
| `orval.config.ts` | - | Configurazione Orval (root) |

Dipendenze backend: `Api` → `ApiFeatures` → `ApiCore`. Non invertirle.
La solution è `MicroMES.Backend.sln`. I nomi dei progetti Nx .NET derivano dal nome del `.csproj`.

## Versioni

Non documentate qui: leggile da `package.json`, `global.json`, `.nvmrc` e dai `.csproj`.
Node: usa la versione di `.nvmrc`.

## Comandi

- `npx nx show projects` / `npx nx graph`: esplora il workspace
- `npx nx build <progetto>` / `npx nx run-many -t build`
- `npx nx affected -t build`
- `npx nx serve web`: avvia il frontend
- `dotnet run --project apps/api`: avvia l'API
- `npx nx run api-client:generate-api`: rigenera `openapi/Api.json` e il client Angular (`generate-api:no-build` salta la build dell'API)
- `npx nx run ApiTests:unit-test`: test unitari backend; `npx nx run ApiTests.Integration:unit-test`: test di integrazione (richiede Docker)
- `npx nx g @spartan-ng/cli:ui <nome> --no-interactive`: aggiunge un componente spartan in `libs/ui` (poi `npx nx lint <nome> --fix`)
- `npx nx run Api:db-up` / `npx nx run Api:db-down`: avvia/ferma il database locale
- Nuova migration: `dotnet tool restore`, poi `dotnet ef migrations add <Nome> --project libs/api-core --startup-project apps/api --output-dir Persistence/Migrations` (applicate all'avvio solo in Development)
- Nuovi progetti Angular: `npx nx g @nx/angular:...`. I progetti .NET si creano con `dotnet new`, poi si aggiungono alla solution.

## Convenzioni

- Cartelle in kebab-case, progetti e namespace .NET in PascalCase con prefisso `MicroMES.`.
- Usa l'agent `@nx-expert` e le skill `nx-workspace`, `nx-generate`, `nx-run-tasks` per le operazioni sul monorepo.
- Skill `impeccable` (design UI, mode Operate): usa `PRODUCT.md` e `DESIGN.md` in root come pointer a `doc/`; la skill è in `.opencode/skills/impeccable`.
- Vocabolario di dominio: riferimento ISA-95 in forma ridotta, vedi `doc/glossary.md`. Pattern backend: `doc/structure.md` (sezione "Backend patterns").
- Backend: mai `DbContext` nel codice di prodotto (solo repository o `IUnitOfWork`; con la UoW i repository si ottengono da `uow.GetRepository<>()`; interfacce repository vuote; seeder/codice con repository in ApiFeatures o Api, non in ApiCore).
- Dipendenze con `pnpm add -w` / `pnpm install`; comandi Nx con `npx nx`.
- Non fare commit né push senza richiesta esplicita.

## Spec-Driven Development

Per feature non banali usa l'agent `feature-lead` (primary). Specialisti: `nx-expert`, `backend`, `frontend`.
Prima di proporre soluzioni leggi `doc/product.md` e `doc/structure.md`.

1. **Requisiti**: crea `doc/specs/<feature-kebab-case>/requirements.md` e attendi approvazione esplicita.
2. **Task**: crea `tasks.md` nella stessa cartella e attendi approvazione.
3. **Esecuzione**: il lead delega (Nx, poi Backend, poi Frontend) e spunta i task; gli specialisti non modificano `tasks.md`.
4. **Verifica**: `npx nx affected -t lint build`, controllo dei criteri di accettazione, aggiornamento di `doc/`.

Template `requirements.md`:

```
# Spec: {Feature}
## 1. Context & Goals
## 2. Requirements
### REQ-1: {Titolo}
- **User Story**: As a {role}, I want {action} so that {benefit}.
- **Acceptance Criteria**: - [ ] ...
- **Technical Constraints**: ...
## 3. Layers impacted
Backend / Web / Nx workspace
## 4. Impact Assessment
- Affected files/modules, breaking changes (sì/no)
```

Template `tasks.md`: sezione "Technical Strategy" e lista di task per layer, ognuno con owner (`[Nx]`, `[Backend]`, `[Frontend]`) e `_Satisfies: REQ-n_`.
