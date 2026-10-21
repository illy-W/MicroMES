# Tasks: Anagrafica macchine e postazioni

## Technical Strategy

- Nuova feature backend `EquipmentFeature` in `libs/api-features`, sul modello di `ProcessSegmentFeature` (CRUD con soft-delete) e di `ScrapFeature` per il catalogo con collegamento alle fasi.
- Entità `Equipment` (`Code` unico maiuscolo, `Name`, `Description`, `IsActive`) e giunzione `EquipmentProcessSegment` (indice unico fase+macchina, cancellazione a cascata dalla giunzione).
- Il collegamento si gestisce da entrambi i lati con lo stesso dato: il servizio della macchina aggiorna le fasi collegate (sostituzione dell'insieme) e il servizio della fase aggiorna le macchine collegate; entrambi in una transazione con `IUnitOfWork`, con la stessa regola di validazione (solo attive, esistenti).
- Nessuna macchina collegata = nessun vincolo; nessuna modifica a cicli, ordini, lavori e panoramica.
- Il seeder dei dati anagrafici crea le macchine dopo le fasi (in `SampleDataFeature`, solo repository/UoW).
- Ordine: Backend (ultimo task = rigenerazione contratto), poi Frontend. Nessun task Nx.

## Backend

- [x] 1.1 [Backend] Entità `Equipment` e `EquipmentProcessSegment` + migration (`AddEquipment`), indici (codice unico, coppia unica), messaggi 409 specifici in `ConstraintViolation` (nessun «riprova»). Nessun metodo nelle interfacce repository. _Satisfies: REQ-1, REQ-2_
- [x] 1.2 [Backend] `EquipmentFeature`: CRUD con soft-delete e filtri (attive, ricerca, per fase), modello con fasi collegate (id, codice, nome, attiva), creazione e modifica con l'insieme di fasi (sostituzione), validazione (codice normalizzato, fasi esistenti e attive), `EquipmentController` con `ProducesResponseType` e azioni esplicite. _Satisfies: REQ-1, REQ-2_
- [x] 1.3 [Backend] `ProcessSegmentFeature`: il modello della fase include le macchine collegate (id, codice, nome, attiva); richieste di creazione e modifica accettano l'insieme di macchine (campo opzionale: se omesso i collegamenti non cambiano, per non rompere i client esistenti; riportare la scelta); stessa validazione (solo macchine attive, esistenti). _Satisfies: REQ-2_
- [x] 1.4 [Backend] Seed di sviluppo: circa 8 macchine e postazioni coerenti con le 11 fasi (due per il taglio tubolare, una per il taglio lamiera, due di saldatura, forno/cabina di verniciatura a polvere, cabina per vernice acquosa, banco di assemblaggio, tavolo di imballaggio), almeno una macchina su più fasi e almeno una fase senza macchine; dati e conteggi esistenti invariati. Indicare cosa svuotare per un DB esistente. _Satisfies: REQ-3_
- [x] 1.5 [Backend] Test unitari (validazione, soft-delete, sostituzione dei collegamenti, solo attive) e di integrazione (CRUD HTTP, 409 codice duplicato, collegamento da entrambi i lati, disattivazione che mantiene i collegamenti, seed su DB nuovo e idempotenza). `npx nx run ApiTests:unit-test`, `npx nx run ApiTests.Integration:unit-test`. _Satisfies: REQ-1..REQ-3_
- [x] 1.6 [Backend] Contratto: `npx nx run api-client:generate-api` (con `--skip-nx-cache` se `openapi/Api.json` risulta invariato), poi `npx nx run-many -t build lint`. Riportare nomi dei tipi e forma dei campi nuovi. _Satisfies: REQ-1..REQ-3_

## Frontend

- [ ] 2.1 [Frontend] Pagina «Macchine e postazioni» (`features/equipment`): lista (codice, nome, fasi collegate, stato), ricerca, filtro attive/disattivate, crea e modifica in dialog con scelta delle fasi (checkbox con ricerca e hint «nessuna = nessun vincolo»), attiva/disattiva con conferma, errori 409/422 con il testo del backend; voce nel gruppo **Anagrafiche** del menu e rotta. Schema di `features/scrap-reasons`. _Satisfies: REQ-1, REQ-2_
- [ ] 2.2 [Frontend] Pagina «Fasi di processo»: colonna delle macchine abilitate e scelta delle macchine (solo attive) nel dialog di creazione/modifica, con lo stesso hint; nessuna regressione sugli altri campi. _Satisfies: REQ-2_
- [ ] 2.3 [Frontend] `npx nx lint web` e `npx nx build web`; riportare cosa non è verificato a runtime. _Satisfies: REQ-1..REQ-3_

## Lead

- [ ] 3.1 [Lead] Aggiornare `doc/glossary.md` (`Equipment`, `EquipmentProcessSegment`, deviazioni da ISA-95, voce «Equipment» dalla tabella Planned) e `doc/structure.md` (feature, collegamento, menu). _Satisfies: tutti_
- [ ] 3.2 [Lead] Verifica: `npx nx run-many -t lint build`, test unitari e di integrazione, DB di sviluppo ripristinato con il seed, controllo DOM nel browser (menu, pagina macchine, scheda fase), verifica dei criteri di accettazione. _Satisfies: tutti_
