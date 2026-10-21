# Spec: Panoramica grafica dell'ordine di produzione

## 1. Context & Goals

Il dettaglio di un ordine di produzione (`/production-orders/:id`) mostra tabelle: lavori, materiali richiesti, ordini figli. Per capire **a che punto siamo** serve leggere righe e stati uno per uno. Questa spec aggiunge una **panoramica grafica** con **ngDiagram**: i lavori come nodi, le dipendenze come archi, gli ordini figli (semilavorati) come **gruppi** che contengono i loro lavori, con stato e avanzamento visibili a colpo d'occhio.

La panoramica è **di sola lettura**: i comandi restano nelle tabelle e nelle pagine esistenti. Riusa il lavoro già fatto per il ciclo di lavoro (nodo come componente Angular con stile proprio, sola lettura con middleware, evidenziazione del percorso più lungo, diagramma sempre montato).

Decisioni già prese (con l'utente):

- Si usa **ngDiagram** (versione installata 1.4.0) e le skill `ng-diagram-*` per l'implementazione (`.agents/skills`).
- Gli ordini figli sono **gruppi** ngDiagram annidati.
- Il layout è **calcolato** (non si salvano posizioni): ELK.js (`elkjs`), come suggerisce la skill `ng-diagram-auto-layout`.

Fuori ambito: aggiornamento in tempo reale (WebSocket/SignalR), comandi dai nodi (avvia, pausa, chiudi), posizioni salvate o spostamento dei nodi, esportazione in immagine, vista a diagramma nella pagina Ordini di lavoro, calendario o diagramma di Gantt, test E2E del frontend.

## 2. Requirements

### REQ-1: Endpoint della panoramica (backend)

- **User Story**: As a pianificatore, I want i dati dell'intero ordine, figli compresi, in una sola chiamata so that il diagramma si disegni senza richieste multiple.
- **Acceptance Criteria**:
  - [ ] `GET /api/production-orders/{id}/overview` restituisce l'**intera famiglia** dell'ordine (l'ordine e tutti i discendenti), in forma piatta:
    - `orders`: per ogni ordine `id`, `code`, `parentOrderId`, materiale (`materialId`, codice, nome, unità), `quantity`, `status`, `producedQuantity`, `progressPercent`;
    - `workOrders`: per ogni lavoro `id`, `productionOrderId`, `code`, `operationNumber`, fase (codice e nome), `status`, `plannedQuantity`, `goodQuantity`, `scrapQuantity`, `plannedMinutes`, `workedMinutes`, `allowedActions`;
    - `dependencies`: **tutte** le dipendenze tra lavori della famiglia (predecessore e successore), comprese quelle tra l'ultimo lavoro di un figlio e i lavori iniziali del padre;
    - `summary`: totale lavori, per stato (contatori), durata minima stimata e percorso più lungo (id dei lavori), calcolati dal backend come nel dettaglio;
    - `generatedAt` (UTC).
  - [ ] 404 se l'ordine non esiste. Per un ordine `Planned` (non rilasciato) risponde 200 con `workOrders` e `dependencies` vuoti.
  - [ ] Se l'id è di un ordine figlio, la famiglia è quella del **suo** sottoalbero (non risale al padre).
  - [ ] Contratto OpenAPI e client Angular rigenerati (`npx nx run api-client:generate-api`).
- **Technical Constraints**: pattern di `ProductionOrderFeature` (controller → service con `Result` → repository). Regole di accesso ai dati del progetto (`AGENTS.md`, `doc/structure.md` "Backend data access rules"): nessun `DbContext` nel servizio, repository vuoti, repository risolti da `IUnitOfWork` solo se il servizio usa una transazione (qui no: sola lettura, quindi repository iniettati). Riusare `ProductionDataLoader` e i calcoli del planner (durata minima, percorso più lungo) invece di duplicarli. Nessuna modifica al modello dati.

### REQ-2: Scheda "Panoramica" nel dettaglio ordine (web)

- **User Story**: As a pianificatore, I want una vista grafica dell'ordine so that capisca subito lo stato di avanzamento.
- **Acceptance Criteria**:
  - [ ] Nel dettaglio ordine (`/production-orders/:id`) una scheda **"Panoramica"**, la prima e quella predefinita quando l'ordine è rilasciato o in corso o concluso (per un ordine `Planned` la scheda mostra un messaggio "L'ordine non è ancora stato rilasciato: i lavori vengono creati al rilascio" e non il diagramma).
  - [ ] Il diagramma ngDiagram è di **sola lettura**: nodi non trascinabili, nessun collegamento, nessuna cancellazione. Pan, zoom, selezione e minimap (sopra una soglia di nodi, es. 20) restano attivi.
  - [ ] Il diagramma resta **montato** quando si cambia scheda e non è nascosto con `display:none` (si usa `invisible` e `inert`), e non si ricrea il modello a ogni cambio (lezione del bug di `routing`). Le skill `ng-diagram-model-init` e `ng-diagram-visibility` sono la guida.
  - [ ] Barra riepilogativa sopra il diagramma: lavori conclusi su totali, in esecuzione, pronti, in attesa, interrotti/abortiti, percentuale di avanzamento, durata minima stimata, e "Aggiornato alle HH:mm" con pulsante **"Aggiorna"** che ricarica la panoramica (non si ricarica da sola).
  - [ ] Legenda degli stati sempre visibile (icona e testo, non solo colore).
  - [ ] Stati skeleton, errore con "Riprova", ordine non trovato.
- **Technical Constraints**: schede helm `tabs` con il diagramma fuori dai pannelli distrutti (come in `features/routing`: `tabs` solo come selettore). Solo componenti helm esistenti e token semantici, nessun `dark:`. La skill `ng-diagram-fundamentals` e `ng-diagram-troubleshooting` vanno consultate prima di scrivere codice; i nomi delle API vanno verificati in `node_modules/ng-diagram/ng-diagram.d.ts`.

### REQ-3: Nodi dei lavori

- **User Story**: As a pianificatore, I want che ogni nodo mostri il lavoro e il suo stato so that riconosca subito dove siamo.
- **Acceptance Criteria**:
  - [ ] Nodo-card con: numero operazione, codice del lavoro, nome della fase, **badge di stato** (icona e testo con tono semantico), **avanzamento** (buono su pianificato, con barra `progress` e quantità), tempo lavorato su pianificato, eventuale scarto.
  - [ ] Stato visivo coerente con il resto dell'app (stessi toni dei badge di `production-orders`): in attesa (neutro), pronto, in esecuzione (evidenziato con anello), interrotto (avviso), concluso, abortito (distruttivo).
  - [ ] Il lavoro `Running` è riconoscibile anche in bianco e nero (anello e icona, senza animazioni).
  - [ ] Il percorso più lungo (dal `summary`) è evidenziato con un token semantico, con interruttore "Mostra percorso più lungo" (attivo di default) e legenda.
  - [ ] Clic su un nodo: lo seleziona e mostra in un pannello laterale i dettagli (stato, quantità, tempi, predecessori e successori con il loro stato) e i link "Apri in Ordini di lavoro" (`/work-orders?order=<id>`) e, se il lavoro appartiene a un figlio, "Apri ordine figlio" (`/production-orders/<id figlio>`). Nessun comando.
  - [ ] Ogni nodo ha ruolo e `aria-label` con codice, fase e **stato in testo**; focus visibile; l'informazione non dipende dal colore.
- **Technical Constraints**: componente nodo dedicato (i nodi ngDiagram non hanno stile proprio; vedi `ng-diagram-custom-nodes`), riuso di `workStatus`/etichette e di `formatMinutes` esistenti senza duplicarli. Nessun nuovo componente helm (sono già presenti `progress`, `badge`, `card`).

### REQ-4: Ordini figli come gruppi

- **User Story**: As a pianificatore, I want vedere i semilavorati come blocchi che contengono i loro lavori so that capisca cosa alimenta cosa.
- **Acceptance Criteria**:
  - [ ] Ogni ordine figlio (a qualsiasi profondità) è un **nodo gruppo** ngDiagram che contiene i suoi lavori, con intestazione: codice dell'ordine, materiale e quantità, **badge di stato** e barra di avanzamento. I gruppi si annidano per BOM multilivello.
  - [ ] I lavori dell'ordine selezionato (radice del diagramma) stanno al livello più alto, non in un gruppo.
  - [ ] Gli archi tra ordini (ultimo lavoro del figlio → lavori iniziali del padre) attraversano il bordo del gruppo e sono distinguibili dagli archi interni (es. tratteggio e legenda).
  - [ ] Il gruppo non è trascinabile né ridimensionabile (sola lettura); la sua dimensione deriva dal contenuto.
  - [ ] Un clic sull'intestazione del gruppo mostra nel pannello i dettagli dell'ordine figlio (stato, quantità, avanzamento) con il link "Apri ordine".
  - [ ] Ordine senza figli: nessun gruppo, solo la catena di lavori.
- **Technical Constraints**: skill `ng-diagram-groups` (membership con `groupId`, `isGroup`, gruppi con layout) e `ng-diagram-auto-layout`. Il modello si costruisce una sola volta da `overview` e si aggiorna (nodi e archi) senza ricreare il diagramma quando si preme "Aggiorna".

### REQ-5: Layout automatico

- **User Story**: As a pianificatore, I want un diagramma ordinato senza doverlo sistemare so that sia leggibile subito.
- **Acceptance Criteria**:
  - [ ] Il layout è calcolato con **ELK.js** (`elkjs`, versione esatta, caricato in modo lazy solo nella scheda), da sinistra a destra, seguendo la skill `ng-diagram-auto-layout`. Poiché gli archi tra ordini attraversano il bordo dei gruppi e la skill avverte che un layout piatto mette i figli fuori dal gruppo e che gli archi che finiscono dentro un gruppo non influenzano il layout dei nodi di primo livello, i gruppi si inviano a ELK come **grafi annidati** (`children` dentro il nodo gruppo). Conseguenze da gestire esplicitamente, come indica la skill: ELK restituisce le posizioni dei figli **relative al gruppo** mentre ngDiagram salva posizioni globali (sommare la posizione del gruppo); i gruppi **non crescono** attorno ai figli, quindi la loro `size` va scritta nel modello dal risultato di ELK; spostare un gruppo non sposta i figli (`getChildrenNested`). Gli archi restano `routingMode: 'auto'` salvo diversa necessità; se la skill o ELK richiedono la gestione delle gerarchie per gli archi che attraversano i gruppi, valutare l'opzione di ELK per includere i figli nel calcolo (da verificare sulla documentazione di ELK, non sulla skill).
  - [ ] Il layout parte quando i nodi sono misurati (`diagramInit`), con il canvas nascosto con `visibility:hidden` (non `display:none`) fino al primo layout; poi `zoomToFit` con tetto a 1:1.
  - [ ] Pulsanti "Riordina" (ricalcola il layout) e "Adatta" (adatta la vista) nella barra del canvas.
  - [ ] "Aggiorna" mantiene vista e selezione quando cambiano solo gli stati e le quantità; ricalcola il layout solo se la struttura cambia.
  - [ ] Con ordini di decine di lavori resta fluido (virtualizzazione solo se serve, secondo `ng-diagram-performance` da installare in quel caso).
- **Technical Constraints**: se ELK si rivela troppo pesante o inadatto ai gruppi, fermarsi e riportare l'alternativa (riuso di `routing-layout.ts` per i livelli interni a un gruppo e un secondo passaggio per i gruppi) prima di implementarla. Nessun salvataggio delle posizioni.

### REQ-6: Test e verifica

- **User Story**: As a team, I want che l'endpoint e il calcolo siano coperti da test so that le modifiche non rompano la panoramica.
- **Acceptance Criteria**:
  - [ ] Test unitari (`ApiTests`) per la costruzione della famiglia, il riepilogo (contatori per stato, percorso più lungo) e le dipendenze tra ordini, con ordine senza figli, con un figlio e con due livelli di figli.
  - [ ] Test di integrazione (`ApiTests.Integration`) su PostgreSQL: l'overview di un ordine rilasciato con figli contiene tutti i lavori e le dipendenze tra ordini; per un ordine `Planned` risponde 200 con liste vuote; 404 per un id inesistente; per un figlio contiene solo il suo sottoalbero.
  - [ ] Nel frontend: funzioni pure (costruzione del modello ngDiagram da `overview`, scelta del tono, formato dei tempi) in file separati e riusabili; `npx nx lint web` e `npx nx build web` passano. (Il frontend non ha ancora un framework di test: non si introduce in questa spec.)
  - [ ] Verifica visiva con l'utente (screenshot) su: ordine tavolini in corso con due figli, ordine sedie con rami paralleli, ordine concluso, ordine annullato.
- **Technical Constraints**: regole dei test del progetto (`doc/structure.md` "Backend tests"): la logica va nei test unitari, l'integrazione solo per ciò che dipende da PostgreSQL o da una query reale.

### REQ-7: Dipendenza e documentazione

- **User Story**: As a team, I want la dipendenza e le convenzioni documentate so that le prossime viste a diagramma le seguano.
- **Acceptance Criteria**:
  - [ ] `[Nx]` installa `elkjs` con versione esatta (`pnpm add -w`), verifica compatibilità con Angular 22.1/Vite (web worker o versione bundled `elk.bundled.js`) e riporta cosa importare.
  - [ ] `doc/structure.md`: sezione "Order overview" con endpoint, gruppi per gli ordini figli, layout ELK, regole di sola lettura e di montaggio del diagramma, e le skill `ng-diagram-*` da consultare.
  - [ ] `doc/glossary.md`: nessun nuovo termine di dominio; se compare "panoramica" come termine di interfaccia, registrarlo.
- **Technical Constraints**: solo file in `doc/` per la documentazione.

## 3. Layers impacted

- **Backend**: nuovo endpoint e modelli di lettura in `ProductionOrderFeature`, test unitari e di integrazione, contratto OpenAPI rigenerato.
- **Web**: scheda "Panoramica" in `features/production-orders` (diagramma, nodi, gruppi, layout, pannello laterale, barra riepilogativa), eventuale riuso di `routing-view-state` o simili.
- **Nx workspace**: installazione di `elkjs`.

## 4. Impact Assessment

- **File interessati**: `libs/api-features/ProductionOrderFeature` (controller, servizio, modelli), `libs/api-tests*` (nuovi test), `openapi/Api.json` e client generato, `package.json` e lockfile, `apps/web/src/app/features/production-orders/*` (nuovi componenti e file), `doc/structure.md`.
- **Breaking changes**: no. Si aggiunge un endpoint e una scheda.
- **Rischi**:
  - I gruppi annidati con layout automatico sono la parte più delicata (le skill avvertono di figli che "si muovono, spariscono o escono dal gruppo"): la spec prevede un punto di arresto se ELK non basta.
  - La misurazione dei nodi (`Measurement timeout reached`) ha già dato problemi con finestre non visibili: il controllo visivo dipende da screenshot dell'utente.
  - `elkjs` aumenta il bundle: va caricato in lazy solo nella scheda.
  - I dati non sono in tempo reale: l'utente potrebbe vedere uno stato vecchio. Per questo ci sono l'ora di aggiornamento e il pulsante "Aggiorna".
