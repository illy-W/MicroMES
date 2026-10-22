# Spec: Pagina di dettaglio macchina

## 1. Context & Goals

Oggi le informazioni su una macchina sono sparse: l'anagrafica con un pannello di dettaglio (calendario effettivo e prossime chiusure) in «Macchine e postazioni», il carico nella timeline di Pianificazione, la coda dei lavori nella lista Ordini di lavoro, le attrezzature compatibili in «Attrezzature». Non esiste un posto dove chiedersi «cosa sta succedendo su SAL-1, cosa c'è in coda, com'è montata, quanto è carica, come ha lavorato».

Obiettivi, per una micro impresa:

1. Una **pagina per macchina** con indirizzo proprio (`/equipment/:id`), raggiungibile da tutti i punti in cui compare una macchina.
2. Panoramica operativa: stato adesso, lavoro in corso, **coda** dei prossimi lavori, stato di montaggio (attrezzature e valori di cambio), carico e segnalazioni.
3. Tutto ciò che definisce la macchina in un posto solo: fasi eseguibili, attrezzature compatibili, calendario effettivo.
4. Storico recente (lavori eseguiti, minuti, scarti) per capire come ha lavorato.
5. Base della futura **vista operatore** (la coda di una macchina è ciò che l'operatore vedrà).

È una **aggregazione in lettura** di dati esistenti: nessuna nuova entità né deviazione dall'ISA-95 (Equipment resta il modello a ruoli semplificato descritto nel glossario). Le modifiche restano nei dialoghi già esistenti.

### Ricerca

- **ISA-95**: la macchina è *Equipment* con *Equipment Capability* (disponibile/impegnata/non raggiungibile) separata dal piano; la pagina mostra queste viste già modellate (calendario = disponibilità, lavori pianificati = impegnata), senza nuovi oggetti. Nota: derivato dalle decisioni già prese su glossario e calendari; non ho riletto la norma per questa spec perché non introduce entità.
- **Prodotti** (conoscenza generale, **non verificata ora**): le pagine «centro di lavoro» di MES/ERP (es. Odoo MRP) raccolgono capacità, ordini in coda, indicatori di utilizzo e calendario; è lo schema qui adottato. L'OEE completo richiede i motivi di fermo, che non abbiamo: fuori scopo.
- **Indicatore usato**: utilizzo = minuti lavorati ÷ minuti disponibili nel periodo (turni meno pause e chiusure, già calcolati dal calendario). Corrisponde al «tempo di operazione pianificato» già usato nella spec dei calendari (ISO 22400, mappatura già documentata nel glossario).

## 2. Requirements

### REQ-1: Pagina e navigazione
- **User Story**: As a responsabile di produzione, I want aprire la pagina di una macchina da ovunque la veda so that ho tutte le informazioni in un posto solo.
- **Acceptance Criteria**:
  - [ ] Rotta `/equipment/:id` con intestazione (codice, nome, attiva/disattivata, capacità, efficienza, calendario seguito, macchina predefinita per le fasi) e azioni «Modifica» (dialogo esistente), «Disattiva/Riattiva» (azione esistente), «Apri nella timeline».
  - [ ] Il click su una riga dell'elenco «Macchine e postazioni» apre la pagina (il pannello laterale di dettaglio attuale viene sostituito dalla pagina); id inesistente → messaggio chiaro con ritorno all'elenco.
  - [ ] Link alla pagina dai punti in cui compare una macchina: etichetta della macchina nella timeline, pannello del lavoro, dettaglio attrezzatura (macchine compatibili), dettaglio ordine/lavori, lista Ordini di lavoro, segnalazioni.
  - [ ] Sezioni a schede con indirizzo (`?tab=panoramica|fasi|calendario|pianificazione|storico`), accessibili da tastiera, con scheda attiva nell'URL.
- **Technical Constraints**: Componenti helm, token semantici; client generato; carico lazy della pagina; nessuna modifica alle regole di business delle macchine.

### REQ-2: Panoramica operativa
- **User Story**: As a responsabile di produzione, I want sapere cosa sta succedendo su una macchina adesso e cosa verrà dopo so that decido come intervenire.
- **Acceptance Criteria**:
  - [ ] **Stato adesso**: libera / in lavorazione (lavori `Running` con inizio reale e durata trascorsa) / chiusa (fuori turno, pausa, chiusura con motivo) / disattivata.
  - [ ] **Coda**: prossimi lavori pianificati sulla macchina (fino a 20, ordinati per inizio) con codice lavoro, ordine, fase, inizio-fine nel fuso aziendale, attrezzaggio previsto, stato del piano (in ritardo/obsoleto/fissato) e link al lavoro nella timeline; messaggio se la coda è vuota.
  - [ ] **Stato di montaggio**: attrezzature e valori di cambio dell'ultimo lavoro eseguito (o in corso) sulla macchina, come usati dal calcolo dell'attrezzaggio; «sconosciuto» se non ci sono lavori precedenti.
  - [ ] **Carico**: percentuale di occupazione per oggi, 7 e 30 giorni (stessa definizione già usata dalla timeline), con evidenza se ≥ 90%.
  - [ ] **Segnalazioni** che riguardano la macchina (sovraccarico, disattivata con lavori, attrezzature non compatibili…), con causa e suggerimento.
- **Technical Constraints**: Endpoint di sola lettura che riusa i calcolatori esistenti (carico, segnalazioni, stato di partenza della macchina, calendario effettivo); nessun dato duplicato né salvato.

### REQ-3: Fasi e attrezzature
- **User Story**: As a responsabile di produzione, I want vedere cosa può fare una macchina e con quali attrezzature so that scelgo bene dove assegnare i lavori.
- **Acceptance Criteria**:
  - [ ] Elenco delle **fasi eseguibili** con indicazione della predefinita, stato attivo, e link alla fase; gestione del collegamento nei dialoghi esistenti.
  - [ ] Elenco delle **attrezzature compatibili** (quelle con collegamento esplicito alla macchina, e quelle «compatibili con tutte» in una sezione distinta) con quantità, tempo di montaggio e stato; link all'attrezzatura.
  - [ ] Indicazione di quali attrezzature sono **montate adesso** (da REQ-2).
- **Technical Constraints**: Dati già disponibili nel modello della macchina e in `ToolingFeature`; eventuali campi mancanti si aggiungono al modello di dettaglio.

### REQ-4: Calendario effettivo
- **User Story**: As a responsabile di produzione, I want vedere quando la macchina lavora so that capisco perché un lavoro è pianificato in un certo momento.
- **Acceptance Criteria**:
  - [ ] Calendario seguito (proprio o aziendale predefinito) con turni settimanali, eccezioni proprie e ereditate (chiusure aziendali, ferie) e prossime chiusure, riusando la visualizzazione di `features/calendars` (settimana e mese).
  - [ ] Pulsante per aprire il calendario seguito e per modificare le eccezioni della macchina (dialoghi esistenti).
  - [ ] Il calendario è visibile in sola lettura nella pagina; nessuna nuova logica di calendario.
- **Technical Constraints**: Riuso dei componenti esistenti (`calendar-view`, `calendar-exceptions`) o loro estrazione in componenti condivisi, senza duplicare il codice.

### REQ-5: Pianificazione della macchina
- **User Story**: As a responsabile di produzione, I want vedere il piano di una sola macchina so that valuto la sua coda senza il rumore delle altre.
- **Acceptance Criteria**:
  - [ ] Timeline compatta della sola macchina per i prossimi 14 giorni con le stesse barre (stato, attrezzaggio, fissato) della pagina Pianificazione e apertura del pannello del lavoro; pulsante «Apri nella pianificazione completa» con la macchina già filtrata.
  - [ ] Carico per giorno (barra) con evidenza dei giorni ≥ 90%.
- **Technical Constraints**: Riuso del componente timeline con il filtro macchina (nessun codice duplicato); stesso endpoint della timeline con `equipmentId`.

### REQ-6: Storico recente
- **User Story**: As a responsabile di produzione, I want sapere come ha lavorato la macchina so that trovo i problemi (scarti, lavori lunghi).
- **Acceptance Criteria**:
  - [ ] Periodo selezionabile (7, 30, 90 giorni; default 30) e riepilogo: lavori eseguiti, minuti lavorati, **utilizzo** (minuti lavorati ÷ minuti disponibili del periodo), quantità buone e scartate, **percentuale di scarto**.
  - [ ] Elenco delle sessioni di lavoro svolte sulla macchina (dal dato reale della sessione: macchina del consuntivo) con lavoro, ordine, inizio-fine, durata, buoni, scarti, in ordine inverso, con paginazione e link al lavoro.
  - [ ] Scarti per motivo nel periodo (se presenti).
  - [ ] Non è un OEE: i fermi non sono modellati (motivi di fermo fuori scopo).
- **Technical Constraints**: Endpoint di sola lettura con aggregazioni calcolate sul DB (nessun caricamento in memoria di tutto lo storico); minuti disponibili dal calendario effettivo.

## 3. Layers impacted

- **Backend**: sì (endpoint di panoramica e di storico in `EquipmentFeature`, riuso di `PlanningFeature`/`CalendarFeature`/`ProductionOrderFeature`; filtro `equipmentId` nella timeline se manca).
- **Web**: sì (nuova pagina a schede, riuso di timeline e calendari, link nei punti di ingresso).
- **Nx workspace**: no (rigenerazione del client con `api-client:generate-api`).

## 4. Impact Assessment

- **File/moduli coinvolti**: `libs/api-features/EquipmentFeature` (endpoint e modelli), `PlanningFeature` (carico, segnalazioni, stato di partenza, filtro), `apps/web/src/app/features/equipment` (nuova pagina, sostituzione del pannello), `features/planning` e `features/calendars` (componenti riusati o estratti), link in `features/{planning,work-orders,tooling,production-orders}`, `app.routes.ts`, `doc/structure.md`, test.
- **Breaking changes**: no per l'API. Cambia la navigazione: il pannello laterale di dettaglio dell'elenco macchine viene sostituito dalla pagina.

## 5. Fuori scopo

- OEE completo e motivi di fermo; manutenzione e storico degli interventi.
- Etichetta QR della macchina e scheda stampabile (spec «Scheda lavoro e QR stampabili»).
- Modifica del calendario o del piano direttamente nella pagina (si usano i dialoghi esistenti).
- Persone assegnate alla macchina (spec sulle risorse umane).
- Confronto tra macchine e grafici avanzati.

## 6. Decisioni confermate

1. Il pannello laterale dell'elenco macchine è sostituito dalla pagina (il click sulla riga la apre).
2. Lo storico usa la macchina del consuntivo (dove il lavoro è stato eseguito), non quella pianificata.
3. L'utilizzo si calcola sui minuti disponibili del calendario effettivo (turni meno pause e chiusure).
