# Spec: Componenti collegati ai passi del ciclo (BOM-step link)

## 1. Context & Goals

Oggi, al rilascio di un ordine, **tutti** i componenti della distinta vengono assegnati al primo passo di partenza e **ogni** passo di partenza dell'ordine padre aspetta la fine dei figli semilavorati. È una semplificazione: in un tavolino il piano si monta solo all'assemblaggio, ma il sistema fa aspettare anche il taglio dei tubi.

Obiettivo: indicare **in quale passo del ciclo si consuma ciascun componente**, con un default che non richiede nulla all'utente, così che:

- i materiali richiesti di un ordine di lavoro siano quelli davvero usati in quel passo (l'operatore vede cosa gli serve);
- un ordine figlio semilavorato blocchi solo il passo che lo consuma, non tutto il padre (meno attese false, durata minima e percorso più lungo realistici);
- la distinta e il ciclo restino coerenti, senza possibilità di riferimenti rotti.

### Come lo fanno standard e prodotti noti (ricerca)

| Fonte | Come collega componente e passo | Cosa ne ricaviamo |
|---|---|---|
| **ISA-95 Part 2 / IEC 62264-1** | Il legame sta nell'*Operations/Product Segment*: ogni segmento ha *Material Specification* (materiale, uso `Consumed`/`Produced`/`Consumable`, quantità), «che dà solo la quantità usata in quel segmento». Nell'ordine di lavoro diventa *Material Requirement* del segmento. La distinta complessiva e il consumo per segmento sono quindi due viste dello stesso dato. | Il consumo si dichiara **per passo**; la BOM resta l'elenco dei componenti per 1 unità. Adottiamo `Consumed` (il resto è futuro). |
| **Odoo MRP** | Campo opzionale sulla riga di distinta «Consumed in Operation». Vuoto = primo passo. Serve anche per la *Manufacturing Readiness*: iniziare quando i componenti del primo passo sono disponibili. Una riga = una sola operazione. | Campo **opzionale** sulla riga BOM, default primo passo. |
| **SAP PP** | *Component allocation*: i componenti non assegnati vanno automaticamente alla **prima operazione** alla creazione dell'ordine; ogni riga BOM si assegna a **una sola** operazione (per usarne una parte in due operazioni si divide la riga). L'assegnazione si può fare dal ciclo o dalla distinta. | Stesso default; una riga = un passo; per dividere si creano due righe. |
| **ERPNext** | Campo *operation* sulla riga dell'articolo; nei BOM annidati l'operazione non viene ereditata dal figlio (fonte di errori segnalati dagli utenti). | Il collegamento è **per livello**: ogni BOM collega ai passi del **proprio** ciclo. |

Scelta di prodotto: stile Odoo/SAP, ISA-95-compatibile. Riga di distinta con passo di consumo **opzionale**; se vuoto vale il default (primo passo di partenza, come oggi), quindi **nessun dato esistente cambia comportamento**.

## 2. Requirements

### REQ-1: Assegnare un componente a un passo
- **User Story**: As a responsabile di produzione, I want indicare in quale passo del ciclo si usa ogni componente so that l'operatore trovi i materiali al passo giusto.
- **Acceptance Criteria**:
  - [ ] La riga di distinta ha un campo opzionale «Usato nel passo» (`ConsumedInStepId`, nullable) che punta a un passo del ciclo **dello stesso materiale padre**.
  - [ ] Vuoto = default automatico: il primo passo di partenza (numero di operazione più basso tra i passi senza predecessori), come oggi. L'interfaccia mostra il passo risolto: «Automatico (Op. 10 Taglio)».
  - [ ] Una riga è collegata a un solo passo. Per usare un componente in due passi si creano due righe (messaggio guida nell'interfaccia). Se la distinta non consente righe duplicate dello stesso componente, la spec lo risolve in fase di task (vedi domande aperte).
  - [ ] Il backend rifiuta (422, come gli altri errori di validazione) un passo che non appartiene al ciclo del materiale padre o che non esiste; rifiuta il collegamento se il padre non ha ciclo (campo disabilitato nell'interfaccia con spiegazione: «Definisci prima il ciclo»).
  - [ ] Funziona uguale per componenti grezzi e semilavorati.
- **Technical Constraints**: Nuova colonna nullable `ConsumedInStepId` su `BomItem` con FK verso `RoutingStep`; migration; nessun cambiamento al comportamento dei dati esistenti. Nessun metodo aggiunto alle interfacce repository.

### REQ-2: Il ciclo non può rompere la distinta
- **User Story**: As a responsabile di produzione, I want che modificare il ciclo non lasci componenti collegati a passi che non esistono so that non si creino riferimenti rotti.
- **Acceptance Criteria**:
  - [ ] Salvando un ciclo che elimina un passo a cui sono collegati dei componenti, il sistema **non perde dati in silenzio**: la risposta indica quali componenti erano collegati al passo (nome componente e passo).
  - [ ] Comportamento scelto (da confermare, vedi domande aperte): i componenti tornano al default automatico, e l'editor del ciclo, **prima di salvare**, mostra la conferma «I componenti X, Y tornano al primo passo».
  - [ ] Eliminare o disattivare un materiale/segmento non lascia mai un `ConsumedInStepId` orfano (FK con `SetNull` o controllo esplicito; coperto da test).
  - [ ] Il ciclo salvato non cambia mai gli ordini già rilasciati (istantanea).
- **Technical Constraints**: Il PUT del ciclo è già transazionale (`IUnitOfWork`): la ripulitura dei collegamenti sta nella stessa transazione. Se i passi vengono ricreati con nuovi id, i collegamenti per passo preservato vanno mantenuti (verifica nel task: identità dei passi nel PUT).

### REQ-3: Rilascio ordine con materiali per passo
- **User Story**: As a responsabile di produzione, I want che al rilascio ogni ordine di lavoro abbia i materiali del suo passo so that l'operatore veda cosa gli serve, e solo quello.
- **Acceptance Criteria**:
  - [ ] Al rilascio i `WorkOrderMaterial` sono creati sul lavoro del passo collegato (o del primo passo di partenza se vuoto), non più tutti sul primo.
  - [ ] Un componente semilavorato con ordine figlio fa dipendere **solo il lavoro che lo consuma** dall'ultimo lavoro del figlio; i passi di partenza non collegati non aspettano più il figlio. Se il lavoro consumatore non è un passo di partenza, la dipendenza dal figlio si aggiunge a quelle del ciclo.
  - [ ] Nessun ciclo di dipendenze può nascere (il figlio è un ordine separato): verificato da test.
  - [ ] Durata minima, percorso più lungo e panoramica riflettono le nuove dipendenze senza codice duplicato (si riusa `EarliestFinish`).
  - [ ] Ordini già rilasciati prima di questa feature restano invariati. I dati seed esistenti (nessun collegamento) producono lo stesso risultato di prima.
  - [ ] Il seed di sviluppo include un caso con collegamento esplicito (es. i tavolini: il piano consumato solo all'assemblaggio, la struttura al primo passo) per mostrare la differenza nella panoramica; gli ordini esistenti mantengono codici e stati.
- **Technical Constraints**: Modifica il release planner (`ProductionOrderPlanner`) e `ProductionOrderService`; aggiornare test unitari e di integrazione; nessun `DbContext` nel codice di prodotto.

### REQ-4: Distinta: scegliere il passo senza sbagliare
- **User Story**: As a responsabile di produzione, I want scegliere il passo direttamente nella riga della distinta so that non devo cambiare pagina né ricordare numeri.
- **Acceptance Criteria**:
  - [ ] Nella tabella/modifica della riga BOM c'è la selezione «Usato nel passo», con voci «Automatico (Op. N Nome)» e i passi del ciclo del padre nel formato «Op. 10 · Taglio tubolare» (nome, non solo numero).
  - [ ] Se il padre non ha ciclo: campo disabilitato con testo «Il materiale non ha un ciclo: i componenti vanno al primo passo quando ne definirai uno».
  - [ ] La colonna è visibile anche nella lista componenti, così il quadro è leggibile senza aprire ogni riga.
  - [ ] Errori del backend (400/409) mostrati in modo specifico, con indicazione dell'azione.
- **Technical Constraints**: Componenti helm e token semantici; select con `itemToString`; client Orval rigenerato, mai modificato a mano.

### REQ-5: Ciclo e panoramica mostrano i materiali dei passi
- **User Story**: As a responsabile di produzione, I want vedere nel ciclo e nella panoramica quali componenti si usano in ogni passo so that capisco il flusso dei materiali a colpo d'occhio.
- **Acceptance Criteria**:
  - [ ] Nel pannello laterale del passo del ciclo: elenco dei componenti consumati (nome, quantità per unità, unità) con link alla distinta del materiale; vuoto = «Nessun componente».
  - [ ] Nel nodo del ciclo: indicatore compatto del numero di componenti (es. icona + «2»), con testo accessibile.
  - [ ] Nel pannello della panoramica del lavoro: materiali richiesti per quel lavoro (già presenti nei dati dell'ordine di lavoro; se un componente è un semilavorato, link all'ordine figlio).
  - [ ] Nessuna modifica diretta dei collegamenti dal ciclo in questa spec (si fa dalla distinta: una sola posizione di editing).
- **Technical Constraints**: Il modello del ciclo (GET) include per passo i componenti consumati; aggiornare il contratto e rigenerare il client.

## 3. Layers impacted

- **Backend**: `BomItem` (campo, validazione, modello), `RoutingService` (ripulitura collegamenti e avviso), planner e servizio di rilascio, seed, test, migration, contratto OpenAPI.
- **Web**: pagina distinta (selezione e colonna), pagina ciclo (elenco materiali nel pannello, indicatore nel nodo, conferma al salvataggio), pannello della panoramica.
- **Nx workspace**: nessun impatto.

## 4. Impact Assessment

- Affected: `libs/api-core` (entità + migration), `libs/api-features/BomFeature`, `RoutingFeature`, `ProductionOrderFeature` (planner, service, overview), `libs/api-features/SampleDataFeature`/seeder ordini, `apps/web/src/app/features/bom`, `features/routing`, `features/production-orders/overview`, `doc/glossary.md`, `doc/structure.md`.
- Breaking changes: **no** per l'API (campo nuovo opzionale). Comportamento: cambia solo per righe con collegamento esplicito; nessuna riga lo ha dopo la migration. Gli ordini già rilasciati non cambiano.
- Deviazioni da ISA-95 (da annotare in `doc/glossary.md`): il consumo per passo è un campo della riga di distinta, non un'entità *Material Specification* del segmento; solo uso `Consumed`; nessuna quantità diversa per passo (una riga = un passo).

## 5. Fuori scopo

Giacenze e verifica di disponibilità (non c'è magazzino: la *readiness* stile Odoo viene dopo), consumo effettivo registrato per lavoro (*Material Actual*), prodotti secondari/co-prodotti, quantità diverse da quella della riga, modifica dei collegamenti dal ciclo.

## 6. Decisioni

1. **Passo eliminato dal ciclo**: i componenti collegati tornano al default automatico; l'editor del ciclo chiede conferma prima di salvare elencando i componenti interessati (confermato).
2. **Stesso componente in due passi**: la distinta ha un indice unico (padre, componente): resta così. Una riga = un componente = un passo. Motivo: evita righe duplicate che raddoppiano per errore le quantità; il caso «lo stesso componente in due passi» è raro nelle micro imprese. Se servirà, basterà estendere l'indice a (padre, componente, passo). Limite annotato in `doc/glossary.md` (proposta del lead, da confermare).
3. **Più passi di partenza**: il default è il numero di operazione più basso, mostrato in chiaro come «Automatico (Op. N Nome)» (confermato).
