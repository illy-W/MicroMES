# Spec: Assegnazione delle macchine ai lavori

## 1. Context & Goals

Dopo l'anagrafica delle macchine (`specs/equipment`) il sistema sa *dove si può* eseguire una fase, ma non *dove si esegue* un lavoro. Obiettivo: poter dire su quale macchina o postazione è pianificato un ordine di lavoro e su quale è stato **effettivamente** eseguito ogni sessione, con il minimo attrito per chi lavora.

Fuori da questa spec: calendario, capacità, carico e pianificazione a capacità finita, fermi macchina, persone. Questa spec produce i dati su cui poggeranno.

### Ricerca (standard e prodotti)

| Fonte | Cosa dice | Cosa ne ricaviamo |
|---|---|---|
| **ISA-95** | Distingue il **requisito** (cosa serve al lavoro, *Equipment Requirement* del Job Order) dal **consuntivo** (cosa è stato usato, *Equipment Actual* del Job Response). | Due dati distinti: macchina **assegnata** al lavoro e macchina **usata** in ogni sessione. |
| **Odoo MRP** | L'operazione del ciclo ha un work center; se è al limite di capacità la pianificazione propone un work center alternativo; l'operatore lavora sul work center del lavoro. | La macchina è proposta, poi confermabile; le alternative sono le macchine collegate alla stessa fase. |
| **ERPNext** | Per ogni operazione si crea una Job Card che prende la postazione disponibile del tipo richiesto. | Proposta automatica quando non c'è scelta, altrimenti scelta esplicita. |

## 2. Requirements

### REQ-1: Macchina assegnata al lavoro
- **User Story**: As a responsabile di produzione, I want assegnare un lavoro a una macchina so that l'officina sappia dove va eseguito.
- **Acceptance Criteria**:
  - [ ] `WorkOrder.AssignedEquipmentId` (nullable, FK a `Equipment`, `SetNull`).
  - [ ] Al rilascio il lavoro nasce con la **macchina predefinita della fase** (REQ-6) se esiste ed è attiva; altrimenti, se la fase ha **una sola** macchina attiva collegata, con quella; se ne ha più di una senza predefinita, o nessuna, nasce senza assegnazione (nessuna scelta arbitraria).
  - [ ] Azione esplicita «Assegna macchina» (cambia/rimuove) per i lavori `Waiting`, `Ready` e `Interrupted`; non su `Running` (si cambia alla prossima sessione), `Ended` e `Aborted`. Endpoint dedicato (es. `PUT /api/work-orders/{id}/equipment`), 409 se lo stato non lo consente.
  - [ ] Validazione: la macchina deve essere attiva ed esistente e **collegata alla fase del lavoro** (422 altrimenti); per una fase senza macchine collegate l'assegnazione non è possibile (422, messaggio chiaro: «La fase non ha macchine collegate»).
  - [ ] Se in seguito la macchina viene disattivata o scollegata dalla fase, il lavoro la mantiene ma l'interfaccia lo segnala («macchina non più abilitata») e l'avvio richiede di sceglierne un'altra.
  - [ ] I lavori degli ordini figli sono trattati allo stesso modo (nessuna eredità).
- **Technical Constraints**: Colonna nullable + migration; il comando non passa dalla macchina a stati (non è un cambio di stato); repository vuoti, nessun `DbContext`; `IUnitOfWork` se si modificano più entità.

### REQ-2: Macchina usata in ogni sessione
- **User Story**: As an operatore, I want che il sistema ricordi su quale macchina lavoro so that i dati reali siano giusti senza fatica.
- **Acceptance Criteria**:
  - [ ] `WorkOrderResponse.EquipmentId` (nullable, FK `SetNull`) con istantanea `EquipmentCode`/`EquipmentName` (lo storico non cambia se la macchina viene rinominata).
  - [ ] Avvio e ripresa accettano un corpo opzionale `equipmentId` (compatibile col client attuale: senza corpo si usa la macchina assegnata; alla ripresa, quella dell'ultima sessione).
  - [ ] Regola: se la fase ha macchine attive collegate, la sessione **deve** avere una macchina (assegnata, indicata o ripresa dalla sessione precedente); altrimenti 422 con messaggio «Scegli la macchina». Se la fase non ha macchine, il campo resta vuoto e non viene chiesto.
  - [ ] La macchina deve essere attiva e collegata alla fase (422 altrimenti).
  - [ ] Se la macchina indicata all'avvio differisce da quella assegnata, l'assegnazione del lavoro viene aggiornata alla macchina usata (il piano segue la realtà); la sessione conserva comunque il suo dato.
  - [ ] Pausa/Termina non chiedono la macchina (è quella della sessione aperta).
  - [ ] La correzione di una sessione chiusa può cambiare la macchina (stessa validazione), con traccia nello storico delle correzioni.
- **Technical Constraints**: La richiesta di avvio/ripresa è un corpo opzionale; nessun cambio di stato nella macchina a stati; lo storico delle correzioni include la macchina precedente e nuova.

### REQ-3: Segnalare le macchine occupate
- **User Story**: As an operatore, I want sapere se la macchina che scelgo è già in uso so that non creo conflitti senza accorgermene.
- **Acceptance Criteria**:
  - [ ] I modelli di lavoro espongono `assignedEquipment` (id, codice, nome, attiva, abilitata) e `currentEquipment` (macchina della sessione aperta, se `Running`).
  - [ ] Nel dialogo di avvio/ripresa e in «Assegna macchina» ogni macchina candidata indica se è **in uso** da un altro lavoro `Running` (codice del lavoro), come avviso **non bloccante** (una macchina può essere usata in parallelo finché non esiste il concetto di capacità).
  - [ ] Il dato viene dall'elenco dei lavori (filtro per stato `Running`), senza un nuovo endpoint dedicato.
- **Technical Constraints**: Nessuna regola di capacità in questa spec; l'avviso è solo informativo.

### REQ-4: Dove si vede la macchina
- **User Story**: As a responsabile di produzione, I want vedere la macchina nei lavori e poter filtrare per macchina so that capisco cosa gira dove.
- **Acceptance Criteria**:
  - [ ] Pagina Ordini di lavoro: colonna «Macchina» (assegnata, con badge se in uso o non più abilitata), filtro per macchina, azione «Assegna macchina», e nel dialogo di avvio/ripresa la scelta della macchina precompilata con quella assegnata (nessun clic extra se c'è già).
  - [ ] Elenco delle sessioni di un lavoro: macchina usata per ogni sessione.
  - [ ] Panoramica dell'ordine: la macchina nel pannello del lavoro e, se c'è spazio, un'indicazione compatta nella scheda del nodo con testo accessibile.
  - [ ] Tab «Lavori» del dettaglio ordine: colonna macchina.
  - [ ] L'endpoint dell'elenco lavori accetta il filtro `equipmentId`.
- **Technical Constraints**: Componenti helm, token semantici, nessun colore fisso; select con `itemToString`; se la fase non ha macchine, il campo non compare (nessun rumore).

### REQ-5: Dati di esempio
- **User Story**: As a utente in prova, I want vedere lavori già assegnati e sessioni con macchina so that la funzione si capisce subito.
- **Acceptance Criteria**:
  - [ ] Il seed imposta una macchina predefinita su alcune fasi con più macchine (es. TAG-TUB-1 per il taglio tubolare, SAL-1 per la saldatura) e ne lascia una senza (per mostrare la scelta manuale).
  - [ ] Il seed assegna le macchine ai lavori degli ordini esistenti in modo coerente con stato e fasi (una sola macchina → assegnata; più macchine → alcune assegnate, altre no), e le sessioni storiche hanno la macchina usata (almeno un caso in cui differisce dall'assegnata, es. SAL-2 al posto di SAL-1).
  - [ ] Almeno un lavoro `Running` su una macchina e un lavoro pronto con la stessa macchina candidata, per mostrare l'avviso «in uso».
  - [ ] Codici, stati e conteggi degli ordini esistenti invariati; test di integrazione su DB nuovo e idempotenza.
- **Technical Constraints**: Seeder solo su tabelle vuote; indicare cosa svuotare per un DB esistente.

### REQ-6: Macchina predefinita per fase
- **User Story**: As a responsabile di produzione, I want indicare la macchina abituale di ogni fase so that i lavori nascano già assegnati senza doverli smistare ogni volta.
- **Acceptance Criteria**:
  - [ ] Tra le macchine collegate a una fase se ne può indicare **una** come predefinita (flag `IsDefault` sul collegamento `EquipmentProcessSegment`, al massimo una per fase: indice unico filtrato). Nessuna predefinita è un caso valido.
  - [ ] La predefinita deve essere una macchina collegata e attiva alla fase al momento dell'impostazione (422 altrimenti); se la macchina viene scollegata dalla fase, il flag cade insieme al collegamento; se viene disattivata, il flag resta ma non viene usato al rilascio (nessuna assegnazione automatica a macchine disattive).
  - [ ] Si imposta dalla scheda della fase: nel dialogo di creazione/modifica, tra le macchine selezionate, una sola «predefinita» (selezione singola con possibilità di toglierla). La lista delle fasi e la scheda macchina la mostrano (es. badge «predefinita»).
  - [ ] Contratto: le richieste della fase accettano `defaultEquipmentId` (nullable). Quando `equipmentIds` è presente, il valore di `defaultEquipmentId` sostituisce la predefinita (null = nessuna; deve appartenere all'insieme, 422 altrimenti); quando `equipmentIds` è omesso, la predefinita non cambia. Dalla scheda macchina, la sostituzione delle fasi conserva i flag dei collegamenti che restano.
  - [ ] La predefinita vale solo per i lavori creati dopo: i lavori già rilasciati non cambiano (nessuna riassegnazione retroattiva).
  - [ ] I modelli di fase e macchina espongono la predefinita (`isDefault` sul collegamento).
- **Technical Constraints**: Colonna booleana sul collegamento con indice unico filtrato (`IsDefault = true` per fase); messaggio 409/422 specifico; migration insieme alle altre di questa spec; il flag appartiene al modello `EquipmentFeature`/`ProcessSegmentFeature` e il rilascio lo legge tramite repository.

## 3. Layers impacted

- **Backend**: flag `IsDefault` sul collegamento fase-macchina, `WorkOrder.AssignedEquipmentId`, `WorkOrderResponse.EquipmentId` + istantanee, migration, rilascio (assegnazione automatica), comando di assegnazione, avvio/ripresa con corpo opzionale, correzioni, modelli, filtro, seed, test, contratto.
- **Web**: pagina Ordini di lavoro (colonna, filtro, assegnazione, avvio/ripresa), sessioni, panoramica, dettaglio ordine.
- **Nx workspace**: nessun impatto.

## 4. Impact Assessment

- Affected: `libs/api-core` (entità, migration), `libs/api-features/ProductionOrderFeature` (planner/servizio di rilascio, `WorkOrderService`, modelli, controller), `EquipmentFeature` (sola lettura delle macchine collegate alle fasi), seeder, `libs/api-tests*`, `apps/web/src/app/features/work-orders`, `production-orders` (dettaglio e panoramica), `doc/glossary.md`, `doc/structure.md`.
- Breaking changes: **no**. I corpi di avvio/ripresa sono opzionali; i campi nuovi dei modelli sono opzionali. Nuovo comportamento: avviare un lavoro la cui fase ha macchine collegate richiede una macchina (assegnata o scelta); i lavori esistenti senza assegnazione chiederanno la scelta al primo avvio.
- Deviazioni da ISA-95 (da annotare in `doc/glossary.md`): requisito e consuntivo sono campi del lavoro e della sessione, non entità `Equipment Requirement`/`Equipment Actual` separate; una sola macchina per lavoro e per sessione; nessuna quantità o uso diverso.

## 5. Fuori scopo

Capacità, carico e finestre di disponibilità, calendario e turni, pianificazione automatica o bilanciamento tra macchine alternative, fermi macchina e manutenzione, persone e operatori, più macchine per sessione, vincolo per singolo passo del ciclo.

## 6. Decisioni (proposte del lead da confermare)

1. Assegnazione automatica al rilascio: macchina predefinita della fase se c'è, altrimenti l'unica macchina attiva collegata; negli altri casi si sceglie (responsabile in anticipo o operatore all'avvio). Confermato, con l'aggiunta della predefinita per fase (REQ-6).
2. (confermato) Avvio/ripresa richiedono la macchina solo se la fase ha macchine collegate; la scelta è precompilata, quindi nessun clic extra quando il lavoro è già assegnato.
3. (confermato) La macchina in uso da altri lavori è un avviso, non un blocco (finché non esiste la capacità).
4. (confermato) Se all'avvio si usa una macchina diversa da quella assegnata, l'assegnazione del lavoro segue la macchina usata.
