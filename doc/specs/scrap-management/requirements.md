# Spec: Gestione degli scarti

## 1. Context & Goals

Oggi la chiusura di una sessione di lavoro (Pausa/Termina) registra due numeri: pezzi buoni e pezzi scartati (`WorkOrderResponse.ScrapQuantity`). Non si sa **perché** si scarta, né si può **correggere** una registrazione sbagliata, né **analizzare** gli scarti.

Obiettivi:

- classificare gli scarti con un **catalogo di motivi** semplice, filtrato per fase;
- registrare **più motivi** nella stessa sessione (es. 2 «Graffio» + 1 «Misura errata»);
- non bloccare mai una dichiarazione: il motivo è **facoltativo**, ma facile da dare;
- poter **correggere** una registrazione errata con traccia;
- un **report minimo** (Pareto) per decidere dove intervenire;
- raggruppare in modo logico le voci del menu, ora che le funzioni crescono.

### Ricerca (standard e prodotti)

| Fonte | Cosa dice | Cosa ne ricaviamo |
|---|---|---|
| **ISA-95** (Job Response, *Material Produced Actual*, *disposition*) | Il risultato di un lavoro riporta quantità effettive di materiale prodotto e la *disposition* (usa così com'è, rilavora, ripara, scarta, rendi al fornitore). Non risulta un catalogo standard di motivi: è lasciato all'implementazione come proprietà. (Fonti secondarie: la norma è a pagamento.) | Lo scarto è un dato della registrazione; il catalogo dei motivi è una scelta nostra. La *disposition* resta estendibile (rilavorazione fuori scopo). |
| **Odoo** | Motivo dello scarto **opzionale**, spesso come etichette. | Motivo facoltativo. |
| **SAP PP** | Lo scarto si conferma per operazione e riduce la quantità disponibile per le operazioni successive. | Segnalare l'effetto sulle fasi successive, senza ricalcoli automatici. |
| **Prassi OEE/MES** | I codici funzionano se la lista è **corta**, legata alla fase e a un solo tocco; con liste lunghe o un «Altro» comodo tutto finisce in «Altro». | Filtro per fase, voci a bottoni, «senza motivo» visibile nei report. |

Decisioni di prodotto già prese con l'utente: catalogo globale con filtro per fase; motivo facoltativo; più motivi per registrazione; correzione inclusa; pagina di gestione per il responsabile.

## 2. Requirements

### REQ-1: Catalogo dei motivi di scarto
- **User Story**: As a responsabile di produzione, I want mantenere un elenco di motivi di scarto so that gli operatori scelgano con un tocco.
- **Acceptance Criteria**:
  - [ ] Entità `ScrapReason`: `Code` (unico, maiuscolo), `Name`, `Description` opzionale, `IsActive` (soft-delete, mai cancellazione fisica).
  - [ ] Un motivo può essere collegato a zero o più fasi di processo (`ProcessSegment`, relazione molti-a-molti). Nessuna fase collegata = vale per tutte le fasi.
  - [ ] Pagina «Motivi di scarto» (lista, crea, modifica, attiva/disattiva, scelta delle fasi con filtro e ricerca) coerente con «Fasi di processo».
  - [ ] Un motivo disattivato non viene più proposto ma resta leggibile nello storico.
  - [ ] Codice duplicato → 409 con messaggio specifico (nessun «riprova»).
  - [ ] Il seed di sviluppo contiene un catalogo realistico (es. «Graffio», «Misura errata», «Saldatura incompleta», «Verniciatura difettosa», «Materiale difettoso», «Errore di montaggio»), alcuni validi per tutte le fasi e altri per fasi specifiche, usato dagli ordini seed.
- **Technical Constraints**: Pattern delle feature esistenti (controller → servizio con Result → repository, mapping manuale, enum a stringa); nuove tabelle e migration; interfacce repository vuote; nessun `DbContext` nel codice di prodotto.

### REQ-2: Dichiarare lo scarto con uno o più motivi
- **User Story**: As an operatore, I want dichiarare lo scarto e il suo motivo (anche più motivi) senza fatica so that non rallento il lavoro.
- **Acceptance Criteria**:
  - [ ] La richiesta di chiusura sessione (Pausa/Termina) accetta `scrapEntries[]` = `{ scrapReasonId?: number | null, quantity }`. `scrapReasonId` vuoto = «senza motivo».
  - [ ] Regola: se `scrapEntries` è presente, la somma delle quantità deve essere uguale a `scrapQuantity` (altrimenti 422 con messaggio chiaro); se è assente, tutto lo scarto è «senza motivo». Compatibile col client attuale.
  - [ ] Il backend accetta solo motivi attivi e validi per la fase del lavoro (motivo senza fasi = tutte); altrimenti 422.
  - [ ] Ogni riga conserva un'istantanea di codice e nome del motivo, così lo storico non cambia se il catalogo viene rinominato.
  - [ ] Quantità: ≥ 0, non negative; righe con quantità 0 ignorate.
  - [ ] Dichiarare scarto senza motivo non viene mai bloccato.
  - [ ] Il dialogo di chiusura dell'ordine di lavoro propone i motivi della fase come righe motivo + quantità (aggiungi/rimuovi riga), con «Senza motivo» come voce esplicita; la somma è calcolata e coincide con lo scarto; nota libera facoltativa; controlli da tastiera.
  - [ ] Se dopo la chiusura i pezzi buoni restano sotto il pianificato, l'interfaccia lo segnala in modo non bloccante («Mancano N pezzi: i passi successivi ne riceveranno meno»); nessun ricalcolo automatico delle quantità.
- **Technical Constraints**: `WorkOrderResponse.ScrapQuantity` resta, come somma delle righe; nuova tabella `WorkOrderResponseScrap` (response, reason FK nullable con SetNull, snapshot code/name, quantity); modelli di dettaglio del lavoro con le righe di scarto per sessione.

### REQ-3: Correggere una registrazione errata
- **User Story**: As a responsabile di produzione, I want correggere i numeri di una sessione chiusa so that i dati restino corretti senza perdere la traccia.
- **Acceptance Criteria**:
  - [ ] Si può correggere una sessione **chiusa** (buoni, scarti con motivi, nota) con una nota di correzione obbligatoria. La sessione in corso non si corregge: si chiude con i valori giusti.
  - [ ] Ogni correzione crea una riga di traccia (`WorkOrderResponseCorrection`: valori precedenti, valori nuovi, nota, data); non si cancella né si sovrascrive senza traccia. Lo storico delle correzioni è leggibile.
  - [ ] Le stesse regole del REQ-2 valgono sulla correzione (somma = scarto, motivi validi).
  - [ ] Non si corregge se l'ordine di produzione è `Completed` o `Cancelled` (409 con spiegazione); il lavoro `Aborted` e quelli degli ordini aperti sono correggibili.
  - [ ] Dopo la correzione totali, avanzamento e percentuale dell'ordine (e quindi la panoramica) sono ricalcolati; gli stati dei lavori non cambiano.
  - [ ] Interfaccia: nella pagina Ordini di lavoro, elenco delle sessioni di un lavoro con azione «Correggi», nota di correzione, indicatore «corretta» con lo storico.
- **Technical Constraints**: Transazione con `IUnitOfWork`; endpoint esplicito (es. `POST /api/work-orders/{id}/responses/{responseId}/corrections`); chi ha corretto non è tracciato finché non esiste l'identità dell'operatore (da riprendere nella spec «vista operatore»).

### REQ-4: Report minimo degli scarti
- **User Story**: As a responsabile di produzione, I want vedere dove si scarta di più so that posso intervenire.
- **Acceptance Criteria**:
  - [ ] Pagina «Scarti»: periodo selezionabile, totale scarti e percentuale sul prodotto (scarto / (buoni + scarto)); classifica per motivo, per fase e per materiale; quota «senza motivo» evidenziata.
  - [ ] Endpoint di sola lettura con filtri `from`, `to`; i dati vengono da registrazioni, con le correzioni applicate.
  - [ ] Stati vuoto, caricamento ed errore gestiti; tabelle e barre con testo (non solo colore), numeri allineati.
  - [ ] I dati seed producono un report leggibile (almeno 5-6 righe con più motivi).
- **Technical Constraints**: Nessun grafico complesso: barre orizzontali semplici con componenti helm; aggregazioni sul backend; contratto OpenAPI e client rigenerato.

### REQ-5: Menu raggruppato
- **User Story**: As an utente, I want un menu organizzato per area so that trovo le voci senza cercarle.
- **Acceptance Criteria**:
  - [ ] Menu in gruppi con etichetta: **Dashboard** (in alto, senza gruppo); **Produzione**: Ordini di produzione, Ordini di lavoro, Scarti; **Anagrafiche**: Materiali, Fasi di processo, Motivi di scarto.
  - [ ] Distinta e ciclo restano raggiungibili dalle pagine del materiale (nessuna voce di menu nuova).
  - [ ] Evidenziazione della voce attiva anche per le rotte figlie (es. `/materials/1/bom` evidenzia Materiali); sidebar compressa e navigazione da tastiera funzionano.
  - [ ] Struttura dati del menu a gruppi, pronta per le voci future (es. «Officina»).
- **Technical Constraints**: Componenti `hlm-sidebar` (group label); icone `@ng-icons/lucide`; nessun colore fisso.

## 3. Layers impacted

- **Backend**: nuove entità (`ScrapReason`, collegamento con `ProcessSegment`, `WorkOrderResponseScrap`, `WorkOrderResponseCorrection`), migration, servizi e controller per catalogo, chiusura sessione, correzione e report; seed; test; contratto OpenAPI.
- **Web**: pagina «Motivi di scarto», dialogo di chiusura, elenco sessioni e correzione, pagina «Scarti», menu raggruppato.
- **Nx workspace**: nessun impatto.

## 4. Impact Assessment

- Affected: `libs/api-core` (entità, migration), nuova feature `ScrapFeature` in `libs/api-features` e modifiche a `ProductionOrderFeature` (chiusura sessione, modelli di dettaglio), seeder, `libs/api-tests*`, `apps/web/src/app/core/layouts` e `features/work-orders`, nuove `features/scrap-reasons` e `features/scrap`, `doc/glossary.md`, `doc/structure.md`.
- Breaking changes: **no**. `scrapEntries` è opzionale e `scrapQuantity` resta nella richiesta. Il menu cambia solo nella presentazione.
- Deviazioni da ISA-95 (da annotare in `doc/glossary.md`): lo scarto è un attributo della registrazione con motivi in un catalogo proprio, senza *disposition* (rilavorazione, riparazione) né lotti; le correzioni sono righe di traccia separate, non un nuovo Job Response.

## 5. Fuori scopo

Rilavorazione/riparazione (*disposition*), scarto di materia prima e sfridi, costo degli scarti, gerarchie di motivi a più livelli, identità dell'operatore (spec «vista operatore»), ricalcolo automatico delle quantità dei passi successivi o ordini integrativi.

## 6. Decisioni

1. Catalogo globale con filtro per fase (collegamento a `ProcessSegment`, non al passo del ciclo): confermato.
2. Motivo facoltativo, mai bloccante: confermato.
3. Più motivi per registrazione: confermato.
4. Correzioni incluse, con traccia: confermato. Regola proposta dal lead: non correggibile se l'ordine è `Completed`/`Cancelled`.
5. Pagina di gestione del catalogo per il responsabile: confermato.
6. Menu raggruppato come in REQ-5 (proposta del lead).
