# Spec: Ordini di produzione e ordini di lavoro

## 1. Context & Goals

Finora MicroMES ha solo dati anagrafici: materiali, distinta base, fasi di processo e cicli di lavoro (la *Product Definition* di ISA-95). Questa spec chiude il ciclo **pianifica → rilascia → esegui → registra** con gli oggetti di richiesta e di consuntivo di ISA-95.

### Riferimento ISA-95 (verificato)

Verificato sulla specifica pubblica *OPC UA for ISA-95, Part 4: Job Control* v2.00.0 (https://reference.opcfoundation.org/specs/OPC-10031-4), che riporta il modello di ISA-95 parte 4:

- Un **Work Schedule** contiene i **Work Request**, divisibili per quantità (es. 1.000.000 bottiglie in 4 richieste da 250.000). Ogni Work Request contiene uno o più **Job Order**, l'oggetto schedulato di livello più basso: una unità di lavoro da eseguire in un work center.
- **Job Order**: `JobOrderID`, `Description`, `WorkMasterID` (la procedura da seguire), `StartTime`/`EndTime` proposti, `Priority` (numero più alto = più priorità), parametri, requisiti di personale, attrezzature, beni fisici e **materiali**.
- **Material Requirement**: riferisce una classe, una definizione, un lotto o un sotto-lotto di materiale, con **uso** (*consumable*, *material consumed*, *material produced*, *co-product*, *by-product*), quantità e unità di misura.
- **Job Response**: il consuntivo di un Job Order: `JobResponseID`, `JobOrderID`, `StartTime`/`EndTime` effettivi, **stato**, dati di risposta e attuali di personale, attrezzature e materiali (consumati e prodotti).
- **Stati del job order** (OPC UA): *NotAllowedToStart* e *AllowedToStart* (preparato, non ancora in esecuzione, con sottostati come *Waiting*/*Ready*), **Running**, **Interrupted** (*Suspended* o *Held*), **Ended** e **Aborted**. Comandi: *Store*, *Start* (consente l'avvio), *RevokeStart*, *Pause*, *Resume*, *Update*, *Stop* (chiude l'esecuzione prima della quantità pianificata), *Abort*, *Cancel* (solo se non avviato), *Clear*.

### Come si mappa su MicroMES (forma ridotta)

| ISA-95 | MicroMES | UI |
|---|---|---|
| Work Request / Production Request | `ProductionOrder` | Ordine di produzione |
| Job Order (Segment Requirement) | `WorkOrder` (uno per passo del ciclo) | Ordine di lavoro |
| Dipendenze tra segmenti | `WorkOrderDependency` | (dipendenza) |
| Material Requirement (*material consumed*) | `WorkOrderMaterial` | Materiale richiesto |
| Job Response | `WorkOrderResponse` (una per sessione di lavoro) | Registrazione |

Decisioni di prodotto già prese:

- L'ordine è un'**istantanea** creata **al rilascio**: copia ciclo e distinta in quel momento; modificarli dopo non cambia gli ordini già rilasciati.
- **Quantità** nei tempi: tempo pianificato = attrezzaggio + tempo per unità × quantità dell'ordine.
- **Più registrazioni per lavoro**: un ordine di lavoro può avere più sessioni (avvii, pause, riprese); il totale è la somma.
- **Stati** ridotti, derivati da ISA-95 (vedi REQ-5 e REQ-6).
- **Semilavorati**: al rilascio si creano **ordini di produzione figli** collegati al padre, ognuno con i propri ordini di lavoro.

Fuori ambito (specifiche successive): vista dedicata all'operatore di reparto, consumi **effettivi** di materiale, lotti e tracciabilità, risorse richieste (personale, attrezzature), parametri di produzione, pianificazione automatica delle date, collegamento BOM-passo, ordini con più righe, autenticazione e ruoli, calcolo della capacità, vista a diagramma degli ordini. Test automatici.

## 2. Requirements

### REQ-1: Modello dati

- **User Story**: As a sviluppatore, I want le entità degli ordini persistite so that le funzioni successive le possano riusare.
- **Acceptance Criteria**:
  - [ ] `ProductionOrder : IEntity<int>`: `Id`, `Code` (generato, univoco, max 50), `MaterialId` (FK), `Quantity` (decimal(18,4) > 0), `Status` (`Planned`, `Released`, `InProgress`, `Completed`, `Cancelled`, `Aborted`; stringa), `Priority` (int, default 0, più alto = più urgente), `RequestedStart` e `RequestedEnd` (date/ora UTC, opzionali), `Notes` (max 1000, opzionale), `ParentOrderId` (FK a `ProductionOrder`, opzionale: ordine padre), `ReleasedAt`, `StartedAt`, `EndedAt` (UTC, opzionali), `CreatedAt`, `UpdatedAt`.
  - [ ] `WorkOrder : IEntity<int>`: `Id`, `ProductionOrderId` (FK), `OperationNumber` (int > 0, univoco per ordine), `ProcessSegmentId` (FK) con copia di `ProcessSegmentCode` e `ProcessSegmentName`, `SetupMinutes` e `RunMinutesPerUnit` (copiati dal passo, tempi **effettivi** al momento del rilascio), `PlannedQuantity` (= quantità dell'ordine), `Notes`, `Status` (`Waiting`, `Ready`, `Running`, `Interrupted`, `Ended`, `Aborted`; stringa), `StartedAt`, `EndedAt` (UTC, opzionali), `CreatedAt`, `UpdatedAt`.
  - [ ] `WorkOrderDependency`: `Id`, `PredecessorWorkOrderId`, `SuccessorWorkOrderId` (FK a `WorkOrder`; possono appartenere a ordini diversi). Indice univoco sulla coppia.
  - [ ] `WorkOrderMaterial`: `Id`, `WorkOrderId`, `MaterialId` (FK), `RequiredQuantity` (decimal(18,4)), `ChildProductionOrderId` (FK opzionale: ordine figlio che produce questo componente). Uso implicito: *material consumed*.
  - [ ] `WorkOrderResponse`: `Id`, `WorkOrderId`, `StartedAt` (UTC), `EndedAt` (UTC, vuoto finché la sessione è aperta), `GoodQuantity` e `ScrapQuantity` (decimal(18,4) >= 0, valorizzate alla chiusura), `Notes` (max 1000).
  - [ ] `DbSet` in `AppDbContext`, configurazione e migration `AddProductionOrders`. FK con `DeleteBehavior.Restrict`. Nessuna cancellazione fisica nell'API.
- **Technical Constraints**: stesso pattern di `Material` e `RoutingStep` (chiave `int`, stringhe per gli enum, UTC). Nessun nuovo pacchetto.

### REQ-2: Repository

- **User Story**: As a sviluppatore, I want i repository vuoti so that la logica non usi direttamente `AppDbContext`.
- **Acceptance Criteria**:
  - [ ] `IProductionOrderRepository`, `IWorkOrderRepository`, `IWorkOrderDependencyRepository`, `IWorkOrderMaterialRepository`, `IWorkOrderResponseRepository`, ciascuno `IGenericRepository<T, int>` senza metodi aggiunti, con le implementazioni e la registrazione in `ProductionOrderFeature/Registrar.cs` (richiamata da `Program.cs`).
  - [ ] Operazioni atomiche (rilascio, cambi di stato) in una transazione con `IUnitOfWork` di illyW, come `RoutingService`.
- **Technical Constraints**: come REQ-2 della spec `material`. Pacchetto NuGet `Stateless` con versione esatta (la stessa del riferimento `ptconnect-nx`, 5.20.1, se compatibile con net10.0) in `ApiFeatures`. I servizi leggono materiali, BOM e cicli tramite i repository esistenti (`IMaterialRepository`, `IBomItemRepository`, `IRoutingStepRepository`, `IRoutingStepDependencyRepository`) senza modificarli.

### REQ-3: Ordini di produzione (API di base)

- **User Story**: As a pianificatore, I want creare e modificare ordini di produzione so that pianifichi cosa produrre.
- **Acceptance Criteria**:
  - [ ] `GET /api/production-orders`: elenco, con filtri `status`, `materialId`, `search` (codice o materiale); per default solo gli ordini **radice** (senza padre); `includeChildren=true` li include. Ogni elemento riporta codice, materiale (codice, nome, unità), quantità, stato, priorità, date, quantità prodotta (somma del buono dell'ultimo ordine di lavoro) e avanzamento.
  - [ ] `GET /api/production-orders/{id}`: dettaglio con ordini di lavoro (con stato, tempi pianificati e reali), dipendenze, materiali richiesti (con l'eventuale ordine figlio), ordini figli e riepilogo dei tempi. 404 se non esiste.
  - [ ] `POST /api/production-orders`: crea un ordine `Planned` con `materialId`, `quantity`, `priority`, `requestedStart`, `requestedEnd`, `notes`. Il codice è generato (`OP-{anno}-{progressivo a 4 cifre}`). Regole: il materiale esiste, è attivo e di tipo `Intermediate` o `FinishedGood`; quantità > 0; `requestedEnd` non precedente a `requestedStart`. 201 con l'ordine; 404 se il materiale non esiste; 422 per le regole.
  - [ ] `PUT /api/production-orders/{id}`: modifica quantità, priorità, date e note, solo se `Planned` (non il materiale). 409 se lo stato non lo consente; 422 per le regole; 404.
  - [ ] Gli ordini figli non si creano né si modificano direttamente (nessun endpoint dedicato).
  - [ ] Contratto OpenAPI e client Angular rigenerati.
- **Technical Constraints**: pattern backend di `doc/structure.md` ("Backend patterns"): controller → `ProductionOrderService` (`Result`, `Failure` per 404/409) → repository vuoti. Mappatura manuale, nessun CQRS, enum come stringa, 422 per `ModelState` non valido.

### REQ-4: Rilascio e generazione degli ordini di lavoro

- **User Story**: As a pianificatore, I want rilasciare un ordine so that venga scomposto in lavori da eseguire.
- **Acceptance Criteria**:
  - [ ] `POST /api/production-orders/{id}/release` (solo da `Planned`) in un'unica transazione:
    - verifica che il materiale abbia un ciclo non vuoto (altrimenti 422 "Il materiale non ha un ciclo di lavoro");
    - crea un `WorkOrder` per ogni passo del ciclo, copiando numero operazione, fase, tempi effettivi e note, con `PlannedQuantity` = quantità dell'ordine, e le `WorkOrderDependency` corrispondenti alle dipendenze del ciclo;
    - crea un `WorkOrderMaterial` per ogni riga della BOM del materiale con `RequiredQuantity` = quantità BOM × quantità dell'ordine, collegato all'ordine di lavoro del **primo passo iniziale** (numero operazione più basso tra i passi senza predecessori);
    - per ogni componente di tipo `Intermediate` (o `FinishedGood`) che ha un ciclo non vuoto, crea un **ordine figlio** (`ParentOrderId` = ordine, stato `Released`, quantità = `RequiredQuantity`, codice `{codice padre}-{n}`, stesse regole di scomposizione, ricorsivo), collegato al `WorkOrderMaterial`; per i componenti senza ciclo non si crea alcun figlio (restano materiali da magazzino);
    - aggiunge una dipendenza tra l'**ultimo passo** di ogni ordine figlio e i **passi iniziali** del padre (finché non esiste il legame BOM-passo, tutti i passi iniziali del padre aspettano i semilavorati);
    - imposta gli stati iniziali: `Ready` per i lavori senza predecessori, `Waiting` per gli altri; l'ordine passa a `Released` con `ReleasedAt`.
  - [ ] Profondità massima 10 livelli: se la BOM contiene un ciclo (es. A contiene B e B contiene A) il rilascio fallisce con 422 "Distinta base ciclica" senza modificare nulla.
  - [ ] Dopo il rilascio il ciclo e la BOM del materiale possono cambiare senza effetti sull'ordine.
  - [ ] Il tempo pianificato di un lavoro è attrezzaggio + tempo per unità × quantità; il dettaglio dell'ordine riporta il **contenuto di lavoro** (somma) e la **durata minima stimata** (percorso più lungo tra lavori e ordini figli, come in `routing-metrics`).
  - [ ] 404 se l'ordine non esiste; 409 se non è `Planned`.
- **Technical Constraints**: nessuna chiamata API tra servizi: il rilascio legge i dati dai repository. Calcoli puri e separati (es. `ProductionOrderPlanner`) per facilitare i test futuri.

### REQ-5: Stati e comandi dell'ordine di lavoro

- **User Story**: As a operatore, I want avviare, mettere in pausa e chiudere un lavoro so that il sistema registri come procede la produzione.
- **Acceptance Criteria**:
  - [ ] Stati: `Waiting` (rilasciato, predecessori non terminati), `Ready` (predecessori tutti `Ended`), `Running`, `Interrupted`, `Ended`, `Aborted`. Un lavoro passa da `Waiting` a `Ready` automaticamente quando tutti i suoi predecessori sono `Ended`.
  - [ ] **Macchina a stati con `Stateless`** (`WorkOrderMachineState` in `ProductionOrderFeature/MachineStates`, stesso schema di `CheckMachineState` in `ptconnect-nx`: stato letto e scritto sull'entità con accessor/mutator, `TimeProvider` per i tempi). Nessuna transizione fuori dalla macchina: i servizi non assegnano `Status` direttamente.
    - Trigger: `Start`, `Pause`, `Resume`, `Stop`, `Abort`, più il trigger interno `PredecessorsEnded`.
    - Transizioni: `Waiting` →`PredecessorsEnded`→ `Ready` →`Start`→ `Running` →`Pause`→ `Interrupted` →`Resume`→ `Running`; `Running`/`Interrupted` →`Stop`→ `Ended`; `Abort` da qualsiasi stato non terminale → `Aborted`. `Ended` e `Aborted` sono terminali. Ogni altra combinazione è vietata.
    - **Sottostati**: gli stati non terminali sono sottostati di un superstato `Open` (che permette `Abort`), e `Waiting`/`Ready` di un superstato `Prepared`, come nella gerarchia di OPC UA/ISA-95 (*AllowedToStart*, *Interrupted*). I superstati servono solo alla configurazione: non sono mai salvati né esposti. La macchina usa un enum interno (stati foglia più superstati) mappato su `WorkOrderStatus`, che contiene solo gli stati foglia.
    - `Pause` e `Stop` sono trigger con parametri (`goodQuantity`, `scrapQuantity`, `notes`); le azioni di ingresso aprono e chiudono le `WorkOrderResponse` e impostano `StartedAt`/`EndedAt`. Gli effetti vanno sugli stati foglia (le `OnEntry` dei superstati non scattano tra sottostati).
    - Una transizione non consentita non lancia un'eccezione al chiamante: il servizio usa `CanFire` e risponde 409 con un messaggio italiano che indica lo stato attuale e le azioni possibili.
    - L'API espone `allowedActions` (le azioni oggi consentite, da `GetPermittedTriggers`) sul lavoro e sull'ordine, come unica fonte per i comandi della UI.
  - [ ] `POST /api/work-orders/{id}/start` (da `Ready`): crea una `WorkOrderResponse` aperta (`StartedAt` = adesso), imposta `Running`, `StartedAt` del lavoro (solo la prima volta) e, se è il primo lavoro avviato, porta l'ordine a `InProgress`.
  - [ ] `POST /api/work-orders/{id}/pause` (da `Running`), con corpo `goodQuantity`, `scrapQuantity`, `notes`: chiude la sessione aperta con quantità e imposta `Interrupted`.
  - [ ] `POST /api/work-orders/{id}/resume` (da `Interrupted`): apre una nuova sessione e imposta `Running`.
  - [ ] `POST /api/work-orders/{id}/stop` (da `Running` o `Interrupted`), con corpo come `pause`: chiude la sessione (se aperta), imposta `Ended` con `EndedAt`, e rende `Ready` i successori i cui predecessori sono tutti `Ended`. La quantità buona può essere inferiore a quella pianificata (come *Stop* in ISA-95).
  - [ ] `POST /api/work-orders/{id}/abort` (da `Waiting`, `Ready`, `Running` o `Interrupted`), con `notes` opzionale: chiude l'eventuale sessione aperta (quantità a 0 se non indicate) e imposta `Aborted`. I successori restano `Waiting`.
  - [ ] Quantità >= 0; la somma dei buoni di un lavoro può superare la pianificata (viene mostrata, non bloccata). Transizioni non consentite: 409 con messaggio chiaro; 404; 422 per quantità non valide.
  - [ ] `GET /api/work-orders` (elenco con filtri `status`, `productionOrderId`, `search`) e `GET /api/work-orders/{id}` (dettaglio con registrazioni, materiali richiesti e predecessori/successori), con tempo reale lavorato (somma delle sessioni) e quantità totali.
  - [ ] Contratto OpenAPI e client Angular rigenerati.
- **Technical Constraints**: ogni comando è atomico (`IUnitOfWork`). Tutta la logica di transizione sta in `WorkOrderService`, con una tabella delle transizioni esplicita e riusabile. Nessun consumo effettivo di materiale in questa spec.

### REQ-6: Stati dell'ordine di produzione

- **User Story**: As a pianificatore, I want che lo stato dell'ordine rifletta i suoi lavori so that sappia a che punto è.
- **Acceptance Criteria**:
  - [ ] Stati: `Planned` → `Released` → `InProgress` → `Completed`, oppure `Cancelled` o `Aborted`.
  - [ ] `InProgress` quando almeno un lavoro è stato avviato. `Completed` (con `EndedAt`) quando **tutti** i lavori dell'ordine sono `Ended`; se l'ordine ha un padre, la fine dell'ultimo lavoro sblocca i passi iniziali del padre.
  - [ ] `POST /api/production-orders/{id}/cancel`: da `Planned`, oppure da `Released` se nessun lavoro dell'ordine e dei figli è stato avviato; porta a `Cancelled`, annulla i lavori e gli ordini figli. 409 altrimenti.
  - [ ] `POST /api/production-orders/{id}/abort` (con `notes` opzionale): da `Released` o `InProgress`; porta a `Aborted` l'ordine e i figli non terminati e interrompe (`Aborted`) tutti i lavori non `Ended`, chiudendo le sessioni aperte. 409 altrimenti.
  - [ ] Gli ordini figli seguono lo stato del padre per annullamento e interruzione, e non si gestiscono da soli.
  - [ ] Nessuna eliminazione fisica di ordini.
  - [ ] **Macchina a stati con `Stateless`** (`ProductionOrderMachineState`), stessa impostazione di REQ-5: trigger `Release`, `Start`, `Complete`, `Cancel`, `Abort`; `Start` è ignorato se l'ordine è già `InProgress`; `Cancel` da `Released` ha una guardia (nessun lavoro avviato); `Completed`, `Cancelled` e `Aborted` sono sottostati di un superstato `Closed` (stati terminali, non modificabili). `Released` e `InProgress` sono sottostati di `Open` (che permette `Abort`).
  - [ ] I comandi sull'ordine rispondono 409 con messaggio chiaro se non consentiti. `allowedActions` anche sull'ordine.
- **Technical Constraints**: lo stato dell'ordine cambia solo tramite la sua macchina e si ricalcola nel servizio dopo ogni comando sui lavori, nella stessa transazione (i trigger `Start` e `Complete` vengono sparati dal servizio dei lavori).

### REQ-7: Pagina Ordini di produzione

- **User Story**: As a pianificatore, I want un elenco degli ordini so that li veda e li gestisca.
- **Acceptance Criteria**:
  - [ ] Pagina `/production-orders` in `apps/web/src/app/features/production-orders`, voce "Ordini di produzione" nel menu laterale, lazy route.
  - [ ] Tabella con stile standard (helm, paginatore condiviso, menu `…`): Codice, Materiale (codice e nome), Quantità e unità, Stato (badge outline con icona), Priorità, Inizio richiesto, Fine richiesta, Avanzamento (prodotto / quantità, barra `progress` se esiste, altrimenti testo e percentuale). Ricerca, filtro per stato (select) e ordinamento per priorità e data.
  - [ ] "Nuovo ordine" in toolbar apre un dialog (materiale con `hlm-select` dei materiali attivi `Intermediate`/`FinishedGood`, quantità, priorità, date, note).
  - [ ] Il menu `…` mostra solo le azioni presenti in `allowedActions` dell'ordine. Menu `…`: "Apri", "Modifica" (solo `Planned`), "Rilascia" (solo `Planned`, con conferma), "Annulla" (con conferma, destructive, dove consentito), "Interrompi" (con conferma e note, destructive, da `Released`/`InProgress`).
  - [ ] Errori 409/422 mostrati nel dialog o nella conferma. Stati skeleton, errore con "Riprova", vuoto, nessun risultato. Testi in italiano.
- **Technical Constraints**: stessa struttura di `features/materials` (store a signals, dialog, row actions nello stesso template del menu). Solo componenti helm esistenti; se manca `progress` valutarlo e, se serve, aggiungerlo con il CLI e segnalarlo.

### REQ-8: Dettaglio dell'ordine di produzione

- **User Story**: As a pianificatore, I want vedere come si scompone un ordine so that ne controlli i lavori e i semilavorati.
- **Acceptance Criteria**:
  - [ ] Pagina `/production-orders/:id`: intestazione con codice, materiale, stato, priorità, date e azioni consentite (Modifica, Rilascia, Annulla, Interrompi) e link "← Ordini".
  - [ ] Riepilogo: quantità richiesta e prodotta, contenuto di lavoro, durata minima stimata, tempo reale lavorato, scarti.
  - [ ] Sezione **Lavori**: tabella dei lavori dell'ordine (numero operazione, fase, tempo pianificato, stato, buono e scarto, tempo reale) con predecessori e successori testuali; la riga apre il dettaglio del lavoro.
  - [ ] Sezione **Materiali richiesti**: tabella con materiale, quantità, unità e, per i semilavorati, il link all'ordine figlio.
  - [ ] Sezione **Ordini figli**: albero (tabella helm con righe espandibili, pattern di `features/bom`) con codice, materiale, quantità, stato e avanzamento.
  - [ ] Stati skeleton, errore con "Riprova" e ordine non trovato.
- **Technical Constraints**: schede helm `tabs` (alla pagina, senza pannelli distrutti con elementi che richiedano misure) o sezioni; nessuna nuova libreria. Le formule di durata e tempo sono funzioni pure del frontend o campi calcolati dal backend (una sola fonte: il backend).

### REQ-9: Pagina Ordini di lavoro

- **User Story**: As a responsabile di produzione, I want un elenco dei lavori da eseguire e i comandi per avviarli e chiuderli so that segua l'avanzamento.
- **Acceptance Criteria**:
  - [ ] Pagina `/work-orders`, voce "Ordini di lavoro" nel menu laterale, lazy route.
  - [ ] Tabella standard: Codice lavoro (`{ordine}/{operazione}`), Fase, Materiale prodotto, Quantità pianificata, Stato, Buono, Scarto, Tempo pianificato, Tempo reale. Filtro per stato (default: `Ready`, `Running`, `Interrupted`, cioè i lavori operativi), per ordine e ricerca.
  - [ ] Menu `…`: "Avvia" (da `Ready`), "Metti in pausa" e "Chiudi lavoro" (da `Running`, con dialog di quantità buona, scarto e note), "Riprendi" (da `Interrupted`), "Interrompi" (con conferma e note, destructive), "Apri ordine".
  - [ ] Il dialog di pausa/chiusura mostra la quantità pianificata e la già registrata, e valida quantità >= 0.
  - [ ] I comandi mostrati dipendono da `allowedActions` del lavoro (nessuna regola di stato duplicata nel frontend).
  - [ ] I lavori `Waiting` mostrano da quali predecessori dipendono (tooltip o testo); i comandi non consentiti non compaiono.
  - [ ] Errori 409/422 nel dialog; stati skeleton, errore, vuoto, nessun risultato.
- **Technical Constraints**: come REQ-7. Questa pagina è una vista di gestione, non l'interfaccia ottimizzata per l'operatore (specifica successiva).

### REQ-10: Dati di esempio

- **User Story**: As a team, I want ordini di esempio credibili so that l'app si possa provare con un'officina in funzione.
- **Acceptance Criteria**:
  - [ ] Un seeder in Development, eseguito dopo il seeder dei dati anagrafici e **solo se la tabella degli ordini è vuota**, crea ordini usando i servizi (stessa logica di rilascio):
    - un ordine di tavolini (es. 20 pezzi, priorità alta), `Released` con i figli (struttura e piano), con la struttura **in corso** (primi lavori `Ended`, un lavoro `Running`, registrazioni con quantità e scarti plausibili);
    - un ordine di sedie (es. 12 pezzi) `Released` con i rami paralleli di taglio avviati entrambi;
    - un ordine di tavolini `Planned` (non rilasciato);
    - un ordine piccolo `Completed` (es. 4 sedie) con tutte le registrazioni;
    - un ordine `Cancelled`.
  - [ ] Date richieste realistiche rispetto a oggi, note brevi, quantità coerenti con la BOM e con i tempi dei cicli.
  - [ ] Il seeder si trova in `libs/api-features/ProductionOrderFeature` (usa i servizi) ed è richiamato da `Program.cs` solo in Development, non durante la generazione OpenAPI.
  - [ ] `doc/structure.md` descrive come ricreare i dati di esempio degli ordini.
- **Technical Constraints**: i dati devono rispettare tutte le regole delle API (stati e transizioni valide). Nessun accesso diretto a `AppDbContext` dal seeder degli ordini.

### REQ-11: Documentazione e vocabolario

- **User Story**: As a team, I want vocabolario e scelte documentati so that le funzioni successive partano da una base chiara.
- **Acceptance Criteria**:
  - [ ] `doc/glossary.md`: `ProductionOrder` (Production Request / Work Request), `WorkOrder` (Job Order / Segment Requirement), `WorkOrderDependency`, `WorkOrderMaterial` (Material Requirement, *material consumed*), `WorkOrderResponse` (Job Response), con gli stati di ordine e lavoro e la tabella di mappatura sugli stati OPC UA (*NotAllowedToStart*/*AllowedToStart*, *Waiting*, *Ready*, *Running*, *Interrupted*, *Ended*, *Aborted*).
  - [ ] Deviazioni da ISA-95 registrate: nessun Work Schedule come contenitore; un solo livello di richiesta (l'ordine) con figli per i semilavorati; un lavoro nasce `Waiting`/`Ready` al rilascio (nessun *NotAllowedToStart* separato); i materiali richiesti sono tutti assegnati al primo passo iniziale e i semilavorati bloccano tutti i passi iniziali del padre finché non esiste il legame BOM-passo; una `WorkOrderResponse` per sessione di lavoro invece di una sola Job Response per job order (il totale si somma); nessun lotto, nessuna risorsa, nessun parametro; consumi effettivi rimandati.
  - [ ] `doc/structure.md`: pattern di rilascio come istantanea, macchine a stati `Stateless` dei lavori e dell'ordine (con diagramma Mermaid o DOT delle transizioni, generato dal codice se la libreria lo consente), regola dei superstati, pagine `features/production-orders` e `features/work-orders`.
- **Technical Constraints**: solo file in `doc/`.

## 3. Layers impacted

- **Backend**: nuove entità e migration, `ProductionOrderFeature` (repository, servizi, controller per ordini e lavori, planner del rilascio, seeder degli ordini), contratto OpenAPI rigenerato.
- **Web**: feature `production-orders` (elenco, dettaglio, dialog), feature `work-orders` (elenco e comandi), voci di menu e route. Eventualmente il componente helm `progress`.
- **Nx workspace**: nessuna modifica prevista (salvo l'eventuale `progress`, generato con il CLI).

## 4. Impact Assessment

- **File interessati**: `libs/api-core/Persistence` (entità, contesto, migration), nuova cartella `libs/api-features/ProductionOrderFeature`, `apps/api/Program.cs`, `openapi/Api.json` e client generato, `apps/web/src/app/features/{production-orders,work-orders}`, `app.routes.ts`, `sidebar-data.ts`, `doc/glossary.md`, `doc/structure.md`.
- **Breaking changes**: no. Si aggiungono tabelle, endpoint e pagine; l'anagrafica non cambia.
- **Rischi**:
  - La scomposizione ricorsiva (BOM multilivello, ordini figli) è la parte più delicata: la BOM non ha controllo dei cicli, quindi il rilascio ha il limite di profondità e un errore chiaro.
  - L'assegnazione di tutti i materiali al primo passo iniziale e il blocco dei passi iniziali del padre sono approssimazioni dichiarate; il legame BOM-passo (spec successiva) le corregge.
  - Le transizioni di stato concorrenti (due utenti sullo stesso lavoro) non sono gestite oltre alla transazione: nessun controllo di concorrenza ottimistica in questa spec.
  - La spec è ampia (5 entità, 2 servizi, 3 pagine). Se serve, si divide in due: ordini e rilascio (REQ-1…4, 6, 7, 8) e lavori e comandi (REQ-5, 9).
