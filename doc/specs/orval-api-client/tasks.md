# Implementation Plan: Client API autogenerato con Orval

## Technical Strategy

Seguire il pattern di ptconnect-nx. L'API genera `openapi/Api.json` con una build che ha `OpenApiGenerateDocuments=true`. Orval legge il file e genera il client Angular in `libs/shared/api-client/src/generated`. Il client usa URL relative (`/api/...`) e il dev-server di `web` le inoltra all'API con un proxy. Nessun controller viene aggiunto: la specifica avrà `paths` vuoto.

Ordine di esecuzione: `[Nx]` (libreria, Orval, target), poi `[Backend]` (OpenAPI e rigenerazione), poi `[Frontend]` (HttpClient e proxy).

I file `.opencode/agents/*.md` e `AGENTS.md` (REQ-5) sono fuori dal perimetro di `feature-lead` (solo `doc/**`): li esegue `nx-expert`.

## Tasks

- [x] 1. **Nx workspace**
  - [x] 1.1 [Nx] Aggiungere `orval` alle devDependencies (versione esatta, come `save-exact`).
    - _Satisfies: REQ-2_
  - [x] 1.2 [Nx] Creare il progetto Nx `api-client` in `libs/shared/api-client` (`project.json`, `tsconfig.json`, `tsconfig.lib.json`, `eslint.config.mjs`, `src/`), coerente con `web`.
    - _Satisfies: REQ-2_
  - [x] 1.3 [Nx] Creare `orval.config.ts` nella root: input `./openapi/Api.json`, output `libs/shared/api-client/src/generated/{services,model}`, `client: 'angular'`, `mode: 'tags'`, `namingConvention: 'kebab-case'`, `clean: true`, formatter Prettier.
    - _Satisfies: REQ-2_
  - [x] 1.4 [Nx] Aggiungere in `tsconfig.base.json` gli alias `@micromes/api-client/models` e `@micromes/api-client/services/*`.
    - _Satisfies: REQ-2_
  - [x] 1.5 [Nx] Escludere `libs/shared/api-client/src/generated/**` da ESLint (`eslint.config.mjs`) e da Prettier (`.prettierignore`).
    - _Satisfies: REQ-2_
  - [x] 1.6 [Nx] Definire in `api-client/project.json` i target `generate-api` (`npx orval --config orval.config.ts`, dipende da `Api:build:release:doc`) e `generate-api:no-build`.
    - _Satisfies: REQ-3_
  - [x] 1.7 [Nx] Creare `apps/api/project.json` con `build:dev`, `build:dev:doc`, `build:release`, `build:release:doc` (`-p:OpenApiGenerateDocuments=false/true`, cwd `{projectRoot}`, cache e outputs sui release) e `serve` (`dotnet watch run`, continuous, dipende da `build:dev`). Verificare che il target `build` inferito da `@nx/dotnet` non si rompa.
    - _Satisfies: REQ-3_
- [x] 2. **Backend**
  - [x] 2.1 [Backend] In `apps/api/Api.csproj`: proprietà `OpenApiGenerateDocuments=false` e `OpenApiDocumentsDirectory=../../openapi`, `AssemblyMetadata` con lo stesso flag, pacchetti `Microsoft.AspNetCore.OpenApi` e `Microsoft.Extensions.ApiDescription.Server` (stessa versione della major di .NET in `global.json`).
    - _Satisfies: REQ-1_
  - [x] 2.2 [Backend] In `apps/api/Program.cs`: `AddOpenApi()`, `MapOpenApi()` in Development e lettura del flag da `AssemblyMetadata`. Con il flag attivo la build scrive `openapi/Api.json`; con il flag false l'app si avvia come prima.
    - _Satisfies: REQ-1_
  - [x] 2.3 [Backend] Documentare la convenzione dei nomi delle azioni (`Name` nel formato `{Verb}{Resource}{Action?}` in PascalCase) nella sezione backend di `doc/structure.md`. Nessun controller viene aggiunto.
    - _Satisfies: REQ-1, REQ-5_
  - [x] 2.4 [Backend] Generare `openapi/Api.json` (`npx nx run Api:build:release:doc`) e riportare il riepilogo del contratto: con `paths` vuoto, nessun endpoint.
    - _Satisfies: REQ-1_
- [x] 3. **Generazione client**
  - [x] 3.1 [Nx] Eseguire `npx nx run api-client:generate-api` e verificare il risultato. Se Orval fallisce o non genera nulla con `paths` vuoto, fermarsi e riportarlo al lead: non aggiungere endpoint senza approvazione.
    - _Satisfies: REQ-2, REQ-3_
- [x] 4. **Frontend**
  - [x] 4.1 [Frontend] Aggiungere `provideHttpClient()` in `apps/web/src/app/app.config.ts`.
    - _Satisfies: REQ-4_
  - [x] 4.2 [Frontend] Creare `apps/web/proxy.config.local.json` (`/api` → `http://localhost:5190`, `secure: false`) e collegarlo alla configurazione `development` del target `serve` di `web`, con `dependsOn: ["Api:serve"]`.
    - _Satisfies: REQ-4_
- [x] 5. **Agent e documentazione**
  - [x] 5.1 [Nx] Aggiornare `.opencode/agents/backend.md`: dopo modifiche al contratto, l'ultimo task è rigenerare `openapi/Api.json` e il client (`nx run api-client:generate-api`) e riportare le modifiche.
    - _Satisfies: REQ-5_
  - [x] 5.2 [Nx] Aggiornare `.opencode/agents/frontend.md`: usa solo `libs/shared/api-client/src/generated/**`, non lo modifica mai e segnala al backend gli endpoint mancanti.
    - _Satisfies: REQ-5_
  - [x] 5.3 [Nx] Aggiornare `.opencode/agents/nx-expert.md` e `.opencode/agents/feature-lead.md`: conoscono il target `generate-api`. Il lead inserisce la rigenerazione come ultimo task `[Backend]` quando l'API cambia.
    - _Satisfies: REQ-5_
  - [x] 5.4 [Nx] Aggiornare `AGENTS.md`: struttura (`libs/shared/api-client`, `openapi/`, `orval.config.ts`) e comandi (`generate-api`, `generate-api:no-build`).
    - _Satisfies: REQ-5_
  - [x] 5.5 [Nx] Aggiornare `doc/structure.md`: sezione sul client generato (struttura, flusso di rigenerazione, regola di non modificare `generated/**`, URL relative e proxy).
    - _Satisfies: REQ-4, REQ-5_
- [x] 6. **Verifica**
  - [x] 6.1 `npx nx run-many -t build lint` passa su tutti i progetti, inclusi `api-client` e `Api`.
    - _Satisfies: REQ-1, REQ-2, REQ-3_
  - [x] 6.2 `npx nx run api-client:generate-api:no-build` è ripetibile e `git status` non mostra modifiche inattese ai file generati.
    - _Satisfies: REQ-3_
  - [x] 6.3 Controllo dei criteri di accettazione di `requirements.md` rispetto ai report degli agent.
    - _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5_
