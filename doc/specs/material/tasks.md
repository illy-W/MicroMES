# Implementation Plan: Material (Materiale)

## Technical Strategy

Primo slice verticale: PostgreSQL + EF Core + illyW Framework nel backend, pagina "Materiali" nel frontend. Pattern come `ptconnect-nx` ma senza CQRS: `Controller` → `MaterialService` (Result pattern) → `IMaterialRepository` (interfaccia vuota, solo i metodi di `IGenericRepository`) → `AppDbContext`. Soft-delete con `IsActive`, mai `DeleteAsync`. Mapping manuale, niente AutoMapper e niente Silverback.

Ordine: `[Nx]` (database locale e componenti spartan), `[Backend]` (bloccante, termina con la rigenerazione del contratto), `[Frontend]` (riceve il riassunto del contratto), `[Nx]` per agent e documentazione, infine verifica del lead. Il lead aggiorna `doc/glossary.md`.

## Tasks

- [x] 1. **Nx workspace: database locale e componenti spartan**
  - [x] 1.1 [Nx] Creare `docker-compose.yml` nella root con PostgreSQL (immagine con versione fissata, volume nominato, porta 5432, credenziali di sviluppo) e un target Nx (es. `Api:db-up` / `db-down`, o equivalente) per avviarlo e fermarlo. Verificare che il container parta e accetti connessioni.
    - _Satisfies: REQ-1, REQ-5_
  - [x] 1.2 [Nx] Generare con `npx nx g @spartan-ng/cli:ui <nome> --no-interactive` i componenti `table`, `dialog`, `alert-dialog`, `input` (già presente), `label`, `select`, `badge`, `switch`, `textarea` e le dipendenze richieste dal generatore. Per ciascuno: `lint --fix`, Prettier sui JSON riscritti e controllo degli alias (prefisso `./`, nessun `baseUrl`). Verificare `npx nx run-many -t build lint`. Riportare l'elenco finale dei componenti e delle nuove dipendenze.
    - _Satisfies: REQ-4_
- [x] 2. **Backend: persistenza, repository e API**
  - [x] 2.1 [Backend] Aggiungere i pacchetti con versioni esatte: `illyW.Framework.Core` e `illyW.Framework.EFCore` (1.3.2) in `ApiCore`, `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` e `Microsoft.EntityFrameworkCore.Design` compatibili con net10.0. Niente Supabase, Hangfire o Silverback.
    - _Satisfies: REQ-1_
  - [x] 2.2 [Backend] Creare in `ApiCore/Persistence` l'entità `Material : IEntity<int>` (`Entities/Material.cs`) con le enum `MaterialType` e `UnitOfMeasure` (salvate come stringhe), `AppDbContext` con `DbSet<Material> Materials` e la configurazione in `OnModelCreating` (lunghezze, indice univoco su `Code`, `IsActive` default `true`), `AppDbContextFactory` per il design time.
    - _Satisfies: REQ-1_
  - [x] 2.3 [Backend] Registrare `AppDbContext` in `Program.cs` (`AddDbContextFactory` o `AddDbContext` con Npgsql), con la stringa di connessione in `appsettings.Development.json` verso il PostgreSQL del compose. Applicare le migration all'avvio solo in Development. La generazione di `openapi/Api.json` non deve richiedere il database.
    - _Satisfies: REQ-1_
  - [x] 2.4 [Backend] Creare la prima migration (`dotnet ef migrations add`, in `ApiCore/Persistence/Migrations`) e verificarne l'applicazione sul database locale.
    - _Satisfies: REQ-1_
  - [x] 2.5 [Backend] Creare `libs/api-features/MaterialFeature`: `IMaterialRepository` (nessun metodo aggiuntivo) e `MaterialRepository`, `Registrar.cs` con `AddRepository` e `MaterialService`, richiamato da `Program.cs`. `DeleteAsync` non è mai usato.
    - _Satisfies: REQ-2_
  - [x] 2.6 [Backend] Creare i modelli `CreateMaterialRequest`, `UpdateMaterialRequest` (DataAnnotations) e `MaterialModel`, con mapping manuale. `MaterialService` (`Result`/`Result<T>`, `Fail()` prima di `AddError()`): lista (solo attivi di default, ordinata per `Code`), dettaglio, creazione, aggiornamento (anche `IsActive`, con `UpdatedAt`), disattivazione idempotente. `Code` normalizzato (trim, maiuscolo), duplicato rilevato anche sui disattivati.
    - _Satisfies: REQ-3_
  - [x] 2.7 [Backend] Creare `MaterialsController` (`api/materials`) con `GetMaterials`, `GetMaterial`, `CreateMaterial`, `UpdateMaterial`, `DeactivateMaterial`; `operationId` espliciti, 200/201/204, 404 per id assente, 409 per codice duplicato, 422 per la validazione. Enum serializzati come stringhe e presenti come enum nell'OpenAPI.
    - _Satisfies: REQ-3_
  - [x] 2.8 [Backend] Verifica manuale con PostgreSQL acceso: avviare l'API e provare con `curl` creazione, duplicato (409), validazione (422), lista (solo attivi e `includeInactive=true`), modifica, disattivazione, riattivazione, id assente (404). Poi fermare i processi. Eseguire `npx nx build Api`.
    - _Satisfies: REQ-1, REQ-2, REQ-3_
  - [x] 2.9 [Backend] Rigenerare il contratto con `npx nx run api-client:generate-api` (non modificare `generated/**` a mano) e riportare il riassunto: endpoint, modelli, enum e nome del servizio generato.
    - _Satisfies: REQ-3_
- [x] 3. **Frontend: pagina Materiali**
  - [x] 3.1 [Frontend] Aggiungere la voce "Materiali" in `sidebar-data.ts` (icona lucide registrata in `nav-main`) e la route lazy `materials` sotto il layout `full` in `app.routes.ts`.
    - _Satisfies: REQ-4_
  - [x] 3.2 [Frontend] Creare `features/materials/` con `MaterialsComponent`: tabella TanStack Table con i componenti helm, colonne Codice, Nome, Tipo, Unità, Fornitore, Cod. fornitore, Stato (badge), Azioni; ricerca testuale e interruttore "Mostra disattivati" lato client; caricamento con `skeleton`, errore di rete e stato vuoto con `hlm-empty` e invito a creare il primo materiale. Dati dal client generato (`@micromes/api-client/*`).
    - _Satisfies: REQ-4_
  - [x] 3.3 [Frontend] Creare il dialog con la form di creazione e modifica (codice, nome, descrizione, tipo, unità, fornitore, codice fornitore e, in modifica, stato attivo): validazione coerente con le lunghezze di REQ-1, errori 409/422 dell'API mostrati nel dialog. Etichette in italiano (tipi: Materia prima, Semilavorato, Prodotto finito, Materiale di consumo; unità: pz, g, kg, m, l).
    - _Satisfies: REQ-4_
  - [x] 3.4 [Frontend] Disattivazione con conferma in `alert-dialog`; la riattivazione avviene dal dialog di modifica. Standalone, OnPush, signals, solo componenti helm e classi semantiche.
    - _Satisfies: REQ-4_
  - [x] 3.5 [Frontend] Verificare `npx nx build web` e `npx nx lint web`. Con PostgreSQL acceso, avviare `npx nx serve web` in background (parte anche `Api:serve`; usare una porta libera se la 4200 è occupata) e controllare che la pagina e le chiamate `/api/materials` rispondano tramite il proxy; se disponibili, gli strumenti del browser per uno screenshot (altrimenti dichiararlo). Poi fermare web e `dotnet watch`.
    - _Satisfies: REQ-4_
- [x] 4. **Agent e documentazione**
  - [x] 4.1 [Nx] `doc/structure.md`: estendere "Backend patterns" con la struttura di una feature (`Controllers`, `Models`, `Services`, `Repositories`, `Registrar.cs`), la regola "mai `AppDbContext` fuori dai repository", il Result pattern, il repository senza metodi aggiuntivi, il soft-delete, il mapping manuale, le migration (comandi `dotnet ef`), il database locale e la pagina Materiali come esempio di feature frontend. Non toccare le altre sezioni.
    - _Satisfies: REQ-5_
  - [x] 4.2 [Nx] `AGENTS.md`: citare ISA-95 e `doc/glossary.md`, documentare come avviare il database e creare una migration. `.opencode/agents/backend.md` e `feature-lead.md`: rimandare al pattern backend e al glossario. Mantenere stile e lingua di ciascun file, senza riformattare altro.
    - _Satisfies: REQ-5_
- [x] 5. **Verifica**
  - [x] 5.1 `npx nx run-many -t build lint` passa su tutti i progetti.
    - _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4_
  - [x] 5.2 [Lead] Aggiornare `doc/glossary.md` (`Material` adottato con campi, enum e scostamenti da ISA-95: fornitore sul materiale, unità come enum).
    - _Satisfies: REQ-5_
  - [x] 5.3 Controllo dei criteri di accettazione di `requirements.md` rispetto ai report degli agent; `git status` coerente (nessun `baseUrl`, alias con `./`, nessun `DeleteAsync`, nessun `AppDbContext` fuori dai repository).
    - _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5_
