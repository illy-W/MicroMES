# Tasks: Blocco manuale del piano

## Technical Strategy

- **Dati**: `WorkOrder.IsPlanLocked` (bool, default false) e `PlanLockedAt` (UTC, nullable) in `ApiCore`, con migration. Il blocco riusa `PlannedStartAt`, `PlannedEndAt` e `AssignedEquipmentId`; nessuna copia dei dati.
- **Motore**: il modello d'ingresso di `IPlanningEngine` riceve il flag di blocco. I lavori fissati sono intervalli fissi come i `Running`: mai spostati, occupano la macchina. I conflitti (predecessore che finisce dopo l'inizio fissato) sono segnalati, non risolti.
- **Servizio**: un `PlanLockService` (ApiFeatures, `PlanningFeature`) con anteprima e applicazione di «Fissa», «Sposta» e «Sblocca», transazionale con `IUnitOfWork`. La fine si calcola con `WorkingTimeCalculator`/estimatore esistente. Inizio nel passato o in un istante non lavorabile = 422 con motivo e prossimo istante lavorabile.
- **Ciclo di vita**: il blocco viene tolto alla partenza del lavoro e alla chiusura/annullamento dell'ordine. `allowedActions` del lavoro espone `Lock`, `Move`, `Unlock`.
- **Segnalazioni**: quattro nuovi tipi nel calcolatore esistente.
- **Frontend**: azioni nel pannello del lavoro, dialogo «Sposta…» con anteprima, icona lucchetto, legenda, sezione «Non spostati» nell'anteprima del piano, filtro, azioni sull'ordine. Nessun trascinamento.
- **Rigenerazione contratto** come ultimo task backend: `npx nx run api-client:generate-api`.

## Backend

- [x] 1.1 [Backend] Entità e migration: `IsPlanLocked`, `PlanLockedAt` su `WorkOrder` (default false/null), modelli di lavoro, timeline e ordine con `isPlanLocked`; `allowedActions` con `Lock`/`Move`/`Unlock` secondo stato del lavoro e dell'ordine. _Satisfies: REQ-1_
- [x] 1.2 [Backend] Ciclo di vita del blocco: tolto alla partenza (`Running`) e alla chiusura/annullamento dell'ordine; «Fissa» senza dati nuovi blocca il piano attuale (409 se non pianificato); «Sblocca» non modifica altro. Endpoint e test. _Satisfies: REQ-1_
- [x] 1.3 [Backend] Motore: estendere ingresso e uscita di `IPlanningEngine` con i lavori fissati come intervalli fissi (mai modificati, occupano capacità, successori dopo la fine), conflitti con predecessori e capacità, riepilogo con conteggio fissati; scenario nel banco di prova e test di determinismo. _Satisfies: REQ-3_
- [x] 1.4 [Backend] Anteprima/applicazione di pianificazione (`Order`, `Unplanned`, `All`, «Rimuovi piano»): i lavori fissati restano invariati e compaiono nell'elenco «non spostati» con i conflitti; «Rimuovi piano» non li tocca e li elenca. _Satisfies: REQ-3_
- [x] 1.5 [Backend] `PlanLockService` e endpoint di anteprima/applicazione di «Sposta» (`startAt`, `equipmentId`): fine calcolata sul calendario, errori 422 (passato, istante non lavorabile con motivo e prossimo istante, macchina non abilitata), 409 se il lavoro è cambiato; anteprima con conflitti (predecessore, successore obsoleto, capacità, ritardo) senza bloccare l'applicazione; l'applicazione fissa il lavoro. Test unitari e di integrazione. _Satisfies: REQ-2_
- [x] 1.6 [Backend] Azioni sull'ordine: «Fissa tutti i lavori aperti» e «Sblocca tutti» (transazione, riepilogo). _Satisfies: REQ-1, REQ-5_
- [x] 1.7 [Backend] Segnalazioni: fissato in conflitto con predecessore, su macchina chiusa/disattivata/non abilitata, oltre la capacità, in ritardo; causa nominata e suggerimento; causa distinta del piano `Obsoleto`. Filtro «solo fissati» sulla timeline e sulla lista lavori. Test. _Satisfies: REQ-4, REQ-5_
- [x] 1.8 [Backend] Rigenerare il contratto OpenAPI e il client (`npx nx run api-client:generate-api`) e riportare il riepilogo del contratto. _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5_

## Frontend

- [x] 2.1 [Frontend] Pannello del lavoro (timeline e Ordini di lavoro): azioni «Fissa», «Sposta…», «Sblocca» secondo `allowedActions`, con conferme e messaggi 409/422 specifici. _Satisfies: REQ-5_
- [x] 2.2 [Frontend] Dialogo «Sposta…»: data e ora nel fuso aziendale, scelta macchina tra quelle abilitate, anteprima in tempo reale di fine e conflitti, errori inline vicino al campo, «Applica e fissa»; accessibile e da tastiera. _Satisfies: REQ-2, REQ-5_
- [x] 2.3 [Frontend] Timeline: icona lucchetto sulle barre (anche strette), tooltip e nome accessibile con «fissato», voce «Fissato» in legenda, filtro «Solo fissati». _Satisfies: REQ-5_
- [x] 2.4 [Frontend] Anteprima di pianificazione: sezione «Non spostati (fissati)» con i conflitti e riepilogo; messaggio dopo «Rimuovi piano» con i lavori fissati rimasti. _Satisfies: REQ-3, REQ-5_
- [x] 2.5 [Frontend] Segnalazioni: nuovi tipi nel popover, nel dettaglio ordine e nella lista lavori; azioni «Fissa tutti» / «Sblocca tutti» sull'ordine con conferma; filtro «Solo fissati» nella lista lavori. _Satisfies: REQ-4, REQ-5_

## Documentazione

- [x] 3.1 [Lead] Aggiornare `doc/glossary.md` (blocco del piano come estensione, non previsto da ISA-95), `doc/structure.md` e verificare i criteri di accettazione. _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5_
