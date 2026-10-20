# Spec: Material (Materiale)

## 1. Context & Goals

`Material` è l'anagrafica di base del MES: tutto ciò che si acquista, si trasforma o si produce (materia prima, semilavorato, prodotto finito, materiale di consumo). Corrisponde alla **Material Definition** di ISA-95 (vedi `doc/glossary.md`). Nella UI si chiama **"Materiale"**.

Questa è la prima entità e la prima persistenza del progetto: la spec introduce anche PostgreSQL, EF Core e il pattern backend (illyW Framework, repository, Result pattern) che le feature successive riuseranno. Riferimento: `ptconnect-nx` (`libs/ApiCore/Persistence`, `libs/ApiFeatures/*Feature`), **senza CQRS** (niente Silverback, né command/query handler).

Fuori ambito: `MaterialClass`, `MaterialLot` e tracciabilità, distinta base e cicli (Product Definition), entità `Supplier`, autenticazione, paginazione lato server, test automatici.

## 2. Requirements

### REQ-1: Modello dati e persistenza

- **User Story**: As a sviluppatore, I want un'entità `Material` persistita su PostgreSQL so that le feature successive possano riferirla.
- **Acceptance Criteria**:
  - [ ] Entità `Material : IEntity<int>` in `libs/api-core/Persistence/Entities/Material.cs` con:
    - `Id` (int), `Code` (obbligatorio, max 50, univoco), `Name` (obbligatorio, max 200), `Description` (opzionale, max 1000);
    - `Type` (enum `MaterialType`: `RawMaterial`, `Intermediate`, `FinishedGood`, `Consumable`; salvato come stringa);
    - `UnitOfMeasure` (enum `UnitOfMeasure`: `Piece`, `Gram`, `Kilogram`, `Meter`, `Liter`; salvato come stringa);
    - `Vendor` (opzionale, max 200) e `VendorMaterialId` (opzionale, max 100): fornitore e codice articolo del fornitore, per i materiali acquistati;
    - `IsActive` (default `true`), `CreatedAt`, `UpdatedAt` (UTC).
  - [ ] `Code` è normalizzato (trim, maiuscolo) prima del salvataggio: l'unicità è quindi senza distinzione tra maiuscole e minuscole. L'indice univoco include anche i materiali disattivati.
  - [ ] `AppDbContext` (`libs/api-core/Persistence`) con `DbSet<Material> Materials` e configurazione in `OnModelCreating`; `AppDbContextFactory` per i migration a design time.
  - [ ] Prima migration EF Core in `libs/api-core/Persistence/Migrations` che crea la tabella `Materials`.
  - [ ] Stringa di connessione in `appsettings.Development.json`, verso un PostgreSQL locale avviato da un `docker-compose.yml` nella root; le migration sono applicate all'avvio solo in Development.
- **Technical Constraints**: pacchetti `illyW.Framework.Core` e `illyW.Framework.EFCore` (stessa versione del riferimento, 1.3.2) e `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore` e `Design` compatibili con net10.0, con versioni esatte. Chiave primaria sempre `int`. Dipendenze `Api` → `ApiFeatures` → `ApiCore` invariate. Nessun `baseUrl`/riferimento al riferimento non necessario (niente Supabase, Hangfire, Silverback).

### REQ-2: Repository

- **User Story**: As a sviluppatore, I want un repository per `Material` so that nessun codice di dominio usi direttamente `AppDbContext`.
- **Acceptance Criteria**:
  - [ ] `IMaterialRepository : IGenericRepository<Material, int>` e `MaterialRepository : GenericRepository<Material, int, AppDbContext>` in `libs/api-features/MaterialFeature/Repositories`.
  - [ ] **L'interfaccia non aggiunge alcun metodo** rispetto a `IGenericRepository`: si usano solo `GetSingleAsync`, `FetchAsync`, `AddAsync`, `UpdateAsync`.
  - [ ] Registrazione con `services.AddRepository<IMaterialRepository, MaterialRepository>()` in `MaterialFeature/Registrar.cs`, richiamata da `Program.cs`.
  - [ ] `DeleteAsync` non è mai invocato.
- **Technical Constraints**: la logica non inietta `AppDbContext`; gli unici accessi ai dati passano dal repository.

### REQ-3: API CRUD con soft-delete

- **User Story**: As a utente, I want creare, leggere, modificare e disattivare materiali so that l'anagrafica resti aggiornata senza perdere storico.
- **Acceptance Criteria**:
  - [ ] Controller `MaterialsController`, route `api/materials`, con azioni e `operationId`:
    - `GET api/materials?includeInactive=false` → `GetMaterials`;
    - `GET api/materials/{id}` → `GetMaterial`;
    - `POST api/materials` → `CreateMaterial` (201 con il materiale creato);
    - `PUT api/materials/{id}` → `UpdateMaterial` (modifica tutti i campi, compreso `IsActive`, quindi permette la riattivazione);
    - `DELETE api/materials/{id}` → `DeactivateMaterial`: **soft-delete**, imposta `IsActive = false`, risponde 204; è idempotente.
  - [ ] Nessuna cancellazione fisica in nessun punto dell'API.
  - [ ] Il controller usa un `MaterialService` (scoped, in `MaterialFeature/Services`) che restituisce `Result`/`Result<T>` (`illyW.Framework.Core.ResultPattern`: `Fail()` prima di `AddError()`); nessuna eccezione arriva al controller.
  - [ ] Codici di risposta: 200/201/204 in successo, 404 se l'id non esiste, 409 se il `Code` è già usato (anche da un materiale disattivato), 422 con gli errori per la validazione.
  - [ ] Validazione con DataAnnotations sui modelli di richiesta (`CreateMaterialRequest`, `UpdateMaterialRequest`); i modelli di risposta (`MaterialModel`) sono distinti dall'entità. Il mapping è scritto a mano (niente AutoMapper).
  - [ ] I valori di enum sono serializzati come stringhe e compaiono come enum in `openapi/Api.json`.
  - [ ] La lista è ordinata per `Code` e, per default, contiene solo i materiali attivi.
  - [ ] Il contratto è rigenerato (`npx nx run api-client:generate-api`) e il client Angular espone un servizio con tag `Materials`.
- **Technical Constraints**: nomi azione `{Verb}{Resource}` come in `doc/structure.md`; nessuna autenticazione; nessun CQRS/Silverback; nessuna paginazione server (la lista è completa, il filtro è lato client); `UpdatedAt` aggiornato a ogni modifica.

### REQ-4: Pagina "Materiali" nel frontend

- **User Story**: As a utente, I want gestire i materiali da una pagina dedicata so that posso consultare e modificare l'anagrafica.
- **Acceptance Criteria**:
  - [ ] Voce di menu "Materiali" in `sidebar-data.ts` (icona lucide, registrata in `nav-main`) e route lazy `materials` sotto il layout `full`, con `MaterialsComponent` in `apps/web/src/app/features/materials/`.
  - [ ] Tabella con TanStack Table (`@tanstack/angular-table`) e componenti spartan: colonne Codice, Nome, Tipo, Unità, Fornitore, Cod. fornitore, Stato (badge), Azioni. Ricerca testuale e interruttore "Mostra disattivati", entrambi lato client.
  - [ ] Pulsante "Nuovo materiale" che apre un **dialog** con il form; la stessa form (in dialog) serve per modificare. Campi: codice, nome, descrizione, tipo, unità di misura, fornitore, codice fornitore e, in modifica, stato attivo.
  - [ ] Validazione nella form (obbligatori e lunghezze massime coerenti con REQ-1); gli errori 409/422 dell'API sono mostrati nel dialog (es. codice già esistente).
  - [ ] Disattivazione con conferma (`alert-dialog`); i materiali disattivati si riattivano dal dialog di modifica.
  - [ ] Stato vuoto con `hlm-empty` e invito a creare il primo materiale; stato di caricamento con `skeleton` e gestione dell'errore di rete.
  - [ ] Etichette in italiano, in chiaro: tipi "Materia prima", "Semilavorato", "Prodotto finito", "Materiale di consumo"; unità "pz", "g", "kg", "m", "l".
  - [ ] Solo il client generato (`@micromes/api-client/*`) per le chiamate HTTP; componenti standalone, OnPush, signals; solo componenti helm e classi semantiche.
  - [ ] `nx build web` e `nx lint web` passano.
- **Technical Constraints**: componenti spartan generati con `@spartan-ng/cli` (previsti: `table`, `dialog`, `alert-dialog`, `input`, `label`, `select`, `badge`, `switch`, `textarea` più le dipendenze richieste dal generatore; `input`, `skeleton`, `empty`, `button` esistono già); nessun componente scritto a mano in `libs/ui`; nessuna nuova libreria oltre a quelle già installate, salvo richieste dal generatore.

### REQ-5: Nx workspace, agent e documentazione

- **User Story**: As a team, I want che il pattern backend e il vocabolario siano documentati so that gli agent li riusino nelle feature successive.
- **Acceptance Criteria**:
  - [ ] `docker-compose.yml` nella root con PostgreSQL (versione e credenziali di sviluppo documentate) e target/istruzioni per avviarlo (es. `npx nx run Api:db-up`, oppure documentato in `AGENTS.md`).
  - [ ] `doc/structure.md`: sezione "Backend patterns" estesa con struttura di una feature (`Controllers`, `Models`, `Services`, `Repositories`, `Registrar.cs`), regola "mai `AppDbContext` fuori dai repository", Result pattern, repository senza metodi aggiuntivi, soft-delete (mai `DeleteAsync`), migration (comandi `dotnet ef`), mapping manuale.
  - [ ] `doc/glossary.md`: `Material` segnato come adottato, con campi, enum e gli scostamenti da ISA-95 (vendor sul materiale invece di una relazione, unità di misura come enum).
  - [ ] `AGENTS.md` cita ISA-95 e `doc/glossary.md`; `.opencode/agents/backend.md` e `feature-lead.md` rimandano al pattern e al glossario; `AGENTS.md` documenta come avviare il database e creare una migration.
- **Technical Constraints**: le modifiche a `AGENTS.md` e `.opencode/agents/**` sono eseguite da `nx-expert`; il lead modifica solo `doc/**`.

## 3. Layers impacted

- **Backend**: `libs/api-core` (entità, `AppDbContext`, migration), `libs/api-features/MaterialFeature` (controller, service, modelli, repository, registrar), `apps/api` (`Program.cs`, `appsettings`), `openapi/Api.json` e client generato.
- **Web**: `apps/web/src/app/features/materials`, `sidebar-data.ts`/`nav-main`, `app.routes.ts`, nuove librerie `libs/ui/*`.
- **Nx workspace**: `docker-compose.yml`, target del database, `AGENTS.md`, agent, `doc/**`.

## 4. Impact Assessment

- **Affected Files/Modules**: come sopra, più `package.json`/`*.csproj` (nuovi pacchetti), `tsconfig.base.json` e `nx.json` (nuovi componenti spartan).
- **Breaking Changes**: No. Si aggiungono primo database e primi endpoint. Il contratto OpenAPI passa da `paths` vuoto ai cinque endpoint di `Materials`.

## 5. Decisioni

Confermate dall'utente:

1. Nome `Material` nel codice, "Materiale" in UI.
2. Solo CRUD, senza CQRS; illyW Framework e repository come in `ptconnect-nx`; nessun metodo aggiunto all'interfaccia base.
3. Campi per i materiali acquistati: `Vendor` e `VendorMaterialId`.
5. Soft-delete, mai cancellazione fisica.
6. Lotti e tracciabilità rimandati.
7. PostgreSQL con EF Core e pacchetti illyW.
8. Solo componenti spartan; form nel dialog.

Proposte del lead (da confermare):

- **Unità di misura**: enum fisso (`Piece`, `Gram`, `Kilogram`, `Meter`, `Liter`). Gli standard sono UN/ECE Recommendation 20 (usata da ISA-95/B2MML, es. `C62` pezzo, `KGM` kg) e UCUM; per le conversioni, il pacchetto NuGet più diffuso è UnitsNet. Si rimandano fino al primo requisito di conversione (le enum si mappano su UnitsNet o sui codici Rec. 20).
- **Fornitore**: due campi sul materiale. Una relazione 1-N `MaterialSupplier` con entità `Supplier` si introdurrà quando servirà più di un fornitore per materiale.
- **Mapping manuale** al posto di AutoMapper (le versioni recenti sono a pagamento, e per un CRUD è superfluo).
- **`Service` tra controller e repository** al posto degli handler CQRS, per avere il `Result` pattern senza Silverback.
- **404/409** per assenza e duplicato, 422 per la validazione (il riferimento usa 422 per tutto).
- **Database locale** con `docker-compose.yml` e migration automatiche solo in Development.
- **Nessun test automatico** in questa spec (come per il frontend).

## 6. Rischi

- Primo uso di EF Core e Npgsql nel progetto: allineare le versioni dei pacchetti a net10.0 e verificare che l'avvio dell'API non dipenda dal database durante la generazione di `openapi/Api.json` (build con `OpenApiGenerateDocuments`).
- La generazione dell'OpenAPI deve restare eseguibile senza PostgreSQL attivo.
- `nx serve web` avvia `Api:serve`: da questa spec l'API richiede il database acceso (`docker compose up`) per rispondere alle chiamate.
- Gli enum sono serializzati come stringhe: verificare che Orval generi tipi union/enum corretti.
