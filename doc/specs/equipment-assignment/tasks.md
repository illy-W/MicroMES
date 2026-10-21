# Tasks: Assegnazione delle macchine ai lavori

## Technical Strategy

- Tre colonne nuove: `EquipmentProcessSegment.IsDefault` (indice unico filtrato per fase), `WorkOrder.AssignedEquipmentId` e `WorkOrderResponse.EquipmentId` con istantanee `EquipmentCode`/`EquipmentName`. Una sola migration (`AddEquipmentAssignment`).
- Regola unica di selezione/validazione della macchina (attiva, esistente, collegata alla fase) in un componente condiviso tra rilascio, assegnazione, avvio/ripresa e correzione.
- Rilascio: macchina predefinita attiva della fase, altrimenti l'unica macchina attiva collegata, altrimenti nessuna.
- Assegnazione: endpoint dedicato, non passa dalla macchina a stati.
- Avvio/ripresa: corpo opzionale con `equipmentId`; senza corpo si usa l'assegnata (avvio) o l'ultima sessione (ripresa); obbligatoria solo se la fase ha macchine attive collegate; l'assegnazione del lavoro segue la macchina usata.
- Frontend: la macchina occupata si ricava dall'elenco dei lavori `Running` (nessun nuovo endpoint).
- Ordine: Backend (ultimo task = rigenerazione contratto), poi Frontend. Nessun task Nx.

## Backend

- [x] 1.1 [Backend] Entità e migration: `IsDefault` su `EquipmentProcessSegment` (indice unico filtrato per fase, messaggio 409 specifico), `WorkOrder.AssignedEquipmentId` (FK `SetNull`), `WorkOrderResponse.EquipmentId` + `EquipmentCode`/`EquipmentName` (FK `SetNull`). Nessun metodo nelle interfacce repository. _Satisfies: REQ-1, REQ-2, REQ-6_
- [x] 1.2 [Backend] Macchina predefinita: `defaultEquipmentId` nelle richieste della fase (semantica: con `equipmentIds` presente sostituisce la predefinita, deve appartenere all'insieme, 422 altrimenti; con `equipmentIds` omesso non cambia), il collegamento scollegato porta via il flag, la sostituzione dalla scheda macchina conserva i flag dei collegamenti che restano; `isDefault` nei modelli di fase e macchina. _Satisfies: REQ-6_
- [x] 1.3 [Backend] Regola condivisa di selezione/validazione macchina (attiva, esistente, collegata alla fase, messaggi 422 chiari in italiano, fase senza macchine → nessuna macchina) e rilascio: assegnazione automatica (predefinita attiva, altrimenti unica macchina attiva, altrimenti nessuna); i lavori già rilasciati non cambiano. _Satisfies: REQ-1, REQ-6_
- [x] 1.4 [Backend] Assegnazione: `PUT /api/work-orders/{id}/equipment` (assegna/rimuove) valido per `Waiting`/`Ready`/`Interrupted` (409 negli altri stati), 404, 422; `assignedEquipment` e `currentEquipment` (id, codice, nome, attiva, abilitata) nei modelli di lista e dettaglio; filtro `equipmentId` nell'elenco lavori. _Satisfies: REQ-1, REQ-3, REQ-4_
- [x] 1.5 [Backend] Avvio e ripresa con corpo opzionale `equipmentId`: macchina obbligatoria solo se la fase ha macchine attive collegate (422 «Scegli la macchina»), istantanea nella sessione, l'assegnazione del lavoro segue la macchina usata, ripresa con l'ultima macchina di default; macchina nelle sessioni dei modelli di dettaglio; correzione di una sessione che può cambiare la macchina con traccia (valori precedente e nuovo). Compatibilità col client attuale (corpo assente). _Satisfies: REQ-2_
- [x] 1.6 [Backend] Seed di sviluppo: predefinite su alcune fasi con più macchine (una fase con più macchine resta senza), macchine assegnate ai lavori e sessioni storiche con la macchina usata (almeno un caso diverso dall'assegnata), un lavoro `Running` su una macchina e un lavoro pronto con la stessa macchina candidata; codici, stati e conteggi degli ordini esistenti invariati. Indicare cosa svuotare per un DB esistente. _Satisfies: REQ-5, REQ-6_
- [x] 1.7 [Backend] Test unitari (selezione macchina, rilascio con predefinita/unica/nessuna, validazione, assegnazione per stato, avvio con e senza macchina, ripresa, correzione) e di integrazione (flussi HTTP, 422/409, compatibilità senza corpo, flag predefinita e indice unico, seed su DB nuovo e idempotenza). `npx nx run ApiTests:unit-test`, `npx nx run ApiTests.Integration:unit-test`. _Satisfies: REQ-1..REQ-6_
- [x] 1.8 [Backend] Contratto: `npx nx run api-client:generate-api` (con `--skip-nx-cache` se `openapi/Api.json` risulta invariato), poi `npx nx run-many -t build lint`. Riportare nomi dei tipi e forma dei campi nuovi. _Satisfies: REQ-1..REQ-6_

## Frontend

- [ ] 2.1 [Frontend] Fasi di processo: nel dialogo, tra le macchine selezionate, scelta di una sola «predefinita» (con possibilità di toglierla); la lista fasi e la lista macchine mostrano il badge «predefinita»; si invia `defaultEquipmentId` insieme a `equipmentIds`. _Satisfies: REQ-6_
- [ ] 2.2 [Frontend] Ordini di lavoro: colonna «Macchina» (assegnata, con badge «in uso» e «non più abilitata»), filtro per macchina, azione «Assegna macchina» per i lavori `Waiting`/`Ready`/`Interrupted` con l'avviso non bloccante delle macchine in uso (dall'elenco dei lavori `Running`); il campo non compare se la fase non ha macchine. _Satisfies: REQ-1, REQ-3, REQ-4_
- [ ] 2.3 [Frontend] Avvio e ripresa: dialogo con la macchina precompilata (assegnata, o ultima sessione alla ripresa), obbligatoria se la fase ha macchine, avviso «in uso», errori 422 mostrati; se il lavoro è già assegnato e valido, l'avvio resta a un clic. Sessioni del lavoro con la macchina usata e correzione che può cambiarla. _Satisfies: REQ-2, REQ-3, REQ-4_
- [ ] 2.4 [Frontend] Dettaglio ordine (tab «Lavori», colonna macchina) e panoramica (macchina nel pannello laterale e indicazione compatta nel nodo con testo accessibile). _Satisfies: REQ-4_
- [ ] 2.5 [Frontend] `npx nx lint web` e `npx nx build web`; riportare cosa non è verificato a runtime. _Satisfies: REQ-1..REQ-6_

## Lead

- [ ] 3.1 [Lead] Aggiornare `doc/glossary.md` (campi nuovi, predefinita, deviazioni da ISA-95) e `doc/structure.md` (assegnazione, regola di selezione, avvio/ripresa). _Satisfies: tutti_
- [ ] 3.2 [Lead] Verifica: `npx nx run-many -t lint build`, test unitari e di integrazione, DB di sviluppo ripristinato con il seed, controllo DOM nel browser (fasi, lavori, avvio, panoramica), verifica dei criteri di accettazione. _Satisfies: tutti_
