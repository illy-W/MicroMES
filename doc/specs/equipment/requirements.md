# Spec: Anagrafica macchine e postazioni

## 1. Context & Goals

Il sistema non sa dove si lavora: il ciclo dice *cosa* si fa (fasi di processo) ma non *su quale macchina o postazione*. Senza questa anagrafica non si possono assegnare i lavori, caricare le macchine, calcolare i tempi reali per macchina né gestire i fermi.

Obiettivo di questa spec: **solo l'anagrafica** delle risorse fisiche e il collegamento con le fasi di processo («dove si può fare»). Assegnazione ai lavori, calendario, capacità, fermi e persone sono specifiche successive.

### Ricerca (standard e prodotti)

| Fonte | Cosa dice | Cosa ne ricaviamo |
|---|---|---|
| **ISA-95 / OPC UA for ISA-95** | Le risorse fisiche sono *Equipment*, organizzate in livelli: *work center* (gruppo pianificato dai livelli 3-4) e *work unit* (sotto il work center, pianificato dal livello 3). Le *Equipment Class* classificano le attrezzature. Il segmento di processo indica le attrezzature necessarie (per classe o per singola attrezzatura). | Usiamo `Equipment` come entità; il collegamento sta sul segmento di processo, come nello standard. Livelli e classi non servono a una micro impresa: deviazione documentata. |
| **Odoo MRP** | Il *work center* ha codice, nome, orari, capacità, efficienza, OEE target e *alternative work centers*; ogni operazione del ciclo indica il work center. | Codice e nome; capacità, efficienza e orari quando arriverà la pianificazione. Le alternative nascono dal fatto che più macchine sono collegate alla stessa fase. |
| **ERPNext** | La *workstation* ha un *workstation type* assegnato all'operazione; al lavoro viene proposta una postazione di quel tipo. | Il legame si esprime con una categoria condivisa (qui la fase), non macchina per macchina nel ciclo. |

Decisioni di prodotto già prese con l'utente: un solo concetto «Macchine e postazioni» senza classi; collegamento a livello di **fase di processo** (proposta del lead, da confermare); voce di menu in «Anagrafiche»; stato «fuori servizio» rimandato (solo attivo/inattivo).

## 2. Requirements

### REQ-1: Anagrafica macchine e postazioni
- **User Story**: As a responsabile di produzione, I want elencare macchine e postazioni di lavoro so that il sistema sappia dove si produce.
- **Acceptance Criteria**:
  - [ ] Entità `Equipment`: `Code` (unico, maiuscolo, normalizzato), `Name`, `Description` opzionale, `IsActive` (soft-delete, mai cancellazione fisica).
  - [ ] Pagina «Macchine e postazioni» nel menu, gruppo **Anagrafiche**: lista con ricerca, filtro attive/disattivate, crea, modifica, attiva/disattiva con conferma; stesso schema di «Fasi di processo» e «Motivi di scarto».
  - [ ] La lista mostra le fasi collegate (codici) e lo stato.
  - [ ] Codice duplicato → 409 con messaggio specifico («Esiste già una macchina o postazione con questo codice»); nessun «riprova».
  - [ ] Una macchina disattivata non viene più proposta nelle scelte, ma resta leggibile nello storico (quando esisterà).
- **Technical Constraints**: Pattern delle feature esistenti (controller → servizio con Result → repository, mapping manuale, enum a stringa); nuova feature `EquipmentFeature` in `libs/api-features`; entità e migration in `libs/api-core`; nessun `DbContext` nel codice di prodotto; interfacce repository vuote.

### REQ-2: Dove si può eseguire una fase
- **User Story**: As a responsabile di produzione, I want indicare su quali macchine si può eseguire ogni fase so that la regola valga per tutti i cicli che la usano.
- **Acceptance Criteria**:
  - [ ] Relazione molti-a-molti `ProcessSegment` ↔ `Equipment` («macchine abilitate»); nessuna macchina collegata = nessun vincolo (la fase si può fare ovunque, ad esempio lavoro manuale).
  - [ ] Si gestisce da entrambi i lati: nella scheda della macchina («Fasi che esegue») e nella scheda della fase («Macchine abilitate»), con ricerca e hint «nessuna = nessun vincolo».
  - [ ] Si possono collegare solo macchine attive; se una macchina viene disattivata, i collegamenti restano ma non vengono proposti (nessuna perdita di dati silenziosa).
  - [ ] Il modello della fase include le macchine collegate (id, codice, nome, attiva); il modello della macchina include le fasi (id, codice, nome).
  - [ ] Fasi disattivate: stesso criterio.
  - [ ] Nessuna modifica a cicli, ordini, lavori e panoramica in questa spec.
- **Technical Constraints**: Tabella di giunzione con indice unico (fase, macchina), FK con cancellazione a cascata dalla giunzione; messaggi 409 specifici per i vincoli nuovi in `ConstraintViolation`.

### REQ-3: Dati di esempio
- **User Story**: As a sviluppatore/utente in prova, I want un esempio realistico su un database nuovo so that vedo subito il collegamento.
- **Acceptance Criteria**:
  - [ ] Il seed di sviluppo contiene circa 8 macchine e postazioni coerenti con le 11 fasi esistenti, ad esempio: due per il taglio tubolare, una per il taglio lamiera, due postazioni di saldatura, un forno/cabina di verniciatura a polvere, una cabina per vernice acquosa, un banco di assemblaggio, un tavolo di imballaggio.
  - [ ] Almeno una fase senza macchine collegate (per mostrare «nessun vincolo») e almeno una macchina collegata a più fasi.
  - [ ] Gli ordini e i dati esistenti non cambiano (codici, stati, conteggi).
  - [ ] Test di integrazione su DB nuovo con controllo dei collegamenti e idempotenza.
- **Technical Constraints**: Seeder in `libs/api-features/SampleDataFeature` (solo repository o `IUnitOfWork`, mai in ApiCore); il seeder lavora solo su tabelle vuote: indicare cosa svuotare per un DB esistente.

## 3. Layers impacted

- **Backend**: entità `Equipment` e giunzione, migration, `EquipmentFeature` (CRUD, soft-delete, collegamento alle fasi), estensione del modello e dei servizi di `ProcessSegmentFeature` per le macchine collegate, seed, test, contratto OpenAPI.
- **Web**: pagina «Macchine e postazioni», selezione fasi nella scheda macchina, selezione macchine nella scheda fase, voce di menu.
- **Nx workspace**: nessun impatto.

## 4. Impact Assessment

- Affected: `libs/api-core` (entità, migration, `ConstraintViolation`), `libs/api-features/ProcessSegmentFeature` (modello e servizio), nuova `EquipmentFeature`, `SampleDataFeature`, `libs/api-tests*`, `apps/web/src/app/features/process-segments`, nuova `features/equipment`, `core/layouts/full/sidebar-data.ts`, `app.routes.ts`, `doc/glossary.md`, `doc/structure.md`.
- Breaking changes: **no** (campi e relazioni nuovi, nessun comportamento esistente cambia).
- Deviazioni da ISA-95 (da annotare in `doc/glossary.md`): niente gerarchia (*work center*/*work unit*), niente *Equipment Class*, niente proprietà e capacità; il collegamento fase-macchina è diretto (non per classe) e non esiste ancora l'attributo di uso/quantità.

## 5. Fuori scopo

Assegnazione di una macchina a un lavoro e sua scelta sul tablet, capacità, efficienza, orari e calendari, carico macchina, fermi e motivi di fermo, manutenzione, stato «fuori servizio», persone e qualifiche, gerarchie e classi, vincolo per singolo passo del ciclo (eccezione alla regola di fase).

## 6. Decisioni

1. Un solo concetto «Macchine e postazioni», senza classi: confermato.
2. Il collegamento sta sulla **fase di processo** (non sul passo del ciclo); nessuna macchina collegata = nessun vincolo: proposta del lead, da confermare. L'eccezione per singolo passo può essere aggiunta dopo senza rifare il modello.
3. Voce di menu nel gruppo Anagrafiche: confermato.
4. Solo attivo/inattivo; «fuori servizio» rimandato: confermato.
