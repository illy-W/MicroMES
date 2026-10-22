# Spec: Attrezzature e attrezzaggi

## 1. Context & Goals

Oggi l'attrezzaggio è un numero fisso per fase (`SetupMinutes` del passo di ciclo), uguale qualunque cosa la macchina abbia prodotto prima. Nella realtà di un'officina il cambio dipende da *cosa c'era montato* e da *cosa serve adesso*: stesso stampo con un altro colore costa poco, uno stampo diverso costa molto. Inoltre stampi, seghe e accessori sono risorse **condivise tra macchine**: lo stesso stampo non può essere su due macchine nello stesso momento.

Obiettivi, per una micro impresa:

1. Anagrafica delle **attrezzature** (stampi, seghe, accessori), condivisibili tra macchine, con quantità disponibile e macchine compatibili.
2. Ogni passo di ciclo indica **quali attrezzature richiede** e il valore degli **attributi di cambio** rilevanti (es. colore, materiale).
3. Il **tempo di attrezzaggio** diventa dipendente dalla sequenza: dipende dal lavoro precedente sulla stessa macchina (stampo da montare 10 min, cambio colore 5 min; stesso stampo e solo colore diverso = 5 min).
4. Il motore di pianificazione rispetta il vincolo delle attrezzature condivise e calcola gli attrezzaggi in base alla sequenza reale; i conflitti si segnalano.
5. Predisporre il modello dati perché l'**ottimizzatore** (spec successiva, OR-Tools CP-SAT) possa riordinare i lavori per ridurre i cambi.

Fuori scopo: vedi §5.

### Ricerca (standard, prodotti, letteratura)

Cosa è **verificato** e cosa no. Nota: non ho trovato testimonianze di utenti (forum, recensioni) sui problemi di implementazione; i problemi critici qui sotto vengono da documentazione di prodotto, blog di fornitori e articoli accademici, quindi sono da leggere come pratica nota, non come lamentele rilevate.

| Fonte | Cosa dice | Cosa ne ricaviamo |
|---|---|---|
| **ISA-95 Parte 2/4** (indici letti dalle anteprime ANSI; OPC UA for ISA-95) | Esiste il modello *Physical Asset* (attrezzature, utensili, stampi), distinto da *Equipment*, con *Physical Asset Class*, *Physical Asset Requirement* nel job order, *Physical Asset Capability* e *Equipment Asset Mapping* (relazione macchina-asset). Una *Physical Asset Segment Specification* dice che cosa richiede una fase. | Verificato sugli indici e sulla descrizione OPC. Gli attributi dei singoli oggetti non sono consultabili senza la norma: non verificato. Mappatura scelta: attrezzatura = *Physical Asset*; requisito del passo = *physical asset segment specification*; requisito del lavoro = *physical asset requirement*; compatibilità attrezzatura-macchina = *equipment asset mapping*. Gli attributi di cambio non hanno un oggetto nella norma: estensione. |
| **Siemens Opcenter APS / Preactor** (documentazione e community) | Tempi di cambio dipendenti dalla sequenza calcolati sugli **attributi** dell'operazione; *changeover group* per risorsa con tempi per attributo; se l'attributo è uguale tra due operazioni consecutive il suo tempo è zero (classificazione *setup override match*); i tempi dei singoli attributi si sommano, con l'opzione di usare solo il più lungo se gli attrezzaggi sono concorrenti (*concurrent setup times*); «like to like» per partire con pochi dati prima di una matrice completa; vincoli secondari (utensili specifici richiesti da un'operazione o da un valore d'attributo). | Verificato. È esattamente il modello dell'esempio dell'utente (stampo 10 min, colore 5 min, stesso stampo = solo 5): somma dei cambi per attributo, zero se uguale. Si parte con attributi e non con una matrice completa da/verso. |
| **Symestic (scheduling dettagliato)** | Il cambio da A a B può differire da B ad A; nello stampaggio la variazione tra la sequenza migliore e la peggiore può arrivare a 10×; «un utensile, due macchine»: lo scheduler non deve assegnarlo a entrambe nello stesso momento; ottimizzare la sequenza riduce il rapporto setup/ore del 20-40% rispetto al FIFO (dato di un fornitore, non indipendente). | Verificato come fonte di fornitore. Conferma i due problemi: risorse condivise e sequenza. |
| **PlanetTogether (best practice)** | Raggruppare i lavori per attributi condivisi (colore, materiale, utensili) è il modo più efficace di recuperare capacità; stampi, matrici e attrezzature vanno trattati come vincolo di prima classe e non come semplice attributo della macchina. | Verificato come blog di fornitore. |
| **Articolo accademico (stampaggio a iniezione, ScienceDirect, 2020)** | Un certo stampo può essere montato solo su alcune macchine; lo stesso stampo ha tempi di produzione diversi su macchine diverse; gli stampi sono pesanti (gru): il cambio stampo ha un tempo proprio; lo stesso stampo non può essere su due macchine insieme; cambio nullo o trascurabile se si resta sullo stesso stampo. | Verificato sull'abstract. Da qui: compatibilità attrezzatura-macchina e quantità disponibile; l'efficienza per macchina (già presente) copre i tempi diversi. |
| **IBM CP Optimizer / OR-Tools CP-SAT** | La distanza di transizione tra intervalli consecutivi di una risorsa disgiuntiva (tipo A → tipo B) è la forma standard dei tempi di cambio dipendenti dalla sequenza; CP-SAT lo realizza con `no_overlap` più `circuit` e archi con costo e tempo di transizione (esempio ufficiale «transitions in no_overlap»); nel forum di OR-Tools il caso «flexible job shop con setup dipendenti dalla sequenza» è risolto con intervalli opzionali per macchina. | Verificato su documentazione. Il modello dati deve poter produrre, per ogni macchina, il tempo di transizione tra due lavori: sommatoria dei cambi per attributo/attrezzatura. |
| **OR-Tools per .NET** | Pacchetto NuGet `Google.OrTools` 9.15 (gen. 2026) con target .NET 8 e pacchetti nativi per win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64; su Linux richiede `libgcc`. | Verificato su NuGet e GitHub; compatibilità a runtime con .NET 10 da verificare con uno spike nella spec dell'ottimizzatore. |

**Problemi critici noti** (dalle fonti sopra): manutenere una matrice completa da/verso è oneroso e i dati sbagliati producono piani «sicuri ma errati» → partiamo da attributi con tempi per cambio; l'attrezzatura condivisa è un vincolo duro, non una preferenza; la compatibilità macchina-attrezzatura è parziale; i cambi pesanti hanno persone e mezzi propri (non modellati qui); la durata di vita e la manutenzione delle attrezzature (colpi, ispezioni) sono un vincolo reale (fuori scopo).

## 2. Requirements

### REQ-1: Anagrafica delle attrezzature
- **User Story**: As a responsabile di produzione, I want un elenco delle attrezzature (stampi, seghe, accessori) so that so quali sono, quante sono e su quali macchine si montano.
- **Acceptance Criteria**:
  - [ ] Entità `Tooling`: `Code` (univoco, maiuscolo), `Name`, `Description`, `Quantity` (unità identiche disponibili, ≥ 1, default 1), `SetupMinutes` (minuti per montarla su una macchina, ≥ 0), `IsActive` (disattivazione al posto della cancellazione, come le macchine).
  - [ ] Compatibilità opzionale con le macchine (`ToolingEquipment`): nessun collegamento = montabile su qualunque macchina; con collegamenti, solo su quelle. Una macchina disattivata non conta come compatibile.
  - [ ] CRUD con 404/409/422 specifici (codice duplicato, quantità non valida); non si elimina un'attrezzatura usata da passi di ciclo o lavori: si disattiva; disattivarla con lavori aperti che la richiedono non è bloccante ma genera segnalazioni (REQ-6).
  - [ ] Pagina «Attrezzature» nel menu Anagrafiche con elenco, ricerca, filtro attive, dettaglio con macchine compatibili e passi/lavori che la usano.
  - [ ] Ogni 409 dichiara il motivo specifico (nessun «riprova»).
- **Technical Constraints**: Nuova feature `ToolingFeature` in `libs/api-features`, entità in `libs/api-core`, migration; repository vuoti, nessun `DbContext`; stesso schema delle macchine (`EquipmentFeature`).

### REQ-2: Attrezzature richieste dal passo di ciclo e dal lavoro
- **User Story**: As a responsabile di produzione, I want indicare quali attrezzature servono a un passo del ciclo so that il piano sa cosa non può essere usato due volte.
- **Acceptance Criteria**:
  - [ ] Un passo di ciclo può richiedere zero o più attrezzature (una unità ciascuna), con l'editor del ciclo esistente esteso (selezione multipla tra le attive).
  - [ ] Al rilascio dell'ordine le attrezzature richieste vengono **copiate** sul lavoro (`WorkOrderTooling` con codice e nome istantanei), come per ciclo e distinta: modificare il ciclo dopo non cambia i lavori già rilasciati.
  - [ ] Se tra le attrezzature richieste ce n'è una disattivata al rilascio: 422 specifico che la nomina.
  - [ ] Pannello del lavoro e dettaglio ordine mostrano le attrezzature richieste.
- **Technical Constraints**: Come lo snapshot esistente di ciclo e distinta; nessuna regola nelle macchine a stati.

### REQ-3: Attributi di cambio e valori
- **User Story**: As a responsabile di produzione, I want definire cosa fa cambiare l'attrezzaggio (colore, materiale…) so that il tempo di cambio rispecchia la realtà.
- **Acceptance Criteria**:
  - [ ] Entità `SetupAttribute`: `Code` (univoco), `Name`, `ChangeMinutes` (minuti se il valore cambia tra due lavori consecutivi sulla stessa macchina, ≥ 0), `IsActive`. Esempio: `COLORE` = 5 min.
  - [ ] Un passo di ciclo può avere un **valore** per ciascun attributo (testo libero normalizzato, es. «rosso»); valori assenti = attributo non rilevante per quel passo.
  - [ ] I valori vengono copiati sul lavoro al rilascio (`WorkOrderSetupValue`).
  - [ ] Pagina «Attributi di cambio» (Anagrafiche) con CRUD e messaggi specifici; l'editor del ciclo permette di impostare i valori.
- **Technical Constraints**: Stesso schema delle altre anagrafiche; confronto dei valori senza distinguere maiuscole/minuscole e spazi ai bordi.

### REQ-4: Calcolo dell'attrezzaggio dipendente dalla sequenza
- **User Story**: As a responsabile di produzione, I want che il tempo di attrezzaggio dipenda dal lavoro precedente so that le stime e il piano siano realistici.
- **Acceptance Criteria**:
  - [ ] **Stato della macchina**: attrezzature montate e valori d'attributo dell'ultimo lavoro eseguito (in esecuzione o terminato) o, nel piano, del lavoro precedente sulla stessa macchina. Lo stato resta tra un lavoro e l'altro (anche dopo pause e chiusure).
  - [ ] **Formula**: attrezzaggio = somma di (a) `SetupMinutes` di ogni attrezzatura richiesta dal nuovo lavoro e non montata dal precedente; (b) `ChangeMinutes` di ogni attributo il cui valore differisce dal precedente (assente nel precedente = differente). Se più cambi sono necessari si **sommano** (opzione «in parallelo, il massimo» non inclusa).
  - [ ] **Primo lavoro / stato ignoto / nessun attributo né attrezzatura nel passo**: vale `SetupMinutes` del passo di ciclo (attrezzaggio completo), come oggi. Il valore del passo resta quindi l'attrezzaggio massimo di riferimento; `SetupMinutes` del passo è sempre un tetto: l'attrezzaggio calcolato non lo supera.
  - [ ] Esempio guida (test): stampo 10 min, colore 5 min: stampo e colore diversi = 15, stesso stampo e colore diverso = 5, tutto uguale = 0; con `SetupMinutes` del passo = 12 il risultato è limitato a 12 nel primo caso.
  - [ ] Il tempo di attrezzaggio entra nella durata del lavoro con lo stesso trattamento dell'attuale `SetupMinutes` (stessa formula di durata, calendario e efficienza della macchina). L'attrezzaggio calcolato e la sua motivazione («cambio stampo ST-01: 10 min; cambio colore: 5 min») sono esposti per ogni lavoro pianificato.
  - [ ] La stima di fine ordine esistente usa il tempo completo (stato ignoto) e resta un ripiego per ordini non pianificati.
  - [ ] Calcolo come **funzione pura** testabile senza database.
- **Technical Constraints**: Servizio puro in `PlanningFeature` (o libreria condivisa con l'estimatore); nessuna duplicazione della formula tra stima, motore e anteprima.

### REQ-5: Motore di pianificazione: attrezzature e sequenza
- **User Story**: As a responsabile di produzione, I want che il piano non usi lo stesso stampo su due macchine insieme e tenga conto dei cambi so that il piano sia eseguibile.
- **Acceptance Criteria**:
  - [ ] **Vincolo attrezzatura**: in ogni istante le unità di un'attrezzatura in uso (somma sui lavori che la richiedono, inclusi attrezzaggio e lavorazione) non superano `Quantity`; vale anche per i lavori `Running` e fissati (intervalli fissi).
  - [ ] **Compatibilità**: un lavoro che richiede un'attrezzatura può andare solo su macchine compatibili con tutte le sue attrezzature; se non esiste una macchina compatibile e attiva il lavoro resta non pianificabile con motivo nominato.
  - [ ] **Attrezzaggio in sequenza**: il motore calcola l'attrezzaggio di ogni lavoro dal lavoro realmente precedente sulla macchina nel piano di uscita. Un inserimento in un vuoto è ammesso solo se il lavoro che lo segue non peggiora oltre quanto il vuoto consente; **proprietà verificata dai test**: per ogni macchina e ogni coppia di lavori consecutivi, l'attrezzaggio riportato coincide con la formula di REQ-4.
  - [ ] **Scelta della macchina**: a parità di fine, preferisce la macchina il cui stato richiede meno attrezzaggio.
  - [ ] I lavori `Running` e fissati restano intervalli fissi; l'attrezzaggio dei lavori liberi che li seguono è calcolato dal loro stato.
  - [ ] Il motore resta deterministico, dietro `IPlanningEngine`, con motivi per ogni attesa («aspetta lo stampo ST-01, in uso su VER-1 fino alle 14:30»).
  - [ ] Il banco di prova ha almeno tre scenari nuovi (stampo condiviso su due macchine; colore/stampo con cambio ridotto; compatibilità parziale) con valori di riferimento che l'ottimizzatore futuro dovrà battere.
- **Technical Constraints**: Estensione del modello d'ingresso/uscita del motore; ordine di elaborazione e regole dei lavori fissati invariati; nessuna dipendenza nuova.

### REQ-6: Segnalazioni e conflitti
- **User Story**: As a responsabile di produzione, I want sapere quando le attrezzature creano un problema so that decido come intervenire.
- **Acceptance Criteria**:
  - [ ] Nuove segnalazioni (stesse sedi delle esistenti): **attrezzatura sovrapposta** (lavori fissati o spostati a mano che superano la quantità disponibile, con i lavori coinvolti), **attrezzatura non compatibile con la macchina** (per assegnazioni manuali o lavori fissati), **attrezzatura disattivata** richiesta da lavori aperti, **lavoro non pianificabile per attrezzatura** (nessuna macchina compatibile).
  - [ ] Lo spostamento manuale e il blocco del piano mostrano questi conflitti nell'anteprima senza bloccarli, come già per capacità e predecessori.
  - [ ] Causa nominata e suggerimento («sposta uno dei due lavori», «scegli una macchina compatibile con ST-01», «riattiva l'attrezzatura»).
- **Technical Constraints**: Stesso calcolatore delle segnalazioni del motore; nessun dato duplicato.

### REQ-7: Interfaccia
- **User Story**: As a responsabile di produzione, I want vedere attrezzature e attrezzaggi nel piano so that capisco perché un lavoro è dove è.
- **Acceptance Criteria**:
  - [ ] Timeline: la parte di attrezzaggio all'inizio della barra è visivamente distinta dalla lavorazione (tratto più chiaro con pattern non solo colore); tooltip e pannello riportano durata e motivazione dell'attrezzaggio e le attrezzature richieste; legenda aggiornata.
  - [ ] Filtro «Attrezzatura» nella timeline (evidenzia i lavori che la usano, con i connettori d'uso sulla stessa attrezzatura nello stesso intervallo).
  - [ ] Anteprima di pianificazione: per ogni lavoro mostra attrezzaggio calcolato e motivi legati alle attrezzature; riepilogo con minuti totali di attrezzaggio prima e dopo.
  - [ ] Pagina Attrezzature con elenco dei lavori che la usano (macchina, orario) e dialoghi di creazione/modifica accessibili.
  - [ ] Editor del ciclo: selezione attrezzature e valori d'attributo con etichette, errori inline e tastiera.
- **Technical Constraints**: Componenti helm, token semantici, nessun colore fisso; client Angular generato; date nel fuso aziendale con gli helper esistenti.

## 3. Layers impacted

- **Backend**: sì (nuova `ToolingFeature`; `RoutingFeature` e `ProductionOrderFeature` per requisiti e snapshot; `PlanningFeature` per motore, anteprima, segnalazioni; migration).
- **Web**: sì (pagine Attrezzature e Attributi di cambio, editor del ciclo, timeline, pannello, anteprima, segnalazioni).
- **Nx workspace**: no (rigenerazione del client con `api-client:generate-api`).

## 4. Impact Assessment

- **File/moduli coinvolti**: `libs/api-core` (entità, migration), `libs/api-features` (`ToolingFeature` nuova, `RoutingFeature`, `ProductionOrderFeature`, `PlanningFeature`, seed di esempio), `openapi/Api.json` e client generato, `apps/web/src/app/features/{tooling,setup-attributes,routing,planning,work-orders}`, `doc/glossary.md`, `doc/structure.md`, test unitari e di integrazione.
- **Breaking changes**: no per l'API. Cambia il comportamento del piano per i passi con attrezzature o attributi; i passi esistenti (senza attributi) mantengono l'attrezzaggio fisso attuale.

## 5. Fuori scopo

- Persone o mezzi per eseguire il cambio (gru, attrezzisti) come vincolo; attrezzaggi concorrenti (massimo invece della somma).
- Vita utile e manutenzione delle attrezzature (colpi, ispezioni, affilature).
- Matrice completa da/verso per coppie di prodotti; tempi di attrezzaggio diversi per macchina.
- Attrezzatura che resta montata su una macchina tra i lavori come risorsa occupata (l'uso è limitato alla durata dei lavori che la richiedono; lo stato «montata» serve solo al calcolo dell'attrezzaggio).
- Magazzino, giacenze e trasporto delle attrezzature; linee e reparti.
- **Ottimizzazione del piano con OR-Tools CP-SAT: spec successiva**, che userà il modello dati di questa spec per ridurre i cambi riordinando i lavori.

## 6. Decisioni confermate

1. `SetupMinutes` del passo = attrezzaggio completo (primo lavoro o stato ignoto) e tetto dell'attrezzaggio calcolato.
2. Più cambi simultanei si sommano (stampo + colore = 15 min nell'esempio).
3. Un passo può richiedere più attrezzature, una unità ciascuna.
4. L'attrezzatura è occupata solo durante i lavori che la richiedono (attrezzaggio incluso), non quando resta montata tra due lavori.
5. `Quantity` resta (default 1) per attrezzature con più unità identiche.
