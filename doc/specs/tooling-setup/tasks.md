# Tasks: Attrezzature e attrezzaggi

## Technical Strategy

- **Dati** (`ApiCore`): `Tooling`, `ToolingEquipment` (compatibilità), `SetupAttribute`, `RoutingStepTooling`, `RoutingStepSetupValue`, `WorkOrderTooling`, `WorkOrderSetupValue` (snapshot al rilascio con codice/nome istantanei), colonne di attrezzaggio calcolato sul lavoro pianificato (minuti e motivazione). Migration.
- **Feature** `ToolingFeature` (ApiFeatures) per attrezzature e attributi, sullo schema di `EquipmentFeature`; `RoutingFeature` esteso per requisiti e valori sul passo; `ProductionOrderFeature` per lo snapshot al rilascio.
- **Calcolo puro** `SetupCalculator` (PlanningFeature): stato macchina (attrezzature montate, valori) + lavoro nuovo = minuti e motivazione; formula di REQ-4 (somma, tetto `SetupMinutes` del passo, stato ignoto = attrezzaggio completo). Usato da stima, motore, anteprima e spostamento manuale: nessuna formula duplicata.
- **Motore**: vincolo cumulativo per attrezzatura (quantità), compatibilità macchine, attrezzaggio dalla sequenza reale con proprietà verificata dai test, preferenza a parità di fine per la macchina con meno attrezzaggio; lavori `Running`/fissati come intervalli fissi e come stato di partenza.
- **Segnalazioni**: quattro nuovi tipi nel calcolatore esistente.
- **Frontend**: pagine Attrezzature e Attributi di cambio, editor del ciclo esteso, timeline (tratto di attrezzaggio, filtro attrezzatura), pannello, anteprima, segnalazioni.
- **Contratto**: l'ultimo task backend rigenera il client (`npx nx run api-client:generate-api`).

## Backend

- [x] 1.1 [Backend] Entità e migration: `Tooling` (codice univoco maiuscolo, nome, descrizione, `Quantity` ≥ 1, `SetupMinutes` ≥ 0, `IsActive`), `ToolingEquipment`, `SetupAttribute` (codice univoco, nome, `ChangeMinutes` ≥ 0, `IsActive`). _Satisfies: REQ-1, REQ-3_
- [x] 1.2 [Backend] `ToolingFeature`: CRUD attrezzature (con compatibilità macchine, disattivazione, dettaglio con passi e lavori che le usano) e CRUD attributi di cambio; 404/409/422 specifici; test unitari e di integrazione. _Satisfies: REQ-1, REQ-3_
- [x] 1.3 [Backend] Passo di ciclo: attrezzature richieste (N:M, una unità) e valori degli attributi; validazione (attive, valori normalizzati); modelli e endpoint del ciclo estesi; test. _Satisfies: REQ-2, REQ-3_
- [x] 1.4 [Backend] Snapshot al rilascio: `WorkOrderTooling` e `WorkOrderSetupValue` con codici/nomi istantanei; 422 se un'attrezzatura richiesta è disattivata; esposti nei modelli del lavoro e dell'ordine. _Satisfies: REQ-2, REQ-3_
- [x] 1.5 [Backend] `SetupCalculator` puro (stato macchina → minuti e motivazione, tetto del passo, stato ignoto) con test dell'esempio guida (15 / 5 / 0 / tetto 12) e casi limite (attributo assente, attrezzatura già montata, nessun attributo); integrazione nella durata e nella stima di fine ordine senza duplicare la formula. _Satisfies: REQ-4_
- [x] 1.6 [Backend] Stato di partenza delle macchine dai lavori `Running` e dall'ultimo lavoro terminato per macchina; costruzione dell'input del motore con attrezzature, compatibilità e valori. _Satisfies: REQ-4, REQ-5_
- [x] 1.7 [Backend] Motore: vincolo cumulativo per attrezzatura (anche con intervalli fissi), compatibilità macchine, attrezzaggio dalla sequenza reale (inserimenti in un vuoto solo se il successivo non peggiora), preferenza a parità di fine, motivi nominati; proprietà «attrezzaggio = formula su coppie consecutive» verificata da test; tre scenari nuovi nel banco di prova con valori di riferimento; baseline esistenti invariate. _Satisfies: REQ-5_
- [x] 1.8 [Backend] Anteprima/applicazione e timeline: attrezzaggio calcolato e motivazione per lavoro, minuti totali prima/dopo nel riepilogo, filtro per attrezzatura; spostamento manuale (`move`) che ricalcola l'attrezzaggio e riporta i conflitti di attrezzatura. _Satisfies: REQ-4, REQ-5, REQ-7_
- [x] 1.9 [Backend] Segnalazioni: attrezzatura sovrapposta, non compatibile con la macchina, disattivata con lavori aperti, lavoro non pianificabile per attrezzatura; causa nominata e suggerimento. _Satisfies: REQ-6_
- [ ] 1.10 [Backend] Dati di esempio di sviluppo (attrezzature condivise tra macchine, attributi colore/materiale, requisiti sui cicli) e rigenerazione del contratto (`npx nx run api-client:generate-api`) con riepilogo per il frontend. _Satisfies: REQ-1, REQ-2, REQ-3, REQ-7_

## Frontend

- [ ] 2.1 [Frontend] Pagina «Attrezzature» (Anagrafiche): elenco, ricerca, filtro attive, creazione/modifica con macchine compatibili, dettaglio con passi e lavori che la usano; voce di menu. _Satisfies: REQ-1, REQ-7_
- [ ] 2.2 [Frontend] Pagina «Attributi di cambio» (Anagrafiche) con CRUD e messaggi specifici. _Satisfies: REQ-3, REQ-7_
- [ ] 2.3 [Frontend] Editor del ciclo: selezione delle attrezzature richieste e valori degli attributi per passo, errori inline, tastiera. _Satisfies: REQ-2, REQ-3, REQ-7_
- [ ] 2.4 [Frontend] Timeline e pannello: tratto di attrezzaggio distinto nella barra (pattern, non solo colore), tooltip e pannello con durata e motivazione e attrezzature richieste, filtro «Attrezzatura», legenda. _Satisfies: REQ-7_
- [ ] 2.5 [Frontend] Anteprima di pianificazione e spostamento: attrezzaggio per lavoro, minuti totali prima/dopo, motivi sulle attrezzature, conflitti di attrezzatura nel dialogo «Sposta…»; segnalazioni nel popover, nel dettaglio ordine e nella lista lavori. _Satisfies: REQ-6, REQ-7_

## Documentazione

- [ ] 3.1 [Lead] Aggiornare `doc/glossary.md` (Physical Asset ↔ attrezzatura, attributi di cambio come estensione, deviazioni), `doc/structure.md` e verificare i criteri di accettazione. _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5, REQ-6, REQ-7_
