# Spec: Client API autogenerato con Orval

## 1. Context & Goals

Oggi il frontend dovrebbe scrivere a mano i servizi HTTP verso l'API .NET. Vogliamo che il client TypeScript sia generato da Orval a partire dalla specifica OpenAPI prodotta dal backend, così il contratto API è l'unica fonte di verità e il frontend non scrive né modifica i servizi.

Riferimento: `ptconnect-nx` (`orval.config.ts`, `openapi/Api.json`, `libs/shared/api-client`, target `generate-api`).

## 2. Requirements

### REQ-1: Il backend produce la specifica OpenAPI

- **User Story**: As a sviluppatore, I want che l'API generi `openapi/Api.json` durante una build dedicata so that Orval abbia un input aggiornato.
- **Acceptance Criteria**:
  - [ ] `Api` espone OpenAPI con `Microsoft.AspNetCore.OpenApi` e `Microsoft.Extensions.ApiDescription.Server`.
  - [ ] Con `OpenApiGenerateDocuments=true` la build scrive `openapi/Api.json` nella root del repository.
  - [ ] Con `OpenApiGenerateDocuments=false` (default) la build normale non genera il file e l'app si avvia come prima.
  - [ ] In Development l'endpoint `/openapi/v1.json` è disponibile.
  - [ ] Convenzione documentata: ogni azione dei controller dichiara un nome esplicito (`Name`, formato `{Verb}{Resource}{Action?}` in PascalCase) per avere nomi di operazione stabili.
- **Technical Constraints**: .NET come da `global.json`; seguire il pattern di ptconnect-nx (flag `OpenApiGenerateDocuments` come `AssemblyMetadata`). Nessuna dipendenza da `ApiFeatures`/`ApiCore` verso `Api`. Non esistono ancora controller e non se ne aggiungono: la specifica generata avrà `paths` vuoto.

### REQ-2: Libreria `api-client` con configurazione Orval

- **User Story**: As a sviluppatore frontend, I want una libreria `api-client` con codice generato so that non debba scrivere servizi HTTP a mano.
- **Acceptance Criteria**:
  - [ ] Esiste `libs/shared/api-client` come progetto Nx (nome `api-client`), con `eslint` e `tsconfig` coerenti con `web`.
  - [ ] `orval.config.ts` nella root legge `./openapi/Api.json` e genera in `libs/shared/api-client/src/generated/{services,model}` con `client: 'angular'`, `mode: 'tags'`, nomi in kebab-case, `clean: true` e formatter Prettier.
  - [ ] `orval` è nelle `devDependencies` (versione esatta, `save-exact`).
  - [ ] I path alias in `tsconfig.base.json` permettono `@micromes/api-client/models` e `@micromes/api-client/services/*`.
  - [ ] Il codice generato è versionato, escluso da lint e da Prettier, e mai modificato a mano.
- **Technical Constraints**: Angular `HttpClient` (il frontend deve fornire `provideHttpClient()`); il filtro lint non deve fallire sul codice generato; il path alias usa lo scope del workspace (`@micromes`).

### REQ-3: Target Nx di generazione

- **User Story**: As a sviluppatore, I want un comando Nx unico so that rigenerare il client sia ripetibile e dipenda dalla build dell'API.
- **Acceptance Criteria**:
  - [ ] `nx run api-client:generate-api` costruisce l'API con la specifica (target `build:release:doc` o equivalente su `Api`) e poi esegue Orval.
  - [ ] `nx run api-client:generate-api:no-build` esegue solo Orval sull'`openapi/Api.json` esistente.
  - [ ] `apps/api/project.json` definisce `build:dev`, `build:dev:doc`, `build:release`, `build:release:doc` (con `OpenApiGenerateDocuments` true/false, cwd `{projectRoot}`, cache e outputs sui release) e `serve` (`dotnet watch run`, continuous, dipende da `build:dev`), come in ptconnect-nx, senza rompere il target `build` inferito da `@nx/dotnet`.
  - [ ] `nx run-many -t build lint` continua a passare.
- **Technical Constraints**: configurare in `project.json` solo ciò che il plugin `@nx/dotnet` non inferisce; usare `nx:run-commands`; definire `inputs`/`outputs` corretti per la cache.

### REQ-4: Il frontend usa solo il client generato

- **User Story**: As a sviluppatore frontend, I want importare i servizi generati so that non debba creare servizi HTTP manuali.
- **Acceptance Criteria**:
  - [ ] `web` fornisce `provideHttpClient()` nella configurazione dell'app.
  - [ ] Il client generato usa URL relative (`/api/...`): nessuna base URL negli file generati né interceptor dedicato.
  - [ ] `web:serve` usa un `proxyConfig` che inoltra `/api` all'API locale (`http://localhost:5190`, da `launchSettings.json`), con `secure: false`, come in ptconnect-nx.
  - [ ] `web:serve` dipende da `Api:serve`.
  - [ ] Documentato che gli endpoint mancanti vanno richiesti al backend e che `generated/**` non si modifica.
- **Technical Constraints**: standalone, signals; nessun client HTTP scritto a mano. In produzione la risoluzione di `/api` spetta al reverse proxy (fuori scopo). Non essendoci endpoint, in questa fase non c'è un consumatore del client in `web`.

### REQ-5: Agent e documentazione allineati

- **User Story**: As a team, I want che agent e documentazione descrivano il nuovo flusso so that il contratto sia gestito correttamente.
- **Acceptance Criteria**:
  - [ ] `backend` riceve la regola: dopo modifiche al contratto rigenera `openapi/Api.json` e il client (`generate-api`) come ultimo task e riporta i cambiamenti.
  - [ ] `frontend` riceve la regola: usa solo `libs/shared/api-client/src/generated/**` e non lo modifica; endpoint mancanti vanno segnalati al backend.
  - [ ] `nx-expert` e `feature-lead` conoscono il target `generate-api`; `feature-lead` inserisce la rigenerazione come ultimo task `[Backend]` quando l'API cambia.
  - [ ] `AGENTS.md` e `doc/structure.md` descrivono struttura, comandi e flusso.
- **Technical Constraints**: modifiche ai file `.opencode/agents/*.md` e `AGENTS.md` eseguite da un agent con permessi di scrittura (il lead modifica solo `doc/**`).

## 3. Layers impacted

- **Backend**: pacchetti OpenAPI, proprietà `.csproj`, `Program.cs`, endpoint di esempio, nomi delle azioni.
- **Web**: `provideHttpClient`, base URL, uso del client generato.
- **Nx workspace**: nuova libreria `api-client`, `orval.config.ts`, target `generate-api`, target `Api` con documento, `tsconfig.base.json`, ESLint/Prettier ignore, dipendenza `orval`.

## 4. Impact Assessment

- **Affected Files/Modules**: `apps/api/Api.csproj`, `apps/api/Program.cs`, `apps/api/project.json` (nuovo), `openapi/Api.json` (nuovo), `orval.config.ts`, `libs/shared/api-client/**`, `tsconfig.base.json`, `package.json`, `apps/web/src/app/app.config.ts`, `.prettierignore`, `eslint.config.mjs`, `.opencode/agents/*.md`, `AGENTS.md`, `doc/structure.md`.
- **Breaking Changes**: No. Il workspace non ha ancora controller né consumatori del client.

## 5. Decisioni

1. **Endpoint di esempio**: no. Nessun controller aggiunto.
2. **Base URL**: come ptconnect-nx, URL relative `/api` + proxy del dev-server per configurazione. Nessun interceptor né `mutator`.
3. **Percorso libreria**: `libs/shared/api-client`.
4. **Progetto `Api`**: aggiungere `project.json` con i target `build:*` e `serve` come ptconnect-nx.

## 6. Rischi

- Con `paths` vuoto Orval potrebbe non generare servizi (o fallire): il task di verifica deve controllarlo; se fallisce, si valuta un endpoint minimo di validazione da concordare.
- Il nome del progetto Nx è `Api` (da `Api.csproj`): i target e le dipendenze (`Api:build:release:doc`, `Api:serve`) usano quel nome.
