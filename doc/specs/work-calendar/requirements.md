# Spec: Calendari di lavoro, capacità ed efficienza delle macchine

## 1. Context & Goals

Oggi le durate sono somme di minuti: `setup + tempo per unità × quantità`. Non esistono giorni lavorativi, turni, pause, ferie né fermi macchina, quindi il sistema non può dire **quando** un lavoro finirà né quante ore la macchina ha davvero a disposizione. Questa spec introduce:

1. **Calendari di lavoro**: turni settimanali con pause, eccezioni (ferie, chiusure, giornate straordinarie) a livello azienda e, quando serve, per singola macchina.
2. **Capacità ed efficienza** della macchina: quante lavorazioni in parallelo e quanto è veloce rispetto allo standard.
3. **Calcolo calendar-aware**: dati un istante di inizio e i minuti di lavoro, la data di fine reale (e viceversa); le durate dei lavori tengono conto dell'efficienza.
4. **Interfaccia di gestione** dei calendari (turni, pause, ferie) e una vista a calendario per consultarli.

**Non** è in questa spec la pianificazione dei lavori sulle macchine (date pianificate per lavoro, carico, conflitti, vista Gantt/timeline di macchina): è la spec successiva «Pianificazione», che poggia su questi dati.

### Ricerca (standard e prodotti)

| Fonte | Cosa dice | Cosa ne ricaviamo |
|---|---|---|
| **Odoo MRP** | Ogni work center ha *Working Hours* (ore settimanali di disponibilità), *Capacity* (operazioni simultanee), *Time Efficiency* (durata attesa = durata standard ÷ efficienza), tempi prima/dopo la produzione e *time off* per risorsa; esistono giorni festivi globali. | Calendario settimanale + eccezioni, capacità ed efficienza per macchina. |
| **ERPNext** | La *Workstation* ha *Working Hours* e una *Holiday List*; la capacità di produzione è un campo della postazione. | Elenco di festività/chiusure separato dai turni. |
| **ISA-95** (IEC 62264-1/2, *Equipment Capability*) | La disponibilità è un attributo **della singola risorsa** (o classe), descritto come periodi di tempo in cui è *available* (disponibile), *committed* (impegnata dal piano) o *unattainable* (non raggiungibile: fermo, manutenzione, chiusura). Lo standard non prescrive calendari settimanali né un calendario aziendale di default. (Da fonti secondarie sulle norme, che sono a pagamento.) | Il dato vive sulla macchina: calendario proprio o ereditato dal predefinito (comodità di implementazione, compatibile). Le eccezioni «chiuso» equivalgono a *unattainable*; i lavori pianificati (spec successiva) saranno *committed*; il resto è *available*. |
| **ISO 22400-2** (modello dei tempi per i KPI) | Il tempo operativo pianificato (*planned operation time*) meno i fermi pianificati (pause, manutenzione programmata) dà il tempo di produzione pianificato (*planned busy time*), base di disponibilità e OEE. | Turni = tempo operativo pianificato; pause e chiusure pianificate = fermi pianificati. Così i futuri KPI (OEE, disponibilità) useranno gli stessi calendari senza rifare il modello. |
| **Librerie UI** | `angular-calendar` (MIT, Angular 20+, viste mese/settimana/giorno; non ottimizzata per mobile, nessuna vista a risorse). DayPilot Lite (Apache-2.0) include uno Scheduler a timeline; FullCalendar e Schedule-X offrono le viste a risorse e il drag and drop solo nei piani a pagamento. | Vedi §6: per questa spec basta un editor a tabella e un selettore di date; la scelta per la vista a timeline si fa nella spec Pianificazione con uno spike. |

## 2. Requirements

### REQ-1: Calendario di lavoro (turni e pause)
- **User Story**: As a responsabile di produzione, I want definire i turni settimanali con le pause so that il sistema conosca le ore realmente lavorabili.
- **Acceptance Criteria**:
  - [ ] Entità `WorkCalendar`: `Code` (unico, maiuscolo), `Name`, `Description`, `IsActive`, `IsDefault` (esattamente un calendario predefinito dell'azienda, sempre attivo), fuso orario unico dell'azienda (impostazione di sistema, default `Europe/Rome`).
  - [ ] Un calendario contiene **fasce di lavoro** per giorno della settimana: `DayOfWeek`, `StartTime`, `EndTime` (ore locali), più fasce per giorno ammesse (es. 08:00-12:00 e 13:00-17:00: la pausa è lo spazio tra due fasce). Nessun giorno senza fasce = giorno non lavorativo.
  - [ ] Validazione: fine > inizio, fasce dello stesso giorno non sovrapposte, nessun calendario attivo senza almeno una fascia, orari su 24 ore; fasce che attraversano la mezzanotte non supportate in questa versione (si usano due fasce su due giorni); messaggi in italiano con l'azione da fare.
  - [ ] Pagina «Calendari»: lista, crea, duplica, modifica, attiva/disattiva, imposta come predefinito; editor settimanale a tabella (righe = giorni, fasce modificabili con aggiungi/rimuovi, scorciatoia «copia su altri giorni», totale ore settimanali visibile).
  - [ ] Seed: calendario «Standard 8-12 / 13-17, lun-ven» come predefinito e uno «Doppio turno» (06-14, 14-22, lun-ven) usato da una macchina di esempio.
- **Technical Constraints**: `WorkCalendar` + `WorkCalendarSlot`; orari come `TimeOnly`; fuso orario IANA; nuova feature `CalendarFeature`; nessun `DbContext` nel codice di prodotto; messaggi 409 specifici per i vincoli.

### REQ-2: Eccezioni (ferie, chiusure, straordinari)
- **User Story**: As a responsabile di produzione, I want segnare ferie, chiusure e giornate straordinarie so that il calendario rifletta la realtà.
- **Acceptance Criteria**:
  - [ ] Entità `CalendarException` collegata a un calendario: intervallo di date (`FromDate`, `ToDate`, inclusivo), `Kind` (`Closed` = non lavorativo, `Custom` = fasce diverse per quel giorno/intervallo, es. mezza giornata o straordinario) con le proprie fasce, `Reason` testuale (es. «Ferie estive», «Ponte 2 giugno»).
  - [ ] Le eccezioni **prevalgono** sulle fasce settimanali; se più eccezioni si sovrappongono, prevale la più specifica (macchina > calendario; a pari livello, la più recente) e la sovrapposizione viene segnalata in fase di inserimento.
  - [ ] Opzione «ricorrente ogni anno» per le festività fisse (giorno e mese), senza duplicare per anno.
  - [ ] Elenco delle eccezioni di un calendario con filtro per anno e vista a **calendario mensile** (giorni chiusi evidenziati, con testo oltre al colore) per leggere a colpo d'occhio le ferie; inserimento con selezione di intervallo date.
  - [ ] Importazione rapida delle festività nazionali italiane dell'anno (elenco fisso incluso, Pasqua e Pasquetta calcolate) con anteprima e conferma; il responsabile può deselezionare voci.
  - [ ] Seed: le festività italiane dell'anno corrente e successivo sul calendario Standard, più un periodo di ferie estive.
- **Technical Constraints**: Intervalli in date locali (nessun orario per `Closed`); il calcolo del fuso e dell'ora legale usa il fuso aziendale; test su cambio ora legale.

### REQ-3: Calendario, capacità ed efficienza della macchina
- **User Story**: As a responsabile di produzione, I want assegnare a ogni macchina un calendario, la capacità e l'efficienza so that le durate siano realistiche.
- **Acceptance Criteria**:
  - [ ] `Equipment` ottiene: `WorkCalendarId` (nullable: vuoto = calendario predefinito dell'azienda), `Capacity` (intero ≥ 1, default 1: lavorazioni simultanee), `EfficiencyPercent` (decimale > 0, default 100: 80 = più lenta dello standard, 120 = più veloce).
  - [ ] Eccezioni **specifiche della macchina** (`EquipmentException`: stessa forma di `CalendarException`, es. «Manutenzione 12-14 marzo», «Fermo guasto»): la macchina non lavora in quell'intervallo; si gestiscono dalla scheda macchina e compaiono sul calendario mensile della macchina.
  - [ ] La scheda macchina mostra il calendario effettivo (nome, ore settimanali, prossime chiusure) e il calendario mensile con le eccezioni dell'azienda e quelle della macchina distinte.
  - [ ] Cambiare calendario, capacità o efficienza non modifica i lavori già rilasciati (nessuna riscrittura dello storico).
  - [ ] Seed: valori di esempio (una macchina con efficienza 85%, una con capacità 2, una sul calendario «Doppio turno», un'eccezione di manutenzione).
- **Technical Constraints**: Colonne nuove con default che preservano il comportamento attuale; migration; validazioni 422 con messaggio; `Capacity` non produce ancora regole di conflitto (solo dato per la spec Pianificazione).

### REQ-4: Calcolo calendar-aware
- **User Story**: As a sistema, I want trasformare minuti di lavoro in date reali so that la durata di un lavoro sia vera.
- **Acceptance Criteria**:
  - [ ] Servizio di calcolo puro e testato: `AddWorkingMinutes(equipment, start, minutes)` → fine, `WorkingMinutesBetween(equipment, from, to)`, e `NextWorkingInstant(equipment, instant)`; considera fasce settimanali, pause, eccezioni azienda e macchina, fuso e ora legale.
  - [ ] I minuti di lavoro di un lavoro su una macchina si dividono per l'efficienza (`minutiEffettivi = minutiStandard ÷ (EfficiencyPercent/100)`); senza macchina assegnata si usa l'efficienza 100 e il calendario predefinito.
  - [ ] Se in un orizzonte ragionevole (es. 2 anni) non esistono minuti lavorabili, il calcolo restituisce un errore esplicito (nessun ciclo infinito) e l'interfaccia dice «il calendario non ha ore lavorabili».
  - [ ] Endpoint di sola lettura `GET /api/calendars/{id}/preview?start=...&minutes=...` (e variante per macchina) per l'anteprima nell'editor («100 minuti da lunedì 8:00 finiscono martedì 9:40»).
  - [ ] Nel dettaglio dell'ordine di produzione e nella panoramica compare una **fine prevista indicativa** (data/ora) calcolata sul percorso più lungo con i calendari delle macchine (o il predefinito), etichettata «stima» e senza vincoli di capacità (la pianificazione vera è la spec successiva).
  - [ ] Test su: pause, giorno chiuso, ferie lunghe, eccezione `Custom`, ora legale, efficienza ≠ 100, minuti che attraversano più giorni.
- **Technical Constraints**: Logica di dominio pura (nessun accesso a database nei calcoli) in `libs/api-features/CalendarFeature`; il fuso aziendale proviene da una impostazione; la stima non cambia lo stato né i dati degli ordini (non persistita).

### REQ-5: Vista a calendario e navigazione
- **User Story**: As a responsabile di produzione, I want consultare turni e chiusure in una vista a calendario so that vedo subito quando le macchine lavorano.
- **Acceptance Criteria**:
  - [ ] Nella pagina «Calendari»: vista **mese** con giorni lavorativi/chiusi/straordinari distinti (colore e testo/icona), clic su un giorno per vedere le fasce e le eccezioni, e vista **settimana** di sola lettura con le fasce.
  - [ ] Selettore dell'ambito (calendario azienda o macchina) per vedere il calendario effettivo di una macchina con le sue eccezioni.
  - [ ] Voce di menu «Calendari» nel gruppo **Anagrafiche** (dopo «Macchine e postazioni»).
  - [ ] Accessibilità: navigazione da tastiera tra i giorni, nomi accessibili («lunedì 2 giugno, chiuso: Festa della Repubblica»), nessuna informazione solo a colore.
  - [ ] La libreria o i componenti scelti sono documentati nella spec dopo uno spike (§6); nessuna dipendenza viene aggiunta senza versione esatta e verifica del bundle (budget iniziale già al limite: la libreria va caricata a richiesta).
- **Technical Constraints**: Helm/spartan come base (calendario e selezione di intervallo se disponibili; se manca, generare con la CLI); stile con token semantici; eventuale libreria solo se porta valore reale rispetto a una griglia semplice; nessun `dark:` né colori fissi.

### REQ-6: Dati e compatibilità
- **User Story**: As a utente, I want che il passaggio ai calendari non rompa ciò che esiste.
- **Acceptance Criteria**:
  - [ ] Tutto ciò che esiste continua a funzionare: senza calendari, il calendario predefinito «Standard» del seed e `Capacity=1`, `Efficiency=100` per le macchine esistenti.
  - [ ] Una migration crea il calendario predefinito se non esiste (anche per database esistenti non seedati) con orari 08-12 / 13-17 lun-ven, così il sistema non resta mai senza predefinito.
  - [ ] Gli ordini e i lavori esistenti non cambiano; i minuti pianificati restano quelli standard (l'efficienza si applica solo al calcolo della durata reale/stima, non riscrive i minuti salvati).
  - [ ] Documentazione aggiornata: `doc/glossary.md`, `doc/structure.md`.
- **Technical Constraints**: Nessun breaking change nell'API (campi nuovi opzionali con default).

## 3. Layers impacted

- **Backend**: nuova `CalendarFeature` (calendari, fasce, eccezioni, calcolo), estensione di `EquipmentFeature` (calendario, capacità, efficienza, eccezioni macchina), stima nella feature degli ordini, impostazione del fuso, migration, seed, test, contratto OpenAPI.
- **Web**: pagina «Calendari» (editor settimanale, eccezioni, vista mese/settimana, import festività), scheda macchina estesa, stima della fine nel dettaglio ordine e nella panoramica.
- **Nx workspace**: possibile nuova dipendenza di UI (da decidere nello spike, §6).

## 4. Impact Assessment

- Affected: `libs/api-core` (entità, migration, vincoli), nuova `libs/api-features/CalendarFeature`, `EquipmentFeature`, `ProductionOrderFeature` (stima), seeder, test, `apps/web/src/app/features/calendars` e `equipment`, `production-orders`, sidebar, `doc/*`.
- Breaking changes: **no**.
- Deviazioni da ISA-95 / dai prodotti noti (da annotare nel glossario): un solo fuso aziendale; niente turni con assegnazione di personale; capacità come numero intero di lavorazioni simultanee, non per prodotto (Odoo ha capacità per prodotto); fasce che non attraversano la mezzanotte; efficienza unica per macchina, non per prodotto o fase.

## 5. Fuori scopo

Date pianificate per lavoro, carico macchina, conflitti e capacità finita, pianificazione automatica e drag and drop (spec «Pianificazione»); calendari delle persone e turni di personale; straordinari per operatore; tempi di preparazione dipendenti dalla sequenza; ricorrenze complesse (es. «ogni primo lunedì»); fusi orari multipli; sincronizzazione con calendari esterni (iCal/Google).

## 6. Decisioni e punti aperti (punti 1, 3 e 5 confermati dall'utente)

1. **(confermato)** **Scomposizione in due spec.** Questa (calendari e capacità + calcolo) e la successiva «Pianificazione» (date per lavoro, carico, timeline di macchina). Motivo: la seconda dipende dalla prima e porta le scelte di UI più rischiose.
2. **Librerie.** Per questa spec basta un editor a tabella e un calendario mensile in sola lettura: si parte con i componenti helm/spartan (calendario/selezione intervallo) e si valuta `angular-calendar` solo se la vista mese/settimana lo richiede davvero (MIT, Angular 20+; non ottimizzata per mobile, nessuna vista a risorse). Per la **timeline per macchina** della spec Pianificazione le strade sono: griglia propria sola lettura (nessuna dipendenza, accessibile, token del tema), DayPilot Lite Scheduler (Apache-2.0, da verificare drag and drop e accessibilità in uno spike), oppure componenti a pagamento (FullCalendar Scheduler, Schedule-X Premium, Bryntum). Proposta: spike dedicato all'inizio della spec Pianificazione.
3. **Un calendario predefinito azienda più override per macchina**, non un calendario per ogni macchina obbligatorio. Confermato: ISA-95 (disponibilità per risorsa, periodi available/committed/unattainable), Odoo (calendario del work center con default aziendale) ed ERPNext (orari e festività per postazione) concordano che il dato sta sulla risorsa; il default aziendale è una comodità, non una deviazione.
4. **Capacità** come dato (intero) ora, regola di conflitto nella Pianificazione.
5. **(confermato)** **Stima della fine ordine** indicativa e non persistita fino alla Pianificazione.
6. **Festività italiane** precaricabili con conferma; nessun calcolo implicito di ponti.

7. **Risorse separate, disponibilità condivisa.** Macchine e (in futuro) persone restano entità distinte, come in ISA-95 (Equipment e Personnel sono modelli separati, con requisiti separati nel lavoro); si condivide solo il meccanismo di calendario ed eccezioni tramite un'interfaccia di disponibilità. Nessuna classe base «Risorsa» nel database. Eccezioni in tabelle per tipo con chiavi esterne; un'eventuale base astratta (EF Core, una tabella per tipo concreto) è un refactoring futuro.
