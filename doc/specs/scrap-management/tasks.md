# Tasks: Gestione degli scarti

## Technical Strategy

- Nuova feature backend `ScrapFeature` (catalogo motivi, report) in `libs/api-features`; la chiusura sessione e la correzione stanno in `ProductionOrderFeature` (usano `IUnitOfWork`: sessione, righe di scarto e traccia nella stessa transazione).
- Entità: `ScrapReason`, `ScrapReasonProcessSegment` (molti-a-molti), `WorkOrderResponseScrap` (response, `ScrapReasonId` nullable con `SetNull`, istantanea `ReasonCode`/`ReasonName`, `Quantity`), `WorkOrderResponseCorrection` (response, valori precedenti e nuovi, nota, data). `WorkOrderResponse.ScrapQuantity` resta ed è la somma delle righe.
- Compatibilità: `scrapEntries` opzionale nella richiesta di chiusura; senza righe, tutto lo scarto è «senza motivo».
- Regola unica di validazione delle righe (somma, motivo attivo, motivo valido per la fase, quantità > 0) condivisa da chiusura e correzione.
- Report: aggregazioni sul backend, sulle registrazioni con le correzioni già applicate (i valori correnti della sessione).
- Frontend: menu a gruppi; pagina «Motivi di scarto» sul modello di «Fasi di processo»; dialogo di chiusura con righe motivo+quantità; elenco sessioni con correzione; pagina «Scarti».
- Ordine: Backend (ultimo task = rigenerazione contratto), poi Frontend. Nessun task Nx.

## Backend

- [x] 1.1 [Backend] Entità e migration (`AddScrapManagement`): `ScrapReason`, `ScrapReasonProcessSegment`, `WorkOrderResponseScrap`, `WorkOrderResponseCorrection`, indici (codice unico, coppia motivo-fase unica), messaggi 409 in `ConstraintViolation` (nessun «riprova»). Nessun metodo nelle interfacce repository. _Satisfies: REQ-1, REQ-2, REQ-3_
- [x] 1.2 [Backend] Catalogo motivi: CRUD con soft-delete (attivo/inattivo), filtro per fase e per attivi, fasi collegate nel modello, validazione (codice maiuscolo, fasi esistenti), controller `ScrapReasonsController`. _Satisfies: REQ-1_
- [x] 1.3 [Backend] Chiusura sessione (Pausa/Termina): `scrapEntries[]` in `CloseSessionRequest`, regola di validazione condivisa (somma = scarto, motivo attivo e valido per la fase, quantità), istantanea di codice e nome, righe nei modelli di dettaglio (`WorkOrderSessionModel` con `scrapEntries`), compatibilità senza righe, messaggi 422 chiari. Il modello del lavoro segnala se i buoni restano sotto il pianificato (campo calcolato o derivabile dal frontend: riportare cosa è stato scelto). _Satisfies: REQ-2_
- [x] 1.4 [Backend] Correzione: `POST /api/work-orders/{id}/responses/{responseId}/corrections` con buoni, scarto e righe, nota obbligatoria; solo sessioni chiuse; 409 se l'ordine è `Completed`/`Cancelled`; riga di traccia con valori precedenti e nuovi; totali e avanzamento ricalcolati; storico correzioni nel modello della sessione. Transazione con `IUnitOfWork`. _Satisfies: REQ-3_
- [x] 1.5 [Backend] Report: `GET /api/scrap/report?from&to` con totali, percentuale di scarto, classifica per motivo (con «senza motivo»), per fase e per materiale; modelli puliti con nome. _Satisfies: REQ-4_
- [x] 1.6 [Backend] Seed di sviluppo: catalogo motivi (con fasi collegate, alcuni validi per tutte); le sessioni degli ordini seed con scarto ricevono righe con motivi diversi (anche una senza motivo e almeno un caso con più motivi); una correzione di esempio su un ordine aperto; nessun cambio di codici, stati e conteggi degli ordini esistenti (se cambia qualcosa nei test riportarlo). Indicare cosa svuotare nel DB di sviluppo (il lead lo fa). _Satisfies: REQ-1, REQ-2, REQ-4_
- [x] 1.7 [Backend] Test unitari (regola di validazione, istantanea, compatibilità senza righe, correzione, report) e di integrazione (catalogo + 409, chiusura con più motivi, motivo non valido per la fase → 422, somma errata → 422, correzione su ordine `Completed` → 409, report sui dati seed, seed su DB nuovo e idempotenza). `npx nx run ApiTests:unit-test`, `npx nx run ApiTests.Integration:unit-test`. _Satisfies: REQ-1..REQ-4_
- [x] 1.8 [Backend] Contratto: `npx nx run api-client:generate-api` (con `--skip-nx-cache` se `openapi/Api.json` risulta invariato), poi `npx nx run-many -t build lint`. Riportare nomi dei tipi e forma dei campi nuovi. _Satisfies: REQ-1..REQ-4_

## Frontend

- [ ] 2.1 [Frontend] Menu raggruppato (`sidebar-data.ts`, `nav-main`): Dashboard in alto, gruppo «Produzione» (Ordini di produzione, Ordini di lavoro, Scarti), gruppo «Anagrafiche» (Materiali, Fasi di processo, Motivi di scarto); etichette di gruppo con `hlm-sidebar-group-label`; voce attiva anche sulle rotte figlie; sidebar compressa e tastiera. Le rotte delle nuove pagine vanno registrate. _Satisfies: REQ-5_
- [ ] 2.2 [Frontend] Pagina «Motivi di scarto» (`features/scrap-reasons`): lista, crea, modifica, attiva/disattiva, scelta fasi (con ricerca), messaggi 409 specifici; stesso schema di «Fasi di processo». _Satisfies: REQ-1_
- [ ] 2.3 [Frontend] Dialogo di chiusura del lavoro: righe motivo + quantità (aggiungi/rimuovi), solo motivi validi per la fase del lavoro, voce «Senza motivo», somma calcolata che alimenta lo scarto, nota facoltativa, errori 422 mostrati, segnalazione non bloccante dei pezzi mancanti rispetto al pianificato. _Satisfies: REQ-2_
- [ ] 2.4 [Frontend] Sessioni del lavoro con correzione: elenco delle sessioni (buoni, scarti con motivi, nota, indicatore «corretta» e storico) e azione «Correggi» con nota obbligatoria, errori 409/422 specifici; azione non offerta se l'ordine è `Completed`/`Cancelled`. _Satisfies: REQ-3_
- [ ] 2.5 [Frontend] Pagina «Scarti» (`features/scrap`): periodo, totale e percentuale, classifica per motivo/fase/materiale con barre e testo, quota «senza motivo» evidenziata, stati vuoto/caricamento/errore. _Satisfies: REQ-4_
- [ ] 2.6 [Frontend] `npx nx lint web` e `npx nx build web`; riportare cosa non è verificato a runtime. _Satisfies: REQ-1..REQ-5_

## Lead

- [ ] 3.1 [Lead] Aggiornare `doc/glossary.md` (`ScrapReason`, `WorkOrderResponseScrap`, `WorkOrderResponseCorrection`, deviazioni da ISA-95) e `doc/structure.md` (scarti, correzioni, menu). _Satisfies: tutti_
- [ ] 3.2 [Lead] Verifica: `npx nx run-many -t lint build`, test unitari e di integrazione, DB di sviluppo ripristinato con il seed, controllo DOM nel browser di menu, motivi, chiusura, correzione e report, verifica dei criteri di accettazione. _Satisfies: tutti_
