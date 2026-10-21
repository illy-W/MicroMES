# Spec: Pianificazione dei lavori sulle macchine

## 1. Context & Goals

Oggi il sistema conosce *cosa* fare (ordini, lavori, dipendenze), *dove* si può fare (macchine abilitate per fase) e *quando* una macchina lavora (calendari, capacità, efficienza), ma non *quando* ogni lavoro verrà eseguito e su quale macchina. La stima di fine ordine ignora i conflitti: due lavori possono finire sulla stessa macchina nello stesso momento.

Obiettivi, per una micro impresa:

1. Un **piano** dei lavori aperti: macchina, inizio e fine pianificati, che rispetti dipendenze, calendari e capacità delle macchine (**capacità finita**, in avanti).
2. Un piano **spiegabile e controllabile**: anteprima prima di applicare, nessuna modifica silenziosa, motivi chiari per ritardi e conflitti.
3. Una **vista a timeline per macchina** (macchine in riga, tempo in orizzontale) per capire carico, code e ritardi a colpo d'occhio.
4. Un ponte verso la vista operatore: l'ordine dei lavori su una macchina è la sua coda di lavoro.

Fuori scopo: vedi §5 (materiali e giacenze, persone, tempi di cambio attrezzaggio dipendenti dalla sequenza, ottimizzazione avanzata, trascinamento).

### Ricerca (standard e prodotti)

| Fonte | Cosa dice | Cosa ne ricaviamo |
|---|---|---|
| **ISA-95** (richiesta/ordine di lavoro) | Il lavoro ha inizio e fine pianificati e una priorità; la disponibilità delle risorse (*Equipment Capability*: available/committed/unattainable) è un dato separato dal piano. (Da fonti secondarie e dalla mia conoscenza della norma: da non considerare verificato punto per punto.) | Il piano è l'inizio/fine (e la macchina) del lavoro; i lavori pianificati rendono la macchina *committed*; calendari ed eccezioni restano *available/unattainable*. |
| **Odoo MRP** | Il pulsante *Plan* assegna ogni lavoro al primo slot libero del suo work center, **in avanti** a partire dalla data pianificata dell'ordine o da adesso; non riadatta i lavori alla scadenza, mostra le conseguenze (può finire in ritardo). Viste Gantt per ordine e per work center, cambio di piano e ripianificazione. | Pianificazione in avanti esplicita e pulsante «Pianifica»; ritardo rispetto alla scadenza mostrato, non nascosto. |
| **Letteratura sulla pianificazione a capacità finita** | MRP lavora a capacità infinita e produce piani non eseguibili; la capacità finita inserisce un'operazione solo se la risorsa è libera, rende visibili i sovraccarichi e le alternative; regole di priorità semplici (scadenza più vicina, priorità) vanno bene per officine; la granularità deve essere minore della durata media dei lavori (15-60 minuti), non giornaliera; il dettaglio serve nel breve periodo, non nel lungo. | Capacità finita sulle macchine, regole di priorità semplici e dichiarate, risoluzione in minuti, orizzonte limitato, sovraccarichi e ritardi evidenziati con la risorsa responsabile. |
| **Librerie UI** | DayPilot Lite (Apache-2.0) include uno Scheduler a timeline per Angular, ma l'accessibilità è un punto debole; FullCalendar Scheduler, Schedule-X Premium e Bryntum sono a pagamento per viste a risorse e drag and drop; `angular-calendar` non ha una vista a risorse. | La timeline in sola lettura si costruisce con una griglia propria (token del tema, accessibilità, nessuna dipendenza). Il trascinamento richiede di conoscere il calendario di lavoro (le fasce non lavorative): rimandato; si rivaluta una libreria con uno spike quando servirà. |

## 2. Requirements

### REQ-1: Piano persistito sui lavori
- **User Story**: As a responsabile di produzione, I want un piano salvato per ogni lavoro so that l'officina sappia cosa fare e quando.
- **Acceptance Criteria**:
  - [ ] `WorkOrder.PlannedStartAt` e `PlannedEndAt` (UTC, nullable), `PlannedAt` (quando è stato pianificato). La macchina del piano è `AssignedEquipmentId` (già esistente): la pianificazione la imposta se manca.
  - [ ] Lavori `Waiting`/`Ready`/`Interrupted`: pianificabili. `Running`: il piano non si sposta (inizio reale, fine = inizio reale + minuti rimanenti sul calendario); `Ended`/`Aborted`: date reali, mai ripianificate.
  - [ ] Stato derivato del piano (non salvato): `NonPianificato`, `Pianificato`, `InRitardo` (fine pianificata oltre la fine richiesta dell'ordine), `Obsoleto` (il piano non è più coerente: un predecessore finisce dopo l'inizio pianificato, una macchina è stata disattivata o resa non abilitata, un calendario è cambiato dopo `PlannedAt`).
  - [ ] I modelli di lavoro e di ordine espongono inizio/fine pianificati e stato del piano; per l'ordine, inizio = minimo e fine = massimo dei suoi lavori (e dei figli).
  - [ ] Nessuna ripianificazione automatica: eventi come ritardi o cambi di calendario rendono il piano `Obsoleto` e lo segnalano, ma lo cambia solo l'utente (REQ-3).
- **Technical Constraints**: Colonne nullable + migration; le date non entrano nelle macchine a stati; repository vuoti, nessun `DbContext`; la stima di fine ordine esistente resta come ripiego per ordini non pianificati e viene sostituita dalla fine pianificata quando c'è un piano.

### REQ-2: Motore di pianificazione (in avanti, capacità finita)
- **User Story**: As a responsabile di produzione, I want un piano che rispetti dipendenze, calendari e capacità so that sia eseguibile.
- **Acceptance Criteria**:
  - [ ] **Ordine di elaborazione** dichiarato e deterministico: priorità decrescente, poi fine richiesta crescente (scadenza più vicina), poi inizio richiesto, poi codice; dentro l'ordine, i lavori in ordine topologico.
  - [ ] **Inizio più presto** di un lavoro = massimo tra: adesso, inizio richiesto dell'ordine, fine del piano di tutti i predecessori (anche tra ordini figlio e padre), prossimo istante lavorabile della macchina.
  - [ ] **Scelta della macchina**: se il lavoro ha già una macchina assegnata valida, la mantiene; altrimenti tra le macchine attive abilitate per la fase sceglie quella che termina prima il lavoro (a parità: la predefinita della fase, poi il codice); se la fase non ha macchine, il lavoro è pianificato sul calendario predefinito senza vincoli di capacità (lavoro manuale/senza risorsa).
  - [ ] **Capacità**: una macchina con capacità N non ospita più di N lavori sovrapposti. Un lavoro occupa un intervallo continuo (può includere pause e chiusure, in cui non si lavora); non viene spezzato né interrotto da altri (non preemptive). Il motore cerca il primo inizio ≥ inizio più presto tale che l'intervallo non superi la capacità.
  - [ ] **Durata**: minuti standard del lavoro (setup + tempo per unità × quantità, al netto di quanto già lavorato per i lavori interrotti) ÷ efficienza della macchina, consumati con il calcolo calendar-aware già esistente (turni, pause, eccezioni).
  - [ ] I lavori `Running` e quelli già pianificati e bloccati occupano la macchina come intervalli fissi; il piano dei lavori `Ended`/`Aborted` non occupa nulla di futuro.
  - [ ] **Orizzonte**: fino a 6 mesi da adesso; i lavori che non entrano restano non pianificati con un motivo («nessuna ora lavorabile entro l'orizzonte»).
  - [ ] **Risoluzione** al minuto (nessun arrotondamento a giorni).
  - [ ] Il motore è una **funzione pura** (stato in ingresso → piano in uscita, con motivi), testabile senza database, deterministica (stesso input, stesso piano) e con tempi accettabili per centinaia di lavori.
  - [ ] Il motore sta dietro un'interfaccia (`IPlanningEngine`: modello di ingresso e di uscita indipendenti dall'implementazione, con motivi per ogni attesa) e ha un banco di prova (test di confronto su scenari fissi: ritardo totale, fine ultimo lavoro, utilizzo) così che un motore di ottimizzazione (es. OR-Tools CP-SAT) possa essere aggiunto in seguito e confrontato con l'euristica senza toccare il resto.
  - [ ] Materiali e giacenze **non** sono considerati (non esiste magazzino); persone non considerate.
- **Technical Constraints**: Nuova feature `PlanningFeature` in `libs/api-features`; riusa `WorkingTimeCalculator`/`AvailabilityResolver` e le read model di `ProductionData`; nessuna dipendenza esterna.

### REQ-3: Anteprima, applicazione e ripianificazione
- **User Story**: As a responsabile di produzione, I want vedere cosa cambierebbe prima di applicare il piano so that non subisco sorprese.
- **Acceptance Criteria**:
  - [ ] `POST /api/planning/preview` con ambito `Order` (un ordine e i suoi figli), `Unplanned` (solo lavori non pianificati) o `All` (tutti i lavori aperti, ripianificazione completa): restituisce il piano proposto senza salvarlo, con per ogni lavoro: macchina, inizio, fine, inizio/fine attuali e differenza, motivo dell'eventuale attesa («aspetta SAL-1 libera dal …», «aspetta il lavoro X»), e riepilogo (lavori pianificati, in ritardo, non pianificabili).
  - [ ] `POST /api/planning/apply` applica lo stesso ambito in transazione (`IUnitOfWork`), imposta macchina assegnata dove mancava, date e `PlannedAt`; risponde con il riepilogo. Se tra anteprima e applicazione i dati sono cambiati, il risultato può differire: la risposta riporta le differenze rispetto all'anteprima (nessun errore bloccante).
  - [ ] «Pianifica» sul dettaglio dell'ordine (ambito `Order`) e sulla pagina Pianificazione («Pianifica non pianificati», «Ripianifica tutto»); la ripianificazione non tocca lavori `Running`, `Ended`, `Aborted`.
  - [ ] Possibilità di **rimuovere il piano** di un ordine (torna a non pianificato) con conferma.
  - [ ] La pianificazione di un ordine `Planned`/`Cancelled`/`Completed`/`Aborted` non è consentita (409 con spiegazione).
  - [ ] Messaggi 409/422 specifici; nessuna modifica parziale in caso di errore.
- **Technical Constraints**: Un solo servizio di applicazione transazionale; l'anteprima usa lo stesso motore dell'applicazione (nessun codice duplicato).

### REQ-4: Segnalazioni (ritardi, conflitti, rischi)
- **User Story**: As a responsabile di produzione, I want sapere cosa non va nel piano so that posso intervenire prima di consegnare in ritardo.
- **Acceptance Criteria**:
  - [ ] Elenco delle segnalazioni del piano con tipo, ordine/lavoro/macchina coinvolti e motivo: **ritardo** (fine pianificata oltre la fine richiesta, con i giorni di ritardo), **lavoro non pianificabile** (orizzonte, calendario senza ore, nessuna macchina attiva abilitata per una fase che ha macchine), **piano obsoleto**, **macchina sovraccarica** (periodi in cui il carico supera una soglia, es. 100% della capacità nella settimana), **macchina disattivata o non più abilitata** con lavori assegnati.
  - [ ] Ogni segnalazione indica la causa nominata (macchina o lavoro che trattiene) e un suggerimento d'azione («assegna un'altra macchina», «anticipa l'inizio richiesto»).
  - [ ] Le segnalazioni compaiono nella pagina Pianificazione, nel dettaglio ordine (banner sintetico) e come badge sui lavori nella lista.
  - [ ] Nessuna notifica esterna (email/push): solo interfaccia.
- **Technical Constraints**: Segnalazioni calcolate dal backend (endpoint di sola lettura) con la stessa funzione del motore; nessun dato duplicato salvato.

### REQ-5: Timeline per macchina
- **User Story**: As a responsabile di produzione, I want una vista con le macchine in riga e il tempo in orizzontale so that vedo code, carico e ritardi.
- **Acceptance Criteria**:
  - [ ] Pagina «Pianificazione» (menu, gruppo **Produzione**): timeline con una riga per macchina attiva (e una riga «Senza macchina» per i lavori su fasi senza macchine), asse del tempo con zoom Giorno (ore) / Settimana (giorni) / Mese, navigazione e «oggi» evidenziato.
  - [ ] Barre dei lavori con inizio/fine pianificati, codice lavoro e fase; colore e icona per stato (in attesa, pronto, in esecuzione, in pausa, terminato) e per ordine (colore stabile per ordine con testo), indicatore di ritardo; lavori sovrapposti su capacità >1 sulla stessa riga su più corsie.
  - [ ] Fasce non lavorative ombreggiate (dal calendario effettivo della macchina) e chiusure/eccezioni con tooltip del motivo.
  - [ ] Carico per periodo (percentuale di occupazione sul tempo lavorabile) sotto o accanto a ogni riga.
  - [ ] Clic/Invio su una barra apre un pannello: dettagli del lavoro, predecessori, stato del piano e segnalazioni, azioni «Assegna macchina» (dialogo già esistente), link all'ordine e agli ordini di lavoro; filtri per ordine, stato, macchina; ricerca per codice.
  - [ ] Sola lettura (nessun trascinamento in questa spec); navigazione da tastiera tra barre, nomi accessibili («Saldatura OP-2026-0003/20 su SAL-1, dal 22/10 08:00 al 22/10 11:30, in ritardo»), nessuna informazione solo a colore.
  - [ ] Prestazioni: virtualizzazione o rendering limitato alla finestra visibile con centinaia di lavori; caricamento a richiesta (chunk lazy), senza far crescere il bundle iniziale.
  - [ ] Endpoint di sola lettura `GET /api/planning/timeline?from&to&equipmentId?` che restituisce macchine, lavori con piano, fasce non lavorative e carico per la finestra richiesta (max 62 giorni).
- **Technical Constraints**: Griglia propria con helm e token semantici (nessuna libreria); orari mostrati nel fuso aziendale; nessun `dark:` né colori fissi.

### REQ-6: Integrazione con le pagine esistenti
- **User Story**: As a utente, I want vedere il piano dove già lavoro.
- **Acceptance Criteria**:
  - [ ] Ordini di lavoro: colonne «Inizio previsto» e «Fine prevista», ordinamento per inizio pianificato (coda della macchina), filtro «da pianificare»/«in ritardo», badge di segnalazione.
  - [ ] Dettaglio ordine: riquadro «Piano» con inizio e fine pianificati, stato, pulsanti «Pianifica»/«Rimuovi piano»; la «Fine prevista (stima)» diventa «Fine pianificata» quando c'è un piano.
  - [ ] Panoramica: il pannello del lavoro mostra inizio e fine pianificati; il riepilogo la fine pianificata.
  - [ ] Dashboard o lista ordini: indicatore «in ritardo» sul piano (solo se la pagina esiste già come punto naturale: altrimenti segnalato come follow-up).
- **Technical Constraints**: Nessun cambiamento di comportamento per ordini non pianificati.

### REQ-7: Dati di esempio
- **User Story**: As a utente in prova, I want un piano di esempio realistico.
- **Acceptance Criteria**:
  - [ ] Il seed produce un piano per gli ordini aperti con almeno: una macchina con coda di più lavori, un conflitto risolto con la scelta di una macchina alternativa (SAL-1/SAL-2, TAG-TUB-1/2), un ordine in ritardo rispetto alla fine richiesta, un lavoro non pianificabile o su fase senza macchina, un piano obsoleto.
  - [ ] Ordini, stati e conteggi esistenti invariati (le date pianificate si aggiungono ai dati esistenti dei lavori aperti, calcolate con il motore all'avvio con `TimeProvider`).
  - [ ] Test di integrazione su DB nuovo e idempotenza.
- **Technical Constraints**: Seeder solo su tabelle vuote o su lavori senza piano; indicare cosa svuotare per un DB esistente.

## 3. Layers impacted

- **Backend**: nuova `PlanningFeature` (motore puro, preview/apply/rimozione, segnalazioni, timeline), colonne nuove su `WorkOrder`, modelli e filtri di `ProductionOrderFeature`, seed, test, contratto OpenAPI.
- **Web**: pagina «Pianificazione» (timeline, segnalazioni, azioni), integrazioni in Ordini di lavoro, dettaglio ordine e panoramica.
- **Nx workspace**: nessun impatto (nessuna libreria nuova).

## 4. Impact Assessment

- Affected: `libs/api-core` (colonne, migration), nuova `libs/api-features/PlanningFeature`, `ProductionOrderFeature` (modelli, read model, lista lavori, stima), `CalendarFeature` (riuso), seeder, `libs/api-tests*`, `apps/web/src/app/features/planning` (nuova), `work-orders`, `production-orders`, sidebar, `doc/*`.
- Breaking changes: **no** (campi nuovi opzionali). Nuovo comportamento solo se si usa la pianificazione.
- Deviazioni da ISA-95 / dai prodotti noti (da annotare nel glossario): il piano sta sul lavoro (inizio/fine/macchina) senza entità `Operations Schedule` né `Equipment Capability` (*committed*) separate; capacità finita in avanti con regole di priorità semplici (nessuna ottimizzazione, nessun backward scheduling); lavori non interrompibili; nessun tempo di cambio attrezzaggio dipendente dalla sequenza; materiali e persone non vincolano.

## 5. Fuori scopo

Trascinamento e modifica manuale delle date (rinviato: richiede di conoscere il calendario di lavoro; si rivaluta una libreria con uno spike), blocco manuale dei lavori, pianificazione all'indietro dalla scadenza e «data di inizio consigliata», vincoli di materiale e giacenze, persone e competenze, tempi di attrezzaggio dipendenti dalla sequenza, ottimizzazione (minimo ritardo, bilanciamento), scenari «what if» salvati, storico dei piani, notifiche esterne, pianificazione automatica al rilascio.

## 6. Decisioni (proposte del lead da confermare)

1. **Pianificazione esplicita**, non automatica al rilascio né alla modifica: l'utente preme «Pianifica» dopo aver visto l'anteprima; il piano obsoleto si segnala, non si corregge da solo.
2. **Capacità finita in avanti** con ordine di priorità, scadenza, inizio richiesto, codice. Nessun backward scheduling in questa versione.
3. **Lavori non interrompibili**: un lavoro occupa un intervallo continuo sul calendario della macchina (pause e chiusure incluse); semplice da spiegare e da visualizzare.
4. **Scelta automatica della macchina** tra quelle abilitate (la prima a finire), mantenendo quella già assegnata; la pianificazione scrive l'assegnazione dove mancava.
5. **Timeline in sola lettura** con griglia propria; trascinamento e ritocchi manuali in una spec successiva.
6. **Anteprima prima di applicare**, con differenze rispetto al piano attuale e motivi dell'attesa.
7. **Euristica prima, ottimizzatore dopo.** Il motore iniziale è l'euristica deterministica descritta in REQ-2 (spiegabile, senza dipendenze native, testabile). Un motore di ottimizzazione (Google OR-Tools CP-SAT, che ha un'API .NET) è una scelta rimandata ma resa possibile: motore dietro interfaccia e banco di confronto (REQ-2). Si valuta in una spec successiva («Ottimizzazione del piano»), con l'euristica come soluzione di partenza e di ripiego.
