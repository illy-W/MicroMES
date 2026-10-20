# Spec: Distinta base (BOM)

## 1. Context & Goals

La **distinta base** (Bill of Materials, BOM) dice da quali materiali, e in che quantità, è composto un materiale prodotto (semilavorato o prodotto finito). In ISA-95 fa parte della **Product Definition** (vedi `doc/glossary.md`); nella UI si chiama **"Distinta base"**.

È la prima parte della Product Definition. Ciclo di lavoro e ordini di lavoro seguiranno in spec separate, e useranno la BOM.

La BOM è **multilivello**: un componente può essere a sua volta un semilavorato con la propria BOM. Si mostra come **albero** con la tabella helm e le righe espandibili di TanStack Table v9, senza nuovi componenti spartan né componenti custom, perché in Spartan non esiste un componente ad albero. Si apre da una pagina dedicata `/materials/:id/bom`, raggiunta dal menu `…` della tabella Materiali. In futuro la pagina diventerà una scheda (tab) di un dettaglio materiale.

Fuori ambito: versioni e stati della BOM, controllo dei cicli (un materiale che contiene se stesso, anche indirettamente), percentuale di scarto, quantità cumulate lungo l'albero, costi, ciclo di lavoro, ordini di lavoro, lotti, paginazione lato server, test automatici.

## 2. Requirements

### REQ-1: Modello dati e persistenza

- **User Story**: As a sviluppatore, I want un'entità per le righe di BOM so that ogni materiale prodotto possa elencare i propri componenti.
- **Acceptance Criteria**:
  - [ ] Entità `BomItem : IEntity<int>` in `libs/api-core/Persistence/Entities/BomItem.cs` con:
    - `Id` (int);
    - `ParentMaterialId` (FK a `Material`, obbligatorio): il materiale prodotto;
    - `ComponentMaterialId` (FK a `Material`, obbligatorio): il componente;
    - `Quantity` (decimal(18,4), obbligatorio, > 0): quantità di componente per **1 unità** del materiale prodotto, espressa nell'unità di misura del componente;
    - `CreatedAt`, `UpdatedAt` (UTC).
  - [ ] Indice univoco su (`ParentMaterialId`, `ComponentMaterialId`): un componente compare una sola volta per BOM.
  - [ ] Le FK usano `DeleteBehavior.Restrict`. `Material` non si elimina mai fisicamente, quindi non esistono cancellazioni a cascata.
  - [ ] `DbSet<BomItem> BomItems` in `AppDbContext` e configurazione in `OnModelCreating`.
  - [ ] Nuova migration EF Core (`AddBomItems`) in `libs/api-core/Persistence/Migrations`.
- **Technical Constraints**: stesso pattern di `Material` (chiave `int`, nessun nuovo pacchetto). Dipendenze `Api` → `ApiFeatures` → `ApiCore` invariate. Nessuna entità "testata" della BOM: la BOM di un materiale è l'insieme delle sue righe. Quando arriverà il versioning si introdurrà la testata con una migration dedicata.

### REQ-2: Repository

- **User Story**: As a sviluppatore, I want un repository per `BomItem` so that nessun codice di dominio usi direttamente `AppDbContext`.
- **Acceptance Criteria**:
  - [ ] `IBomItemRepository : IGenericRepository<BomItem, int>` e `BomItemRepository : GenericRepository<BomItem, int, AppDbContext>` in `libs/api-features/BomFeature/Repositories`.
  - [ ] L'interfaccia **non aggiunge metodi** rispetto a `IGenericRepository`: solo `GetSingleAsync`, `FetchAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`.
  - [ ] Registrazione con `services.AddRepository<IBomItemRepository, BomItemRepository>()` in `BomFeature/Registrar.cs`, richiamata da `Program.cs`.
- **Technical Constraints**: come REQ-2 della spec `material`. Per leggere e validare i materiali, `BomService` inietta `IMaterialRepository` (già registrato) senza modificarlo.

### REQ-3: API della BOM

- **User Story**: As a pianificatore, I want leggere e modificare la BOM di un materiale so that la composizione sia sempre aggiornata.
- **Acceptance Criteria**:
  - [ ] `GET /api/materials/{id}/bom`: restituisce l'albero della BOM del materiale. Ogni nodo contiene `id` (della riga), `componentMaterialId`, `code`, `name`, `type`, `unitOfMeasure`, `quantity`, `isActive` del componente e `children`: le righe della BOM del componente, a loro volta annidate. Un componente senza BOM ha `children` vuoto. 404 se il materiale non esiste.
  - [ ] Per evitare cicli infiniti, finché manca il controllo dei cicli, la lettura si ferma a 10 livelli di profondità.
  - [ ] `POST /api/materials/{id}/bom/items` con `componentMaterialId` e `quantity`: aggiunge un componente. 201 con la riga creata.
  - [ ] `PUT /api/bom-items/{id}` con `quantity`: modifica la quantità. 404 se non esiste.
  - [ ] `DELETE /api/bom-items/{id}`: rimuove la riga dalla BOM (cancellazione fisica: è una relazione, non un'anagrafica). 204. 404 se non esiste.
  - [ ] Regole sul POST: il materiale prodotto deve essere di tipo `Intermediate` o `FinishedGood`; il componente deve esistere ed essere attivo; il componente non può coincidere con il materiale prodotto; `quantity` > 0; la coppia (prodotto, componente) non deve già esistere. Violazione: 422 se i dati non sono validi, 409 se il componente è già presente, 404 se il materiale o il componente non esistono.
  - [ ] La logica sta in `BomService` (pattern `Result`, come `MaterialService`), mappatura manuale, nessun CQRS.
  - [ ] Il contratto OpenAPI e il client Angular sono rigenerati (`npx nx run api-client:generate-api`).
- **Technical Constraints**: pattern backend di `doc/structure.md` ("Backend patterns"): controller → `BomService` → repository. Il controller espone le route sopra in un `BomController` nella feature `BomFeature`. Enum serializzati come stringa. Nessun controllo dei cicli oltre al caso "componente = prodotto".

### REQ-4: Pagina BOM con albero

- **User Story**: As a pianificatore, I want vedere la BOM di un materiale come albero so that capisca da cosa è composto, anche a più livelli.
- **Acceptance Criteria**:
  - [ ] Route `/materials/:id/bom`, caricata in lazy loading, nella feature `apps/web/src/app/features/bom`.
  - [ ] L'intestazione mostra codice e nome del materiale, un link per tornare all'elenco Materiali e il pulsante "Aggiungi componente".
  - [ ] La BOM è una tabella con le direttive helm (stesso stile della tabella Materiali, vedi `doc/structure.md` "Tables"), con TanStack Table v9 e righe espandibili per i sotto-componenti (`getSubRows` e feature di espansione). Un pulsante con chevron espande e comprime i nodi, con `aria-expanded` e un'indentazione per livello.
  - [ ] Colonne: Componente (codice e nome, con chevron e indentazione), Tipo (badge outline), Quantità (allineata a destra, classe `num`), Unità, Stato (badge outline: i componenti disattivati sono evidenziati), Azioni.
  - [ ] Le righe del primo livello hanno il menu `…` con "Modifica quantità" e "Rimuovi" (stile destructive, con conferma in alert-dialog). I sotto-livelli sono di sola lettura: la loro BOM si modifica dalla pagina del componente, e la riga ha un link "Apri distinta".
  - [ ] "Aggiungi componente" apre un dialog con `hlm-select` dei materiali attivi (escluso il materiale stesso) e il campo quantità. Gli errori 409 e 422 sono mostrati nel dialog.
  - [ ] Stati di caricamento (skeleton), errore con "Riprova", BOM vuota (`hlm-empty` con invito ad aggiungere un componente). I testi sono in italiano.
  - [ ] Il paginatore non serve: l'albero è completo e non paginato.
- **Technical Constraints**: spartan helm per ogni elemento UI (tabella, badge, dialog, alert-dialog, select, dropdown-menu, empty, skeleton, button, label). Nessun nuovo componente generato, nessun componente custom se non necessario: se ne serve uno, motivarlo nel report. Classi semantiche, nessun colore fisso, nessun `dark:`. Il client Orval generato non si modifica. Un servizio store a signals come in `features/materials`.

### REQ-5: Accesso dalla tabella Materiali

- **User Story**: As a pianificatore, I want aprire la BOM dalla riga del materiale so that non debba cercarla altrove.
- **Acceptance Criteria**:
  - [ ] Il menu `…` della tabella Materiali ha la voce "Distinta base", prima di "Modifica", solo per i materiali di tipo `Intermediate` o `FinishedGood`.
  - [ ] La voce naviga a `/materials/:id/bom`.
  - [ ] Il comportamento esistente della tabella Materiali non cambia.
- **Technical Constraints**: il menu è `material-row-actions.component.ts`; le voci restano nello stesso template dell'`ng-template #menu`.

### REQ-6: Documentazione e vocabolario

- **User Story**: As a team, I want il vocabolario e le scelte documentati so that le prossime feature (ciclo, ordini) partano da una base chiara.
- **Acceptance Criteria**:
  - [ ] `doc/glossary.md`: aggiunta di `BomItem` (UI "Distinta base" / "Componente"), mappata sulla Product Definition di ISA-95. La riga "Product Definition" resta in "Planned" come parziale (ciclo mancante).
  - [ ] Deviazioni da ISA-95 registrate: nessuna testata né versioning; nessun controllo dei cicli; quantità per 1 unità senza scarto; righe cancellate fisicamente.
  - [ ] `doc/structure.md`: nota sul pattern "tabella ad albero" (TanStack con righe espandibili) e sulla pagina `/materials/:id/bom`.
- **Technical Constraints**: solo file in `doc/`.

## 3. Layers impacted

- **Backend**: nuova entità `BomItem`, migration, `BomFeature` (repository, service, controller, registrazione), contratto OpenAPI rigenerato.
- **Web**: feature `bom` (pagina, store, dialog), voce nel menu `…` dei Materiali, nuova route. Nessuna nuova libreria `libs/ui`.
- **Nx workspace**: nessuna modifica prevista (nessun nuovo progetto né dipendenza).

## 4. Impact Assessment

- **File interessati**: `libs/api-core/Persistence` (entità, contesto, migration), nuova cartella `libs/api-features/BomFeature`, `apps/api/Program.cs`, `openapi/Api.json` e `libs/shared/api-client/src/generated` (rigenerati), `apps/web/src/app/features/bom`, `apps/web/src/app/features/materials/material-row-actions.component.ts`, `apps/web/src/app/app.routes.ts`, `doc/glossary.md`, `doc/structure.md`.
- **Breaking changes**: no. Le API esistenti non cambiano; si aggiungono endpoint e una tabella.
- **Rischi**:
  - Senza controllo dei cicli un utente può creare un ciclo (A contiene B e B contiene A). La lettura è protetta da un limite di 10 livelli, ma l'albero mostrerebbe dati ripetuti. Il controllo è rimandato per scelta.
  - L'API di espansione delle righe di TanStack v9 non è stata ancora verificata a fondo: se non copre il caso, si usa uno stato di espansione gestito a signal nella pagina.
