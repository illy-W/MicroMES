# Tasks: Panoramica grafica dell'ordine di produzione

## Technical Strategy

- Ordine: [Nx] 0 → [Backend] 1 → [Frontend] 2 → [Lead] 3. Il frontend parte dopo il report del contratto dell'endpoint (1.4). I test dell'endpoint (1.3) e la rigenerazione del contratto (1.4) chiudono il backend.
- Backend: solo lettura, nessuna modifica al modello dati. Regole di accesso ai dati del progetto: nessun `DbContext` nel servizio, repository vuoti, repository iniettati direttamente (nessuna transazione), nessun metodo nuovo nelle interfacce dei repository. Riuso di `ProductionDataLoader` e dei calcoli del planner per durata minima e percorso più lungo.
- Frontend: le skill `ng-diagram-*` (`.agents/skills`) vanno caricate prima di scrivere codice (fundamentals, troubleshooting, model-init, state-updates, custom-nodes, groups, auto-layout, middleware, viewport, styling, visibility, minimap). I nomi delle API si confermano in `node_modules/ng-diagram/ng-diagram.d.ts`.
- Layout: ELK.js con grafi annidati per i gruppi, come descritto in REQ-5. Prima dell'implementazione completa, una **prova minima** (task 2.2) con i dati reali dei tavolini e delle sedie decide se ELK annidato regge; se no, il frontend si ferma e riporta l'alternativa.
- Lezioni già imparate da `routing`: diagramma sempre montato, nascosto con `invisible`/`inert` (non `display:none`), nessun ricreare del modello al cambio scheda; sola lettura con `updateConfig` e middleware che annulla le modifiche.
- Verifica visiva: il browser dell'agente non ha una finestra visibile; il lead controlla il DOM e chiede all'utente screenshot.

## 0. Nx workspace

- [x] 0.1 [Nx] Installare `elkjs` con versione esatta (`pnpm add -w elkjs@<esatta>`; la skill cita la 0.10 degli esempi: verificare l'ultima stabile compatibile). Verificare l'import in Angular 22.1/Vite e l'errore noto `Can't resolve 'web-worker'` (alternativa `elkjs/lib/elk.bundled.js`), riportare cosa importare e la dimensione approssimativa nel bundle. Non modificare `apps/web/src`. Verificare `pnpm install --frozen-lockfile` e `npx nx build web`.
  _Satisfies: REQ-5, REQ-7_

## 1. Backend

- [x] 1.1 [Backend] `ProductionOrderOverviewModel` e relativi modelli di lettura (ordini della famiglia, lavori con ordine di appartenenza, dipendenze comprese quelle tra ordini, riepilogo con contatori per stato, durata minima e percorso più lungo come id dei lavori, `generatedAt`). Servizio `ProductionOrderOverviewService` (`Result`, `Failure` 404) che costruisce la famiglia dell'ordine (l'ordine e i discendenti) con `ProductionDataLoader` e i calcoli esistenti; ordine `Planned` → liste vuote.
  _Satisfies: REQ-1_
- [x] 1.2 [Backend] Endpoint `GET /api/production-orders/{id}/overview` nel controller degli ordini (nome esplicito dell'azione come gli altri), registrazione del servizio, codici 200 e 404.
  _Satisfies: REQ-1_
- [x] 1.3 [Backend] Test: unitari in `ApiTests` (famiglia senza figli, con un figlio, con due livelli, contatori per stato, percorso più lungo, dipendenze tra ordini) e di integrazione in `ApiTests.Integration` (ordine rilasciato con figli, `Planned` con liste vuote, 404, figlio che restituisce solo il suo sottoalbero). `npx nx run ApiTests:unit-test` e `npx nx run ApiTests.Integration:unit-test`.
  _Satisfies: REQ-1, REQ-6_
- [x] 1.4 [Backend] (ultimo) Rigenerare il contratto: `npx nx run api-client:generate-api` e riportare tipi e metodo generati. `npx nx run-many -t build lint`.
  _Satisfies: REQ-1_

## 2. Frontend

- [x] 2.1 [Frontend] Funzioni pure in `features/production-orders` (o sottocartella `overview`): costruzione del modello ngDiagram da `overview` (nodi lavoro, nodi gruppo annidati per gli ordini figli, archi interni e tra ordini), scelta del tono dello stato, riepilogo e formato dei tempi (riuso di `formatMinutes` e delle etichette esistenti, senza duplicarli). Nessun layout ancora.
  _Satisfies: REQ-3, REQ-4_
- [x] 2.2 [Frontend] Prova minima del layout con ELK annidato sui dati reali (ordine tavolini con due figli, ordine sedie con rami paralleli): gruppi come grafi annidati, somma dell'offset del gruppo alle posizioni dei figli, `size` dei gruppi dal risultato di ELK, canvas nascosto con `visibility:hidden` fino al primo layout, `zoomToFit` con tetto a 1:1. Se i figli finiscono fuori dal gruppo o gli archi tra ordini non reggono, **fermarsi e riportare** l'alternativa (due passaggi con `routing-layout.ts`) prima di continuare.
  _Satisfies: REQ-4, REQ-5_
- [x] 2.3 [Frontend] Componenti del diagramma: nodo-card del lavoro (numero operazione, codice, fase, badge di stato con icona, avanzamento con `progress`, tempi, scarto, anello per `Running`, `aria-label` con stato in testo, focus visibile), nodo gruppo con intestazione (codice, materiale, quantità, stato, avanzamento), arco tratteggiato per le dipendenze tra ordini, evidenziazione del percorso più lungo con interruttore. Sola lettura (middleware e `updateConfig` come in `features/routing`).
  _Satisfies: REQ-3, REQ-4_
- [x] 2.4 [Frontend] Scheda "Panoramica" nel dettaglio ordine come prima scheda (predefinita per ordini rilasciati, in corso o conclusi; messaggio per `Planned`), `tabs` come solo selettore, diagramma sempre montato e nascosto con `invisible`/`inert`, barra riepilogativa (conclusi su totali, in esecuzione, pronti, in attesa, interrotti/abortiti, percentuale, durata minima, "Aggiornato alle HH:mm", "Aggiorna"), legenda degli stati, "Riordina" e "Adatta", minimap sopra la soglia di 20 nodi, stati skeleton/errore con "Riprova".
  _Satisfies: REQ-2, REQ-5_
- [x] 2.5 [Frontend] Pannello laterale per nodo o gruppo selezionato (stato, quantità, tempi, predecessori e successori con stato) con i link "Apri in Ordini di lavoro" (`/work-orders?order=<id>`), "Apri ordine figlio" e "Apri ordine". "Aggiorna" mantiene vista e selezione quando cambiano solo stati e quantità e ricalcola il layout solo se cambia la struttura.
  _Satisfies: REQ-3, REQ-4, REQ-5_
- [x] 2.6 [Frontend] `npx nx lint web` e `npx nx build web`. Dichiarare cosa non è stato verificato a runtime e quali skill `ng-diagram-*` sono state consultate.
  _Satisfies: REQ-2, REQ-3, REQ-4, REQ-5, REQ-6_

## 3. Documentazione e verifica

- [x] 3.1 [Lead] `doc/structure.md`: sezione "Order overview" (endpoint, gruppi, layout ELK, sola lettura, montaggio del diagramma, skill `ng-diagram-*`), eventuale termine d'interfaccia "panoramica" nel glossario.
  _Satisfies: REQ-7_
- [ ] 3.2 [Lead] `npx nx affected -t lint build`, test, controllo dei criteri di accettazione di ogni REQ, verifica del DOM, e verifica visiva con l'utente (screenshot): ordine tavolini in corso con due figli, ordine sedie con rami paralleli, ordine concluso, ordine annullato, ordine pianificato.
  _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5, REQ-6, REQ-7_
