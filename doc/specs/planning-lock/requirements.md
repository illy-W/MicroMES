# Spec: Blocco manuale del piano (lavori fissati)

## 1. Context & Goals

Oggi il piano lo decide solo il motore: l'operatore agisce sui vincoli (priorità, date richieste, macchina assegnata, calendari) e ripianifica. Non può dire «questo lavoro parte giovedì alle 8 su SAL-1» e non può impedire che una ripianificazione completa sposti un lavoro che ha già concordato con il cliente o con l'officina.

Obiettivi, per una micro impresa:

1. **Fissare** un lavoro a un inizio e a una macchina: il motore non lo sposta mai e pianifica gli altri attorno ad esso.
2. **Spostare a mano** un lavoro (nuovo inizio e/o nuova macchina) con anteprima di fine e conflitti prima di confermare.
3. **Nessuna modifica silenziosa**: i conflitti causati da un lavoro fissato (predecessori in ritardo, macchina chiusa, capacità superata) vengono mostrati e segnalati, non corretti di nascosto.
4. Sbloccare in qualsiasi momento, tornando al piano calcolato dal motore.

Fuori scopo: vedi §5 (trascinamento delle barre nella timeline, blocco della sola macchina o del solo orario, blocco di intere sequenze con un gesto, storico dei cambi di piano, ottimizzazione).

### Ricerca (standard e prodotti)

Cosa è **verificato** e cosa no:

| Fonte | Cosa dice | Stato |
|---|---|---|
| **ISA-95 Parte 3** (indice e glossario letti dall'anteprima ANSI/ISA-95.00.03-2013) | L'attività *Detailed production scheduling* (6.6) comprende *finite capacity scheduling* (6.6.4, definita come: il lavoro è pianificato in modo che nessuna capacità pianificata superi quella disponibile) e produce il *Work schedule* (6.6.6), dal quale i *job order* vengono inviati al *dispatching* (6.7). | Verificato su indice e definizione 3.1.1. Il testo dei paragrafi 6.6.3-6.6.6 non è consultabile senza acquistare la norma: non verificato. |
| **ISA-95 Parte 2/4 e B2MML** (job order) | Il job order ha inizio e fine pianificati, priorità e risorse richieste (equipaggiamento, persone, materiali); *Job Order* = unità minima del *work schedule* (OPC UA for ISA-95, Part 4). | Verificato su OPC Foundation (Job Order). |
| **Blocco/«fissaggio» nella norma** | Non ho trovato un attributo o un concetto di «lavoro bloccato/fissato» nella norma. | **Non verificato; probabilmente non previsto.** Il blocco è quindi un'estensione di MicroMES e va documentato come deviazione nel glossario. |
| **Dynamics 365 SCM** (job scheduling) | La capacità finita considera le prenotazioni già presenti sulla risorsa e impedisce sovrapposizioni; la risorsa è scelta per durata minima o priorità. | Verificato su Microsoft Learn. Non descrive un blocco manuale. |
| **Odoo MRP, SAP PP/DS, Asprova** (da conoscenza generale, **non verificata ora**) | Odoo: l'utente può spostare un lavoro nel Gantt e la data diventa manuale; SAP PP/DS: ordini «fixed»/pianificazione ferma (*planning time fence*) non toccati dall'ottimizzatore; Asprova: operazioni «fixed» attorno a cui si pianifica. | **Secondario/non verificato.** Pratica comune: i lavori fissati diventano intervalli immobili per il motore. |

Ricaviamo: i lavori fissati sono **intervalli già impegnati** sulla macchina (come già avviene per i lavori `Running`), il motore pianifica gli altri attorno ad essi, e i conflitti si segnalano invece di essere corretti di nascosto.

## 2. Requirements

### REQ-1: Fissare e sbloccare un lavoro
- **User Story**: As a responsabile di produzione, I want fissare un lavoro al suo orario e alla sua macchina so that il motore non lo sposta.
- **Acceptance Criteria**:
  - [ ] `WorkOrder.IsPlanLocked` (bool, default false) e `PlanLockedAt` (UTC, nullable). Un lavoro fissato ha sempre inizio, fine pianificati e macchina (se la fase ha macchine); i dati sono quelli del piano esistente (`PlannedStartAt`, `PlannedEndAt`, `AssignedEquipmentId`), non una copia.
  - [ ] «Fissa» senza dati nuovi blocca il piano attuale (errore 409 specifico se il lavoro non è pianificato: «pianifica il lavoro oppure scegli un orario»). «Sblocca» riporta il lavoro al piano calcolato dal motore (resta dov'è finché non si ripianifica).
  - [ ] Solo lavori `Waiting`, `Ready`, `Interrupted` e di ordini `Released`/`InProgress` sono fissabili; per `Running`, `Ended`, `Aborted` (e ordini chiusi) 409 con spiegazione.
  - [ ] Quando un lavoro fissato **parte** (`Running`) il blocco viene tolto: da quel momento valgono le date reali (nessuna modifica silenziosa: il blocco scompare con la partenza, è comportamento documentato).
  - [ ] Se l'ordine viene annullato/chiuso, i blocchi dei suoi lavori vengono tolti.
  - [ ] Fissare/sbloccare non modifica altro (stato del lavoro, macchina, date, altri lavori).
- **Technical Constraints**: Colonne nullable + migration; nessuna regola nelle macchine a stati; repository vuoti e nessun `DbContext`; il blocco non è una nuova macchina a stati.

### REQ-2: Spostare un lavoro a mano (orario e macchina)
- **User Story**: As a responsabile di produzione, I want scegliere l'inizio e la macchina di un lavoro so that rispetto accordi che il motore non conosce.
- **Acceptance Criteria**:
  - [ ] Endpoint di **anteprima** (nessun salvataggio) e di **applicazione**: ricevono `startAt` (data e ora, mostrate e inserite nel fuso orario aziendale), `equipmentId` opzionale (solo macchine attive abilitate per la fase; se omesso resta quella attuale), e restituiscono la **fine calcolata** (minuti standard ÷ efficienza, sul calendario effettivo: turni, pause, eccezioni) senza che l'utente la digiti; applicando, il lavoro risulta fissato (REQ-1).
  - [ ] L'inizio non può essere nel passato (422 «l'inizio deve essere dopo adesso») e deve cadere in un istante lavorabile della macchina scelta: se non lo è, 422 con il motivo («SAL-1 è chiusa il 25/12: chiusura aziendale») e il prossimo orario lavorabile come suggerimento; **il sistema non sposta l'orario in silenzio**.
  - [ ] L'anteprima elenca i **conflitti** del risultato, senza bloccare l'applicazione: predecessore che finisce dopo l'inizio scelto (con minuti di conflitto), successore che inizia prima della nuova fine (diventa piano obsoleto), macchina con capacità superata nell'intervallo (con i lavori coinvolti), fine oltre la fine richiesta dell'ordine (ritardo, con i giorni). Si applica solo dopo aver visto l'anteprima.
  - [ ] Dopo l'applicazione, i lavori successori non vengono spostati: restano al loro piano e, se in conflitto, il loro stato del piano diventa `Obsoleto` con la segnalazione (REQ-4). Si ripianificano a scelta dell'utente.
  - [ ] Cambio macchina: se la fase non ha macchine collegate l'assegnazione non è richiesta; se `equipmentId` non è abilitata per la fase → 422 specifico.
  - [ ] Concorrenza: l'applicazione fallisce con 409 specifico se tra anteprima e applicazione il lavoro è cambiato di stato (es. è partito).
  - [ ] Messaggi 409/422 specifici; nessuna modifica parziale.
- **Technical Constraints**: Il calcolo della fine riusa `WorkingTimeCalculator`/estimatore esistenti (nessun codice duplicato); anteprima e applicazione condividono lo stesso servizio transazionale (`IUnitOfWork`); date in UTC nel contratto.

### REQ-3: Il motore rispetta i lavori fissati
- **User Story**: As a responsabile di produzione, I want che la ripianificazione non sposti i lavori fissati so that gli accordi restano validi.
- **Acceptance Criteria**:
  - [ ] Nelle anteprime e applicazioni con ambito `Order`, `Unplanned` e `All`, i lavori fissati **non vengono mai modificati** (date, macchina) e occupano la loro macchina come intervalli fissi, come i lavori `Running`.
  - [ ] Gli altri lavori vengono pianificati attorno: non possono sovrapporsi a un intervallo fisso oltre la capacità della macchina.
  - [ ] I successori di un lavoro fissato iniziano dopo la sua fine pianificata. Se un **predecessore** libero non riesce a finire prima dell'inizio del lavoro fissato, il motore non sposta il lavoro fissato: lo segnala come conflitto con il nome del predecessore e di quanti minuti.
  - [ ] **Scadenza dei predecessori**: per ogni lavoro fissato, i suoi predecessori (anche tra ordini figlio e padre) hanno una scadenza = inizio fissato meno le durate della catena che li separa dal fissato. I lavori con una scadenza vengono pianificati **prima** degli altri, ordinati per scadenza crescente (poi con le regole usuali), anche se il loro ordine ha priorità più bassa. Se la catena non entra comunque, il lavoro fissato non si sposta e il conflitto resta segnalato con i minuti. Il motivo del lavoro anticipato è esplicito («anticipato per rispettare il lavoro fissato OP-…/40»).
  - [ ] L'anteprima mostra i lavori fissati con un'indicazione esplicita («fissato, non spostato») e il numero dei conflitti trovati; il riepilogo conta i lavori fissati.
  - [ ] «Rimuovi piano» di un ordine non tocca i lavori fissati (restano pianificati): la risposta li elenca e propone di sbloccarli.
  - [ ] Il motore resta una funzione pura e deterministica; il banco di prova ha almeno uno scenario con lavori fissati (stesso piano a parità di input).
- **Technical Constraints**: Estensione del modello d'ingresso di `IPlanningEngine` (flag di blocco e intervallo fisso); nessuna dipendenza nuova.

### REQ-4: Segnalazioni per i lavori fissati
- **User Story**: As a responsabile di produzione, I want sapere quando un lavoro fissato crea un problema so that decido come intervenire.
- **Acceptance Criteria**:
  - [ ] Nuove segnalazioni (stesso formato e punti di comparsa delle esistenti: popover «Segnalazioni», dettaglio ordine, lista lavori): **fissato in conflitto con un predecessore**, **fissato su macchina chiusa/disattivata/non più abilitata** (calendario o macchina cambiati dopo il blocco), **fissato oltre la capacità** della macchina, **fissato in ritardo** rispetto alla fine richiesta; ognuna con la causa nominata e il suggerimento («sblocca il lavoro», «anticipa il predecessore», «assegna un'altra macchina»).
  - [ ] Nessuna correzione automatica: la segnalazione resta finché l'utente non agisce.
  - [ ] Il piano `Obsoleto` ha una causa distinta per i lavori fissati.
- **Technical Constraints**: Stesso calcolatore delle segnalazioni del motore; nessun dato duplicato.

### REQ-5: Interfaccia (timeline, pannello, anteprima)
- **User Story**: As a responsabile di produzione, I want fissare e spostare i lavori dall'interfaccia so that non devo cambiare pagina.
- **Acceptance Criteria**:
  - [ ] Nel pannello del lavoro (timeline e pagina Ordini di lavoro): azioni **«Fissa»**, **«Sposta…»** (dialogo con data e ora nel fuso aziendale, scelta macchina tra quelle abilitate, anteprima in tempo reale della fine e dei conflitti, pulsante «Applica e fissa») e **«Sblocca»**. Ogni azione è disponibile solo quando consentita (`allowedActions`).
  - [ ] Le barre dei lavori fissati hanno un'icona lucchetto, visibile anche su barre strette (angolo) e nel tooltip («Fissato»); il testo accessibile include «fissato».
  - [ ] Legenda: nuova voce «Fissato» con l'icona.
  - [ ] Nell'anteprima di pianificazione i lavori fissati sono raggruppati in una sezione «Non spostati (fissati)» con i conflitti.
  - [ ] Filtro «Solo fissati» nella timeline e nella lista lavori.
  - [ ] Azione sull'ordine: «Fissa tutti i lavori aperti» e «Sblocca tutti» (con conferma).
  - [ ] Accessibilità: dialogo con etichette, focus gestito, errori 422 mostrati inline vicino al campo; tastiera completa.
  - [ ] Il trascinamento delle barre **non** è incluso; il dialogo «Sposta…» è l'unico modo di cambiare l'orario.
- **Technical Constraints**: Componenti helm, token semantici, nessun colore fisso; client Angular generato (non modificare a mano `generated/**`); date mostrate nel fuso aziendale con gli helper esistenti di `features/planning`.

## 3. Layers impacted

- **Backend**: sì (`PlanningFeature`: modello e risultato del motore, servizio di blocco/spostamento e anteprima; `ProductionOrderFeature`: campi sul lavoro, `allowedActions`, pulizia del blocco alla partenza/chiusura; migration).
- **Web**: sì (pannello lavoro, dialogo «Sposta…», icona e legenda, anteprima, filtro, segnalazioni).
- **Nx workspace**: no (rigenerazione del client con `api-client:generate-api`).

## 4. Impact Assessment

- **File/moduli coinvolti**: `libs/api-core` (entità `WorkOrder`, migration), `libs/api-features/PlanningFeature` (motore, servizio, segnalazioni, timeline), `libs/api-features/ProductionOrderFeature` (modelli, azioni consentite, transizioni), `openapi/Api.json` e client generato, `apps/web/src/app/features/planning` e `features/work-orders`, `doc/glossary.md`, `doc/structure.md`, test unitari e di integrazione.
- **Breaking changes**: no (campi aggiuntivi e nuovi endpoint; il comportamento del motore cambia solo in presenza di lavori fissati).

## 5. Fuori scopo

- **Trascinamento** delle barre nella timeline (richiede libreria e calendario lato client): userà gli stessi endpoint di anteprima/applicazione, in una spec successiva.
- Blocco della sola macchina o del solo orario (qui il blocco è sempre «orario e macchina»).
- Storico dei cambi di piano e di chi li ha fatti (serve l'autenticazione, prevista per ultima).
- Spostamento automatico dei successori o ripianificazione a cascata dopo uno spostamento manuale.
- Persone, materiali e giacenze come vincoli.
- Ottimizzazione del piano (spec «Ottimizzazione del piano», OR-Tools).

## 6. Decisioni confermate

1. Il blocco vale sempre per **orario e macchina insieme** (confermato).
2. Alla **partenza** del lavoro il blocco viene tolto e valgono le date reali (confermato).
3. Lo spostamento manuale **mostra i conflitti ma non li blocca** (confermato).
