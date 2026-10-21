# Tasks: Ordini di produzione e ordini di lavoro

## Technical Strategy

- Ordine: [Nx] 0 → [Backend] 1 → [Frontend] 2 → [Lead] 3. Il backend riporta il contratto prima che parta il frontend. La parte più delicata (macchine a stati e rilascio) sta in file separati e puri, per testarla a mano con curl e, in futuro, con test automatici.
- Backend: stesso pattern di `Material`, `BomFeature` e `RoutingFeature` (controller → service con `Result` → repository vuoti → `AppDbContext`), mappatura manuale, nessun CQRS. Cambi di stato **solo** tramite le macchine `Stateless` (`ProductionOrderFeature/MachineStates`), come `CheckMachineState` in `ptconnect-nx`. Transazioni con `IUnitOfWork`.
- Il rilascio è un calcolo puro (`ProductionOrderPlanner`: dati di ciclo, BOM e quantità in, ordini di lavoro, materiali e figli fuori) seguito dalla persistenza. Profondità massima 10 livelli, con errore su BOM ciclica.
- `allowedActions` esce dalle macchine (`GetPermittedTriggers`) ed è l'unica fonte dei comandi per il frontend.
- Frontend: stessa struttura di `features/materials` e `features/bom` (store a signals, tabella helm con paginatore condiviso, row actions nello stesso template del menu, dialog). L'albero degli ordini figli riusa il pattern di `features/bom`. Nessun componente `shared` nuovo se non giustificato.
- Dati di esempio: il seeder degli ordini usa i servizi, quindi vale la logica di rilascio. Il DB locale ha già i dati anagrafici: non serve azzerarlo, perché il seeder parte solo con la tabella degli ordini vuota.
- Verifica visiva: il browser dell'agente non ha una finestra visibile; il lead controlla il DOM e chiede all'utente uno screenshot per le viste grafiche.

## 0. Nx workspace

- [x] 0.1 [Nx] Generare il componente helm `progress` con `npx nx g @spartan-ng/cli:ui progress --no-interactive` (se non già presente in `libs/ui`), poi `npx nx lint progress --fix`, alias con prefisso `./`, nessun `baseUrl`, `npx nx run-many -t build lint`. Segnalare dipendenze nuove, se ve ne sono.
  _Satisfies: REQ-7_

## 1. Backend

- [x] 1.1 [Backend] Entità `ProductionOrder`, `WorkOrder`, `WorkOrderDependency`, `WorkOrderMaterial`, `WorkOrderResponse` e gli enum `ProductionOrderStatus` e `WorkOrderStatus` (stati foglia, salvati come stringa) in `libs/api-core/Persistence/Entities`; `DbSet`, configurazione in `AppDbContext` (indici univoci su `Code`, su (ordine, numero operazione) e sulla coppia di dipendenze; FK `Restrict`; precisioni decimali), migration `AddProductionOrders`.
  _Satisfies: REQ-1_
- [x] 1.2 [Backend] Aggiungere il pacchetto `Stateless` (versione esatta, preferibilmente 5.20.1 come in `ptconnect-nx`) e verificare la compatibilità con net10.0. Creare `ProductionOrderFeature/MachineStates/WorkOrderMachineState.cs` e `ProductionOrderMachineState.cs` con enum interno (foglie e superstati `Prepared`, `Open`, `Closed`), mappatura sullo stato persistito, trigger e transizioni di REQ-5 e REQ-6, guardie (`Cancel` da `Released` solo senza lavori avviati), `Start` ignorato se già `InProgress`, trigger con parametri per `Pause` e `Stop`, `OnEntry` sugli stati foglia, `TimeProvider`, `GetPermittedTriggers` esposto come `AllowedActions`, e un metodo di esportazione del grafo (Mermaid o DOT) se la libreria lo prevede.
  _Satisfies: REQ-5, REQ-6_
- [x] 1.3 [Backend] Repository vuoti e implementazioni per le cinque entità, registrazione in `ProductionOrderFeature/Registrar.cs` (con `AddUnitOfWork` se non già registrato), richiamo da `Program.cs`.
  _Satisfies: REQ-2_
- [x] 1.4 [Backend] `ProductionOrderPlanner` (puro): dato materiale, quantità, BOM e cicli, produce lavori (tempi effettivi, quantità pianificata), dipendenze (anche tra ordini figli e padre), materiali richiesti (assegnati al primo passo iniziale) e struttura degli ordini figli, ricorsivo con limite di 10 livelli e rilevazione di BOM ciclica (errore 422). Calcolo di contenuto di lavoro e durata minima stimata (percorso più lungo tra lavori e figli).
  _Satisfies: REQ-4_
- [x] 1.5 [Backend] `ProductionOrderService` (`Result`, `Failure` per 404/409): elenco con filtri e avanzamento, dettaglio (lavori, dipendenze, materiali, figli, riepilogo tempi), creazione (codice `OP-{anno}-{nnnn}`, regole di REQ-3), modifica solo `Planned`, rilascio transazionale che usa il planner e la macchina, annullamento e interruzione via macchina con effetto sui figli e sui lavori. Messaggi di 409 e 422 in italiano.
  _Satisfies: REQ-3, REQ-4, REQ-6_
- [x] 1.6 [Backend] `WorkOrderService`: elenco con filtri, dettaglio (registrazioni, materiali, predecessori e successori, tempo reale e quantità totali, `allowedActions`), comandi `start`, `pause`, `resume`, `stop`, `abort` tramite la macchina, apertura e chiusura delle `WorkOrderResponse`, sblocco dei successori (`PredecessorsEnded`, anche tra ordini), aggiornamento dello stato dell'ordine (`Start` e `Complete`) nella stessa transazione. 409 con messaggio italiano che indica lo stato attuale e le azioni possibili; 422 per quantità non valide.
  _Satisfies: REQ-5, REQ-6_
- [x] 1.7 [Backend] `ProductionOrdersController` (`GET` elenco, `GET {id}`, `POST`, `PUT {id}`, `POST {id}/release`, `POST {id}/cancel`, `POST {id}/abort`) e `WorkOrdersController` (`GET`, `GET {id}`, `POST {id}/start`, `pause`, `resume`, `stop`, `abort`); modelli di richiesta e risposta con `allowedActions`; enum come stringa; codici 200, 201, 204, 404, 409, 422.
  _Satisfies: REQ-3, REQ-5, REQ-6_
- [x] 1.8 [Backend] Verifica con curl su un'istanza temporanea (porta diversa da 5190, spenta alla fine): creazione, modifica, rilascio del tavolino con i due figli, rilascio della sedia con rami paralleli, stati iniziali `Ready`/`Waiting`, blocco dei passi iniziali del padre finché i figli non finiscono, cicli completi di avvio, pausa, ripresa, chiusura e interruzione, sblocco dei successori, ordine `Completed`, annullamento ammesso e vietato, 409 con messaggio, `allowedActions`, BOM ciclica (creata temporaneamente) con 422, tempi pianificati e quantità. Poi `npx nx build Api`. Riportare i dati di prova da cancellare.
  _Satisfies: REQ-3, REQ-4, REQ-5, REQ-6_
- [x] 1.9 [Backend] Rigenerare il contratto: `npx nx run api-client:generate-api`. Riportare tipi, metodi e forma delle risposte generate (incluso `allowedActions`).
  _Satisfies: REQ-3, REQ-5_
- [x] 1.10 [Backend] Seeder degli ordini di esempio (REQ-10) in `ProductionOrderFeature`, solo in Development, solo con la tabella degli ordini vuota, via servizi: ordine tavolini in corso con i figli, ordine sedie con i due rami avviati, ordine tavolini `Planned`, ordine `Completed` con tutte le registrazioni, ordine `Cancelled`. Richiamarlo da `Program.cs` dopo il seeder anagrafico. Verificare l'avvio e l'elenco degli ordini. Eliminare i dati di prova di 1.8 (azzerando solo ordini e lavori, non l'anagrafica).
  _Satisfies: REQ-10_

## 2. Frontend

- [x] 2.1 [Frontend] Feature `production-orders`: route lazy `/production-orders`, voce "Ordini di produzione" nel menu laterale, store a signals, tabella standard (colonne, ricerca, filtro per stato, ordinamento, avanzamento con `progress`), dialog di creazione e modifica con `hlm-select` dei materiali ammessi, stati skeleton, errore, vuoto, nessun risultato.
  _Satisfies: REQ-7_
- [x] 2.2 [Frontend] Menu `…` dell'elenco ordini con le azioni di `allowedActions` ("Apri", "Modifica", "Rilascia", "Annulla", "Interrompi") con alert-dialog di conferma e note dove previste, errori 409/422 nelle conferme.
  _Satisfies: REQ-7_
- [x] 2.3 [Frontend] Pagina `/production-orders/:id`: intestazione e azioni consentite, riepilogo (quantità, prodotta, contenuto di lavoro, durata minima, tempo reale, scarti), sezioni Lavori (con predecessori e successori), Materiali richiesti (link all'ordine figlio) e Ordini figli (albero, pattern di `features/bom`), stati skeleton, errore, non trovato.
  _Satisfies: REQ-8_
- [x] 2.4 [Frontend] Feature `work-orders`: route lazy `/work-orders`, voce "Ordini di lavoro" nel menu laterale, store, tabella standard con filtri (stato, ordine, ricerca; default lavori operativi), menu `…` da `allowedActions` ("Avvia", "Metti in pausa", "Chiudi lavoro", "Riprendi", "Interrompi", "Apri ordine"), dialog di pausa/chiusura con quantità buona, scarto e note (pianificata e già registrata a vista, quantità >= 0), tooltip o testo sui predecessori dei lavori `Waiting`, errori 409/422.
  _Satisfies: REQ-9_
- [x] 2.5 [Frontend] `npx nx lint web` e `npx nx build web`. Dichiarare cosa non è stato verificato a runtime.
  _Satisfies: REQ-7, REQ-8, REQ-9_

## 3. Documentazione e verifica

- [x] 3.1 [Lead] `doc/glossary.md`: `ProductionOrder`, `WorkOrder`, `WorkOrderDependency`, `WorkOrderMaterial`, `WorkOrderResponse`, stati e mappatura OPC UA, deviazioni da ISA-95. `doc/structure.md`: rilascio come istantanea, macchine `Stateless` (diagramma, regola dei superstati, `allowedActions`), pagine `features/production-orders` e `features/work-orders`, ricreare i dati di esempio degli ordini.
  _Satisfies: REQ-11_
- [x] 3.2 [Lead] `npx nx affected -t lint build`, controllo dei criteri di accettazione di ogni REQ, verifica visiva con l'utente (screenshot): elenco ordini, dettaglio con albero dei figli, pagina dei lavori con i comandi, dialog di pausa e chiusura, un ciclo completo di un ordine piccolo.
  _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5, REQ-6, REQ-7, REQ-8, REQ-9, REQ-10, REQ-11_
