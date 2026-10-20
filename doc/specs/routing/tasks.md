# Tasks: Ciclo di lavoro (Routing) e Fasi di processo

## Technical Strategy

- Ordine: [Nx] 0 → [Backend] 1 → [Frontend] 2 → [Lead] 3. Il frontend parte solo dopo il report del contratto del backend. I dati di esempio (1.8) stanno dopo il contratto, ma prima del frontend, così il frontend si prova subito su dati reali.
- Nx: installare `ng-diagram` con versione esatta (`pnpm add -w`) e verificare che compili con Angular 22.1. Il frontend integra poi gli stili.
- Backend: stesso pattern di `Material` e `BomFeature` (controller → service con `Result` → repository vuoti → `AppDbContext`), mappatura manuale, nessun CQRS. Il `PUT` del ciclo salva l'intero grafo in una transazione. Il seeder è infrastruttura e usa `AppDbContext`.
- Frontend: tabelle con le direttive helm e il paginatore condiviso; l'editor usa `ng-diagram` con nodi Angular che usano badge, input e label Spartan. Una prova minima di `ng-diagram` precede l'editor completo: se la compatibilità fallisce, il frontend si ferma e riporta.
- Dati di esempio: il DB locale viene azzerato (l'utente deve prima fermare `nx serve web`, perché tiene aperte connessioni) e ricreato dalle migration; il seeder popola solo se `Materials` è vuota.

## 0. Nx workspace

- [x] 0.1 [Nx] Installare `ng-diagram` con versione esatta (`pnpm add -w ng-diagram@<esatta>`, ultima stabile: 1.4.0, da verificare), controllare i peer con Angular 22.1 e se richiede stili globali (CSS/SCSS di `ng-diagram`) o asset: riportare al lead e al frontend cosa importare e dove, senza modificare `apps/web/src`. Verificare `pnpm install --frozen-lockfile`.
  _Satisfies: REQ-5_

## 1. Backend

- [x] 1.1 [Backend] Entità `ProcessSegment` (campi e regole di REQ-1), `DbSet`, configurazione in `AppDbContext`, indice univoco su `Code`, migration `AddProcessSegments`.
  _Satisfies: REQ-1_
- [x] 1.2 [Backend] `ProcessSegmentFeature`: `IProcessSegmentRepository` (nessun metodo aggiunto), `ProcessSegmentRepository`, `ProcessSegmentService` (`Result`, normalizzazione del codice, 409 su duplicato), `ProcessSegmentsController` con i 5 endpoint come `MaterialsController` (`DELETE` disattiva), registrazione in `Program.cs`.
  _Satisfies: REQ-1_
- [x] 1.3 [Backend] Entità `RoutingStep` e `RoutingStepDependency` (REQ-2), indici univoci (materiale + numero operazione, coppia predecessore/successore), FK `Restrict`, migration `AddRouting`.
  _Satisfies: REQ-2_
- [x] 1.4 [Backend] `RoutingFeature`: repository vuoti per i due tipi, registrazione, `RoutingService` con `GET` (grafo con dati della fase e tempi effettivi) e `PUT` transazionale (upsert dei passi per `id`, eliminazione dei mancanti con le loro dipendenze, dipendenze per `key`). Regole di REQ-3: tipo del materiale, `operationNumber` univoco, fase attiva per passi nuovi o con fase cambiata, tempi >= 0, nessun autoanello né duplicato, aciclico, connesso, un solo passo finale; ciclo vuoto valido.
  _Satisfies: REQ-3_
- [x] 1.5 [Backend] `RoutingController`: `GET /api/materials/{id}/routing` e `PUT /api/materials/{id}/routing`; 200, 404, 422. Enum come stringa.
  _Satisfies: REQ-3_
- [x] 1.6 [Backend] Verifica con curl su un'istanza temporanea (a DB azzerato): CRUD delle fasi, ciclo lineare, rami paralleli, ciclo con anello (422), grafo non connesso (422), due passi finali (422), numero operazione duplicato (422), fase disattivata (422), materiale `RawMaterial` (422), ciclo vuoto, 404. Poi `npx nx build Api`.
  _Satisfies: REQ-1, REQ-2, REQ-3_
- [x] 1.7 [Backend] Rigenerare il contratto: `npx nx run api-client:generate-api`. Riportare i nomi dei tipi e dei metodi generati.
  _Satisfies: REQ-1, REQ-3_
- [x] 1.8 [Backend] Seeder dei dati di esempio (REQ-7), eseguito solo in Development dopo le migration e solo se `Materials` è vuota: materiali, fasi, BOM multilivello e cicli (con la sedia a rami paralleli), con le posizioni X/Y dei passi. Azzerare il DB locale (dopo aver chiesto al lead di far fermare il dev server): `dotnet ef database drop --force` o `docker compose down -v` + `npx nx run Api:db-up`, poi avvio dell'API e verifica via curl che i dati rispettino le regole (albero BOM a due livelli, cicli letti con `GET`). Riportare l'elenco finale di materiali, fasi e cicli creati.
  _Satisfies: REQ-7_

## 2. Frontend

- [x] 2.1 [Frontend] Feature `process-segments`: route lazy `/process-segments`, voce nel menu laterale (`sidebar-data.ts`), store a signals, tabella con stile standard, ricerca, "Mostra disattivati", "Nuova fase", menu `…` con Modifica e Disattiva (alert-dialog), dialog di creazione/modifica con errori 409/422, stati skeleton, errore, vuoto, nessun risultato. Stessa struttura di `features/materials`.
  _Satisfies: REQ-4_
- [x] 2.2 [Frontend] Prova minima di `ng-diagram` (stili da 0.1, un diagramma con due nodi collegati nella futura pagina) per verificare la compatibilità con Angular 22.1 e il tema con i token semantici. Se fallisce: fermarsi e riportare.
  _Satisfies: REQ-5_
- [x] 2.3 [Frontend] Pagina `/materials/:id/routing`: route lazy, store/stato a signals (carica il grafo e le fasi attive, modifiche locali, stato "modificato", annulla modifiche), intestazione con codice e nome del materiale, link "← Materiali", "Salva" e "Annulla modifiche", avviso se si esce con modifiche non salvate.
  _Satisfies: REQ-5_
- [x] 2.4 [Frontend] Editor ngDiagram: componente nodo (numero operazione, codice e nome della fase, tempi effettivi in badge), archi orientati, trascinamento, connessione tra porte, eliminazione di nodi e archi, pan/zoom, minimap, palette o elenco delle fasi attive con "Aggiungi" (prossimo numero operazione multiplo di 10), layout iniziale a strati per i passi senza posizione.
  _Satisfies: REQ-5_
- [x] 2.5 [Frontend] Pannello del passo selezionato (numero operazione, tempi che sostituiscono quelli standard con il valore standard indicato, note) con componenti form Spartan; vista alternativa in sola lettura (tabella dei passi con predecessori e successori, accessibile da tastiera).
  _Satisfies: REQ-5_
- [x] 2.6 [Frontend] Salvataggio con `PUT` dell'intero grafo (chiavi del client, gli `id` esistenti), convalida lato client con messaggi in italiano (cicli, passo finale unico, grafo connesso, numeri operazione duplicati), errori 422/404 mostrati con il testo del backend, stati skeleton/errore/ciclo vuoto.
  _Satisfies: REQ-5_
- [x] 2.7 [Frontend] Voce "Ciclo di lavoro" nel menu `…` dei Materiali (solo `Intermediate` e `FinishedGood`, dopo "Distinta base") verso `/materials/:id/routing`.
  _Satisfies: REQ-5_
- [x] 2.8 [Frontend] `npx nx lint web` e `npx nx build web`. I browser tool non sono disponibili al frontend: dichiararlo, il lead fa il controllo visivo.
  _Satisfies: REQ-4, REQ-5_

## 3. Documentazione e verifica

- [x] 3.1 [Lead] `doc/glossary.md`: `ProcessSegment`, `RoutingStep`, `RoutingStepDependency`, aggiornare "Product Definition", registrare le deviazioni da ISA-95. `doc/structure.md`: `ng-diagram`, salvataggio dell'intero grafo con `PUT`, `features/routing`, azzeramento dei dati di esempio.
  _Satisfies: REQ-6, REQ-7_
- [x] 3.2 [Lead] `npx nx affected -t lint build`, controllo dei criteri di accettazione di ogni REQ, verifica visiva nel browser (fasi, editor con la sedia a rami paralleli, salvataggio, errori, menu Materiali, BOM del tavolino a due livelli).
  _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5, REQ-6, REQ-7_
