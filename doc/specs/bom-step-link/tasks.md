# Tasks: Componenti collegati ai passi del ciclo

## Technical Strategy

- Campo nullable `BomItem.ConsumedInStepId` (FK a `RoutingStep`, `OnDelete: SetNull` come rete di sicurezza; il servizio del ciclo gestisce comunque il caso in modo esplicito). Null = default automatico = primo passo di partenza (come oggi): nessun dato esistente cambia.
- Indice unico (padre, componente) invariato: una riga = un componente = un passo.
- La distinta è l'unico punto di modifica del collegamento. Ciclo e panoramica lo mostrano in sola lettura.
- Il planner calcola per ogni componente il passo effettivo (`ConsumedInStepId` o `FirstStartStep`): i `WorkOrderMaterial` vanno sul lavoro di quel passo; la dipendenza ultimo lavoro del figlio -> lavoro consumatore sostituisce «figlio -> tutti i passi di partenza». Si riusa `EarliestFinish`/`CriticalPath`.
- Il ciclo, al salvataggio, riporta a null i collegamenti dei passi eliminati nella stessa transazione e li restituisce nella risposta; l'editor li confronta prima del salvataggio con i componenti per passo già caricati col GET e chiede conferma.
- La panoramica riceve i materiali per lavoro nell'endpoint `overview` (oggi non li contiene).
- Ordine: Backend (ultimo task = rigenerazione contratto), poi Frontend. Nessun task Nx.

## Backend

- [x] 1.1 [Backend] `BomItem.ConsumedInStepId` nullable + FK a `RoutingStep` (`SetNull`) + migration (`AddBomConsumedInStep`). Nessun metodo nei repository. _Satisfies: REQ-1_
- [x] 1.2 [Backend] BOM: richiesta e modello con `consumedInStepId`; il modello restituisce anche `consumedInStep` (id, numero operazione, nome fase) e `effectiveStep` (passo risolto, `isAutomatic`). Validazione: passo esistente e del ciclo del padre (400), padre senza ciclo con passo indicato (400); messaggi in italiano con l'azione da fare. Cambiare il ciclo del padre in seguito non rompe il collegamento (vedi 1.3). _Satisfies: REQ-1, REQ-4_
- [x] 1.3 [Backend] Ciclo: (a) il GET restituisce per passo i componenti consumati (materiale, quantità per unità, unità) più `isAutomatic` per quelli senza collegamento esplicito sul primo passo di partenza; (b) il PUT, nella stessa transazione, azzera i collegamenti dei passi eliminati e restituisce l'elenco `unassignedComponents` (componente, passo precedente); verifica e documenta se i passi mantengono l'id tra i PUT (altrimenti preservare i collegamenti dei passi che restano, per `OperationNumber`/id); un passo cambiato di numero non perde collegamenti. _Satisfies: REQ-2, REQ-5_
- [x] 1.4 [Backend] Rilascio: `ProductionOrderPlanner` e `ProductionOrderService` assegnano i `WorkOrderMaterial` al lavoro del passo effettivo; la dipendenza dal figlio (ultimo lavoro -> lavoro che consuma il semilavorato) sostituisce il vincolo su tutti i passi di partenza; nessun ciclo di dipendenze; durata minima/percorso più lungo coerenti. Ordini già rilasciati e dati senza collegamenti: risultato identico a prima (test di non regressione). _Satisfies: REQ-3_
- [x] 1.5 [Backend] Endpoint `overview`: ogni lavoro include i `materials` (codice, nome, quantità richiesta, unità, `childOrderId` se semilavorato). _Satisfies: REQ-5_
- [x] 1.6 [Backend] Seed di sviluppo: aggiungere nei master data un collegamento esplicito realistico (es. sedie: tubi al taglio, bulloneria all'assemblaggio) senza cambiare codici e stati degli ordini esistenti e senza cambiare i conteggi già testati dei tavolini; se i conteggi di un ordine cambiano, aggiornare i test e riportarlo. Il seeder lavora solo su tabelle vuote: riportare cosa serve per vedere i dati su un DB esistente. _Satisfies: REQ-3_
- [x] 1.7 [Backend] Test unitari (default, passo esplicito, componente semilavorato collegato a un passo non di partenza, passo non valido, nessun ciclo, non regressione) e di integrazione (validazione 400, ciclo che elimina un passo collegato: azzeramento e `unassignedComponents`, rilascio con materiali sul passo giusto e dipendenza dal figlio solo sul consumatore, overview con materiali). `npx nx run ApiTests:unit-test`, `npx nx run ApiTests.Integration:unit-test`. _Satisfies: REQ-1, REQ-2, REQ-3_
- [x] 1.8 [Backend] Aggiornare il contratto: `npx nx run api-client:generate-api`, poi `npx nx run-many -t build lint`. Riportare nomi dei tipi e forma dei campi nuovi. _Satisfies: REQ-1..REQ-5_

## Frontend

- [x] 2.1 [Frontend] Distinta: select «Usato nel passo» (voci «Automatico (Op. N Nome)» e passi «Op. 10 · Nome»), disabilitata con spiegazione se il padre non ha ciclo; colonna nella lista dei componenti; errori 400/409 mostrati con il testo del backend; usare i componenti helm e `itemToString`. _Satisfies: REQ-1, REQ-4_
- [x] 2.2 [Frontend] Ciclo: nel pannello del passo l'elenco dei componenti consumati (con link alla distinta); nel nodo l'indicatore compatto con testo accessibile; al salvataggio, se tra i passi eliminati ce ne sono con componenti collegati, conferma prima di inviare («I componenti X, Y tornano al primo passo»); dopo il salvataggio, conferma coerente con `unassignedComponents`. Il diagramma resta montato e in sola lettura di default. _Satisfies: REQ-2, REQ-5_
- [x] 2.3 [Frontend] Panoramica: nel pannello laterale del lavoro, materiali richiesti (con link all'ordine figlio per i semilavorati); il caso «nessun materiale» è esplicito. _Satisfies: REQ-5_
- [x] 2.4 [Frontend] `npx nx lint web` e `npx nx build web`; verificare i flussi nel browser; riportare cosa non è stato verificato. _Satisfies: REQ-1..REQ-5_

## Lead

- [x] 3.1 [Lead] Aggiornare `doc/glossary.md` (campo `ConsumedInStepId`, deviazioni, limite «un componente, un passo», riga «Product Definition» tra i «Planned») e `doc/structure.md` (sezione ciclo/BOM e rilascio). _Satisfies: tutti_
- [x] 3.2 [Lead] Verifica: `npx nx run-many -t lint build`, test unitari e di integrazione, controllo DOM nel browser di distinta, ciclo e panoramica sui dati del seed, verifica dei criteri di accettazione. _Satisfies: tutti_
