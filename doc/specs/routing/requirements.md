# Spec: Ciclo di lavoro (Routing) e Fasi di processo

## 1. Context & Goals

Il **ciclo di lavoro** dice **come** si produce un materiale: quali fasi, in che ordine e con quali tempi. Completa la **Product Definition** di ISA-95 (vedi `doc/glossary.md`), di cui la distinta base (spec `bom`) è la prima parte.

Il modello segue ISA-95 in forma ridotta:

- **Fase di processo** (`ProcessSegment`, ISA-95 *Process Segment*): attività riusabile e indipendente dal prodotto (es. Taglio, Saldatura, Collaudo), con tempi standard. Ha una propria anagrafica.
- **Ciclo di lavoro** di un materiale `Intermediate` o `FinishedGood`: un **grafo** di passi (`RoutingStep`, ISA-95 *Operations Segment*) legati da dipendenze (`RoutingStepDependency`, ISA-95 *Segment Dependency*). Sono ammessi **rami paralleli**, che poi convergono in un unico passo finale.
- Il ciclo si disegna e modifica con un editor a nodi basato su **ngDiagram** (`ng-diagram`), con i passi come nodi Spartan e le dipendenze come archi.

Ciclo e distinta base restano separati: il collegamento tra componente e passo (fase di consumo) e gli ordini di lavoro (che copiano il ciclo) sono in specifiche successive.

Fuori ambito: versioni e stati del ciclo, collegamento BOM-passo, risorse richieste (attrezzature, personale), tipi di dipendenza diversi da "fine → inizio" e ritardi, ordini di lavoro, undo/redo, auto-layout avanzato, test automatici.

## 2. Requirements

### REQ-1: Anagrafica Fasi di processo (backend)

- **User Story**: As a pianificatore, I want un'anagrafica di fasi riusabili con tempi standard so that i cicli di lavoro le riutilizzino.
- **Acceptance Criteria**:
  - [ ] Entità `ProcessSegment : IEntity<int>` in `libs/api-core/Persistence/Entities` con: `Id`, `Code` (obbligatorio, max 50, univoco, normalizzato trim + maiuscolo), `Name` (obbligatorio, max 200), `Description` (opzionale, max 1000), `SetupMinutes` (decimal(10,2), >= 0, default 0: attrezzaggio per ordine), `RunMinutesPerUnit` (decimal(10,4), >= 0, default 0: lavorazione per unità prodotta), `IsActive` (default `true`, soft-delete), `CreatedAt`, `UpdatedAt` (UTC).
  - [ ] `DbSet<ProcessSegment>` in `AppDbContext`, configurazione e migration `AddProcessSegments`.
  - [ ] `ProcessSegmentFeature` in `libs/api-features` con `IProcessSegmentRepository` (nessun metodo aggiunto), `ProcessSegmentService` (`Result`), `ProcessSegmentsController` con gli stessi 5 endpoint e le stesse regole di `Material`: `GET /api/process-segments?includeInactive`, `GET /api/process-segments/{id}`, `POST`, `PUT`, `DELETE` (disattivazione, mai cancellazione fisica). Codici 201, 200, 204, 404, 409 (codice duplicato), 422.
  - [ ] Contratto OpenAPI e client Angular rigenerati.
- **Technical Constraints**: stesso pattern di `Material` (`doc/structure.md` "Backend patterns"), mappatura manuale, nessun CQRS, nessun nuovo pacchetto.

### REQ-2: Modello e persistenza del ciclo (backend)

- **User Story**: As a pianificatore, I want un ciclo di lavoro con passi e dipendenze so that descriva anche rami paralleli.
- **Acceptance Criteria**:
  - [ ] Entità `RoutingStep : IEntity<int>`: `Id`, `MaterialId` (FK a `Material`), `ProcessSegmentId` (FK a `ProcessSegment`), `OperationNumber` (int > 0, univoco per materiale: 10, 20, 30...), `SetupMinutes` e `RunMinutesPerUnit` (decimal, **opzionali**: se assenti valgono i tempi standard della fase), `Notes` (opzionale, max 1000), `PositionX` e `PositionY` (double, posizione nel diagramma), `CreatedAt`, `UpdatedAt`.
  - [ ] Entità `RoutingStepDependency : IEntity<int>`: `Id`, `PredecessorStepId`, `SuccessorStepId` (FK a `RoutingStep`). Indice univoco sulla coppia. Un solo tipo di dipendenza: il successore parte quando il predecessore finisce.
  - [ ] FK con `DeleteBehavior.Restrict` (le dipendenze di un passo si eliminano esplicitamente dal servizio insieme al passo). Nessuna testata del ciclo: il ciclo di un materiale è l'insieme dei suoi passi.
  - [ ] `DbSet` in `AppDbContext` e migration `AddRouting`.
- **Technical Constraints**: come REQ-1 della spec `bom`. Nessun versioning.

### REQ-3: API del ciclo (backend)

- **User Story**: As a pianificatore, I want salvare il ciclo di un materiale in un'unica operazione so that il grafo sia sempre coerente.
- **Acceptance Criteria**:
  - [ ] `GET /api/materials/{id}/routing`: restituisce `steps` (con `id`, `processSegmentId`, codice, nome e tempi standard della fase, `operationNumber`, tempi del passo, tempi effettivi, `notes`, `positionX`, `positionY`) e `dependencies` (`id`, `predecessorStepId`, `successorStepId`). Ciclo vuoto: due liste vuote. 404 se il materiale non esiste.
  - [ ] `PUT /api/materials/{id}/routing`: sostituisce l'intero grafo in una transazione. I passi esistenti mantengono l'`id` (aggiornati per `id`), quelli mancanti nella richiesta sono eliminati con le loro dipendenze, quelli senza `id` sono creati. Le dipendenze si riferiscono ai passi tramite una chiave del client (`key`) unica nella richiesta; la risposta è il ciclo salvato, con gli `id` definitivi.
  - [ ] Regole, con 422 (`ProblemDetails.detail` o `errors`): il materiale è di tipo `Intermediate` o `FinishedGood`; `operationNumber` > 0 e univoco; fase esistente e **attiva** per i passi nuovi o con fase cambiata; tempi >= 0; nessuna dipendenza di un passo con se stesso né duplicata; il grafo è **aciclico**; se non è vuoto è **connesso** (una sola componente) e ha **un solo passo finale** (senza successori).
  - [ ] Un ciclo vuoto è valido (rimuove il ciclo).
  - [ ] 404 se il materiale o una fase non esistono.
  - [ ] Contratto OpenAPI e client Angular rigenerati.
- **Technical Constraints**: logica in `RoutingService` (`Result`) con `IRoutingStepRepository` e `IRoutingStepDependencyRepository` (senza metodi aggiunti), e `IMaterialRepository`/`IProcessSegmentRepository` per le verifiche. Salvataggio in una sola transazione (se `GenericRepository` non lo permette senza metodi aggiunti, documentare la scelta nel report). Nessun CQRS. Enum come stringa.

### REQ-4: Pagina Fasi di processo (web)

- **User Story**: As a pianificatore, I want gestire le fasi da una pagina so that le trovi come i materiali.
- **Acceptance Criteria**:
  - [ ] Pagina `/process-segments` ("Fasi di processo") in `apps/web/src/app/features/process-segments`, voce nel menu laterale, lazy route.
  - [ ] Tabella con stile standard (helm, paginatore condiviso, menu `…`, vedi `doc/structure.md` "Tables"): colonne Codice, Nome, Attrezzaggio (min), Tempo per unità (min), Stato. Ricerca, "Mostra disattivati", "Nuova fase" (toolbar), "Modifica" e "Disattiva" (alert-dialog) dal menu.
  - [ ] Dialog di creazione e modifica con gli errori 409 e 422 mostrati nel dialog.
  - [ ] Stati: skeleton, errore con "Riprova", vuoto, nessun risultato.
- **Technical Constraints**: stessa struttura di `features/materials` (store a signals, form dialog, row actions nello stesso template del menu). Solo componenti Spartan helm. Nessun componente `shared` nuovo se non giustificato.

### REQ-5: Editor del ciclo con ngDiagram (web)

- **User Story**: As a pianificatore, I want disegnare il ciclo come un diagramma so that vedo subito sequenze e rami paralleli.
- **Acceptance Criteria**:
  - [ ] Pagina `/materials/:id/routing` ("Ciclo di lavoro") in `apps/web/src/app/features/routing`, lazy route, con intestazione (codice e nome del materiale, link "← Materiali"), pulsanti "Salva" e "Annulla modifiche" e indicazione di modifiche non salvate.
  - [ ] Il diagramma usa `ng-diagram`: nodi con un componente Angular (numero operazione, codice e nome della fase, tempi effettivi come badge), archi orientati dal predecessore al successore, trascinamento dei nodi, creazione degli archi tra le porte, eliminazione di nodi e archi selezionati, pan e zoom, minimap.
  - [ ] Elenco delle fasi attive come palette (ngDiagram o elenco con "Aggiungi"): aggiunge un nodo al diagramma con il prossimo `operationNumber` libero (multipli di 10). Una stessa fase può comparire in più passi.
  - [ ] Pannello di modifica del passo selezionato: numero operazione, tempi che sostituiscono quelli standard (con indicazione del valore standard), note. Componenti form Spartan.
  - [ ] Vista alternativa in sola lettura, accessibile da tastiera: tabella dei passi con predecessori e successori.
  - [ ] Salvataggio con `PUT` dell'intero grafo; gli errori 422/404 sono mostrati in un messaggio visibile con il testo del backend. Dopo il salvataggio il diagramma mostra gli `id` definitivi.
  - [ ] Convalida lato client prima del salvataggio, con messaggi in italiano: cicli, passo finale unico, grafo connesso, numeri operazione duplicati. Il backend resta l'autorità.
  - [ ] I passi senza posizione salvata (`positionX/Y` nulle) ricevono un layout iniziale a strati (da sinistra a destra per profondità).
  - [ ] Stati: skeleton, errore con "Riprova", ciclo vuoto con invito ad aggiungere la prima fase. Avviso se si lascia la pagina con modifiche non salvate.
  - [ ] Voce "Ciclo di lavoro" nel menu `…` della tabella Materiali, solo per `Intermediate` e `FinishedGood`, dopo "Distinta base".
- **Technical Constraints**: `ng-diagram` aggiunto da `[Nx]` con versione esatta e tema/stili integrati con i token semantici (variabili CSS), nessun colore fisso né `dark:`. Nodi e pannelli con componenti Spartan helm (badge, input, label, button, card se esiste, altrimenti elementi con token). I componenti custom (nodo del diagramma, calcolo del layout) sono ammessi solo perché necessari: motivarli nel report. Il client Orval generato non si modifica. Lo stato dell'editor è locale alla pagina (signals); nessun altro store globale.

### REQ-6: Documentazione e vocabolario

- **User Story**: As a team, I want vocabolario e scelte documentati so that ordini di lavoro e risorse partano da una base chiara.
- **Acceptance Criteria**:
  - [ ] `doc/glossary.md`: aggiunta di `ProcessSegment` (Process Segment, UI "Fase di processo"), `RoutingStep` (Operations Segment, UI "Passo") e `RoutingStepDependency` (Segment Dependency, UI "Dipendenza"); "Product Definition" aggiornata (BOM e ciclo adottati; collegamento BOM-passo e risorse ancora pianificati).
  - [ ] Deviazioni da ISA-95 registrate: nessuna testata né versioning; un solo tipo di dipendenza (fine → inizio) senza ritardo; tempi standard nella fase con sostituzione per passo; nessuna risorsa richiesta; passo finale unico; posizione grafica (X, Y) salvata sul passo.
  - [ ] `doc/structure.md`: nota su `ng-diagram` (installazione, integrazione stile, salvataggio dell'intero grafo con `PUT`) e sulla struttura `features/routing`.
- **Technical Constraints**: solo file in `doc/`.

### REQ-7: Dati di esempio realistici (backend, solo Development)

- **User Story**: As a team, I want dati di esempio credibili so that l'app si possa provare e mostrare con materiali, distinte e cicli reali.
- **Acceptance Criteria**:
  - [ ] I dati di prova esistenti nel DB locale (materiali e righe BOM creati durante lo sviluppo e i test) sono eliminati: il DB locale è azzerato e ricreato dalle migration.
  - [ ] Un seeder eseguito **solo in Development**, dopo le migration, inserisce i dati di esempio **solo se la tabella `Materials` è vuota** (idempotente, mai in produzione, mai durante la generazione OpenAPI). Il seeder è codice di infrastruttura e può usare `AppDbContext` direttamente.
  - [ ] Scenario: piccola officina che produce arredi in acciaio e rovere. Codici e nomi in italiano, fornitori plausibili, unità di misura coerenti con l'enum esistente.
    - **Materie prime e consumabili** (circa 12): tubolare in acciaio quadro (m), lamiera S235 (kg), tavola di rovere (pz), vernice in polvere nera (kg), vernice acquosa trasparente (l), viti, piedini in gomma, scatola di imballo (pz), filo di saldatura, dischi da taglio e carta abrasiva (consumabili). Con `Vendor` e `VendorMaterialId` per i materiali acquistati. Almeno un materiale disattivato (es. un tubolare sostituito), per mostrare "Mostra disattivati".
    - **Semilavorati**: struttura del tavolino saldata e verniciata, piano del tavolino in rovere finito.
    - **Prodotti finiti**: un tavolino da caffè e una sedia.
    - **BOM multilivello**: il tavolino contiene i due semilavorati più viti, piedini e imballo; i semilavorati hanno ognuno la propria BOM di materie prime; la sedia ha una BOM di sole materie prime. Quantità realistiche (es. metri di tubolare, kg di vernice).
    - **Fasi di processo** (circa 10): taglio tubolare, taglio e piegatura lamiera, saldatura, sbavatura, verniciatura a polvere, fresatura legno, levigatura, finitura legno, assemblaggio, collaudo, imballaggio. Tempi standard di attrezzaggio e per unità plausibili (es. verniciatura con attrezzaggio più lungo).
    - **Cicli di lavoro**: struttura del tavolino lineare (taglio → saldatura → sbavatura → verniciatura); piano del tavolino lineare (fresatura → levigatura → finitura); tavolino finito lineare (assemblaggio → collaudo → imballaggio); la **sedia con rami paralleli** (taglio tubolare e taglio e piegatura lamiera in parallelo, che convergono nella saldatura, poi sbavatura → verniciatura → assemblaggio → collaudo → imballaggio). Alcuni passi con tempi diversi da quelli standard e con note. Posizioni X/Y per un diagramma ordinato.
  - [ ] I dati rispettano le regole delle API (codici univoci e maiuscoli, tipi validi per BOM e ciclo, grafi aciclici, connessi e con un solo passo finale).
  - [ ] `doc/structure.md` descrive come azzerare e ricreare i dati di esempio in locale (`docker compose down -v`, poi `npx nx run Api:db-up`).
- **Technical Constraints**: il seeder sta in `libs/api-core/Persistence` (o accanto a `Program.cs`) e viene richiamato da `Program.cs` solo in Development. Dati scritti come codice C# leggibile, senza file esterni. Nessuna dipendenza nuova.

## 3. Layers impacted

- **Backend**: nuove entità `ProcessSegment`, `RoutingStep`, `RoutingStepDependency`, due migration, `ProcessSegmentFeature` e `RoutingFeature`, seeder dei dati di esempio (solo Development), contratto OpenAPI rigenerato.
- **Web**: feature `process-segments`, feature `routing` con editor ngDiagram, voci di menu (laterale e riga Materiali), route.
- **Nx workspace**: installazione di `ng-diagram` (versione esatta) e integrazione degli stili, senza nuovi progetti.

## 4. Impact Assessment

- **File interessati**: `libs/api-core/Persistence` (entità, contesto, migration), nuove cartelle `libs/api-features/ProcessSegmentFeature` e `RoutingFeature`, `apps/api/Program.cs`, `openapi/Api.json` e client generato, `package.json` e lockfile, configurazione stili di `apps/web`, `apps/web/src/app/features/{process-segments,routing}`, `app.routes.ts`, `sidebar-data.ts`, `material-row-actions.component.ts`, `doc/glossary.md`, `doc/structure.md`.
- **Breaking changes**: no. Si aggiungono tabelle, endpoint e pagine.
- **Rischi**:
  - `ng-diagram` è nuovo nel progetto (1.4.0, licenza Apache-2.0, peer `@angular/core >=18`): la compatibilità con Angular 22.1 è dichiarata dai peer ma non provata. Prima dell'editor completo si verifica con una prova minima.
  - Mancano undo/redo e layout automatico: il layout iniziale è un calcolo semplice, e l'accessibilità da tastiera è coperta dalla vista tabella.
  - Il salvataggio dell'intero grafo con `PUT` semplifica il frontend, ma gli `id` dei passi devono restare stabili: i collegamenti futuri (BOM-passo, ordini di lavoro) li useranno.
  - L'insieme è ampio (3 entità, 2 pagine, un editor). Se serve, si divide in due spec: Fasi (REQ-1, REQ-4) e Ciclo (REQ-2, REQ-3, REQ-5).
