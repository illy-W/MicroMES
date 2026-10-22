# Tasks: Pagina di dettaglio macchina

## Technical Strategy

- **Backend**: due endpoint di sola lettura in `EquipmentFeature`: `GET /api/equipment/{id}/overview` (stato adesso, lavori in corso, coda, stato di montaggio, carico 1/7/30 giorni, segnalazioni) e `GET /api/equipment/{id}/history?days=7|30|90&page=` (riepilogo, utilizzo, scarti per motivo, sessioni paginate). Riusano i calcolatori esistenti (carico e segnalazioni di `PlanningFeature`, `MachineStartingState`, calendario effettivo, sessioni di `ProductionOrderFeature`); nessun dato duplicato né salvato. Filtro `equipmentId` sulla timeline se manca.
- **Frontend**: nuova pagina `/equipment/:id` a schede (`?tab=`), lazy; la timeline e il calendario si riusano estraendo componenti condivisi, senza duplicare codice; il pannello laterale dell'elenco viene sostituito; link dai punti di ingresso.
- **Contratto**: l'ultimo task backend rigenera il client (`npx nx run api-client:generate-api`).

## Backend

- [ ] 1.1 [Backend] `GET /api/equipment/{id}/overview`: stato adesso (libera, in lavorazione con inizio reale e durata trascorsa, chiusa con motivo da calendario, disattivata), coda dei prossimi 20 lavori pianificati (codice, ordine, fase, inizio-fine, attrezzaggio, stato del piano, fissato), stato di montaggio (attrezzature e valori di cambio dell'ultimo lavoro eseguito o in corso, sconosciuto se assente), carico per oggi/7/30 giorni con la definizione della timeline, segnalazioni della macchina con causa e suggerimento; 404 per id inesistente; test unitari e di integrazione. _Satisfies: REQ-2_
- [ ] 1.2 [Backend] Dettaglio macchina esteso: attrezzature compatibili (collegamento esplicito) e quelle «compatibili con tutte» in una collezione distinta, con quantità, minuti di montaggio e stato; indicatore delle attrezzature montate adesso (coerente con 1.1). Filtro `equipmentId` sulla timeline (se non presente) con test. _Satisfies: REQ-3, REQ-5_
- [ ] 1.3 [Backend] `GET /api/equipment/{id}/history?days=&page=`: lavori eseguiti, minuti lavorati, utilizzo (minuti lavorati ÷ minuti disponibili del calendario effettivo nel periodo), buoni e scarti con percentuale, scarti per motivo, sessioni dove la macchina del consuntivo è questa (paginate, ordine inverso, con lavoro, ordine, inizio-fine, durata, buoni, scarti); aggregazioni calcolate sul DB; periodo 7/30/90 giorni (default 30), 422 per valori non validi; test unitari e di integrazione. _Satisfies: REQ-6_
- [ ] 1.4 [Backend] Rigenerare il contratto OpenAPI e il client (`npx nx run api-client:generate-api`) e riportare il riepilogo del contratto. _Satisfies: REQ-1, REQ-2, REQ-3, REQ-5, REQ-6_

## Frontend

- [ ] 2.1 [Frontend] Rotta `/equipment/:id` (lazy) con intestazione (codice, nome, stato, capacità, efficienza, calendario seguito, predefinita per le fasi), azioni Modifica/Disattiva/Riattiva con i dialoghi esistenti, schede con `?tab=`, id inesistente con messaggio e ritorno all'elenco; il click su una riga dell'elenco apre la pagina e il pannello laterale attuale viene rimosso. _Satisfies: REQ-1_
- [ ] 2.2 [Frontend] Scheda Panoramica (stato adesso, lavoro in corso, coda, stato di montaggio, carico 1/7/30 con evidenza ≥ 90%, segnalazioni) e scheda Fasi e attrezzature (fasi con predefinita, attrezzature compatibili e «compatibili con tutte», montate adesso, link). _Satisfies: REQ-2, REQ-3_
- [ ] 2.3 [Frontend] Scheda Calendario (riuso dei componenti di `features/calendars`, estratti se serve, con pulsanti verso il calendario e le eccezioni della macchina) e scheda Pianificazione (timeline compatta della sola macchina a 14 giorni con riuso del componente timeline e pannello del lavoro, carico per giorno, «Apri nella pianificazione completa» con la macchina filtrata). _Satisfies: REQ-4, REQ-5_
- [ ] 2.4 [Frontend] Scheda Storico (periodo 7/30/90, riepilogo con utilizzo e % scarto, scarti per motivo, sessioni paginate con link al lavoro) e link alla pagina dai punti di ingresso: etichetta macchina della timeline, pannello del lavoro, dettaglio attrezzatura, dettaglio ordine e lavori, lista Ordini di lavoro, segnalazioni. _Satisfies: REQ-1, REQ-6_

## Documentazione

- [ ] 3.1 [Lead] Aggiornare `doc/structure.md` (nuova pagina, endpoint, componenti riusati) e verificare i criteri di accettazione. _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5, REQ-6_
