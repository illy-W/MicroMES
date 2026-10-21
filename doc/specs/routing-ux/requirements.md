# Spec: Ciclo di lavoro, lettura e modifica (routing-ux)

## 1. Context & Goals

La spec `routing` ha introdotto l'editor del ciclo con ngDiagram. Una critica con la skill `impeccable` (24/40, vedi sotto) ha mostrato che la pagina funziona ma tratta allo stesso modo chi **legge** il ciclo e chi lo **modifica**, e non scala oltre una decina di passi. Questa spec migliora l'uso, senza cambiare il modello dati né le API.

Decisioni di prodotto già prese:

- Il canvas **ngDiagram si mantiene**: dà un'immagine professionale.
- La pagina si apre in **sola lettura**; la modifica si attiva con un pulsante esplicito.
- Il **numero operazione è proposto dal sistema**, non digitato.

Il lavoro si divide in due giri, con una fermata di controllo tra l'uno e l'altro:

- **Giro 1**: lettura e modifica (REQ-1, REQ-2, REQ-3).
- **Giro 2**: robustezza dell'editing (REQ-4, REQ-5, REQ-6, REQ-7, REQ-8).

Fuori ambito: quantità d'ordine nei tempi (il calcolo usa 1 unità), conflitti tra più utenti, layout sempre automatico, undo/redo, versioni del ciclo, modifiche al backend, test automatici.

Evidenze della critica da cui parte la spec (in sintesi): accessibilità da tastiera assente per i collegamenti, errori di validazione senza posizione sul canvas, nessun tempo totale, grafo illeggibile con 20+ passi, pannello laterale instabile, nessun feedback di salvataggio, vista Elenco senza intestazione fissa.

## 2. Requirements

### REQ-1: Lettura di default e modalità modifica

- **User Story**: As a operatore o pianificatore, I want aprire il ciclo in sola lettura so that non lo modifichi per errore, e attivare la modifica solo quando serve.
- **Acceptance Criteria**:
  - [ ] All'apertura la pagina è in **lettura**: i nodi non sono trascinabili, non si creano né si eliminano archi, non esiste la palette delle fasi né il pannello di modifica. Pan, zoom, minimap e vista Elenco restano attivi.
  - [ ] In lettura, selezionare un nodo mostra i dettagli del passo (fase, numero, tempi standard ed effettivi, note) in sola lettura, senza campi modificabili.
  - [ ] Il pulsante primario "Modifica ciclo" attiva la modalità modifica. In modifica compaiono "Salva", "Annulla" e le funzioni dell'editor attuale; "Modifica ciclo" scompare.
  - [ ] "Salva" riuscito riporta in lettura. "Annulla" senza modifiche riporta in lettura; con modifiche chiede conferma (alert-dialog) e poi scarta la bozza.
  - [ ] Il controllo di uscita con modifiche non salvate e `beforeunload` restano, ma attivi solo in modalità modifica con bozza modificata.
  - [ ] Un ciclo vuoto in lettura mostra un invito con il pulsante "Crea il ciclo", che attiva la modifica.
  - [ ] Nessuna modifica alle API né al modello dati.
- **Technical Constraints**: stato di modalità a signal nella pagina. In lettura i nodi sono non trascinabili e non collegabili tramite la configurazione di ng-diagram (verificare l'opzione corretta nella documentazione); se serve un modello separato, motivarlo nel report.

### REQ-2: Riepilogo e percorso più lungo

- **User Story**: As a pianificatore, I want vedere subito quanto vale un ciclo so that capisca tempi e colli di bottiglia senza sommarli a mano.
- **Acceptance Criteria**:
  - [ ] Barra riepilogativa sopra il diagramma con: numero di passi, **contenuto di lavoro** (somma di tutti i tempi effettivi per 1 unità: attrezzaggio più lavorazione) e **percorso più lungo** (tempo del cammino più lungo nel grafo, con rami paralleli, per 1 unità).
  - [ ] In lettura i passi e gli archi del percorso più lungo sono evidenziati con un token semantico (non un colore fisso), con una legenda testuale.
  - [ ] I tempi sono formattati in minuti e ore (es. "95 min · 1 h 35 min").
  - [ ] I valori si ricalcolano durante la modifica, sulla bozza.
  - [ ] Il calcolo è una funzione pura del grafo (`routing-metrics.ts`), senza chiamate API.
- **Technical Constraints**: il calcolo usa i tempi **effettivi** e quantità 1 (la quantità d'ordine arriverà con gli ordini di lavoro). Un grafo non valido (ciclico) mostra i riepiloghi come "n/d" e non si blocca. Formato numeri `it-IT`.

### REQ-3: Numero operazione proposto dal sistema

- **User Story**: As a pianificatore, I want che i numeri operazione siano assegnati dal sistema so that non debba inventarli né mantenerli coerenti a mano.
- **Acceptance Criteria**:
  - [ ] Il campo "numero operazione" non è modificabile nel pannello del passo: è mostrato come valore proposto (sola lettura).
  - [ ] Un nuovo passo riceve il numero `massimo esistente + 10` (multipli di 10).
  - [ ] L'azione "Rinumera" (modifica) riassegna 10, 20, 30... seguendo l'ordine del flusso (ordinamento topologico; a parità, per posizione verticale e poi per numero attuale). I passi esistenti mantengono l'`id`.
  - [ ] Se al salvataggio un passo ha un numero maggiore o uguale a quello di un suo successore, compare un avviso **non bloccante** "I numeri non seguono il flusso" con il pulsante "Rinumera". Il salvataggio resta possibile.
  - [ ] I numeri non cambiano da soli ad ogni modifica del grafo.
- **Technical Constraints**: nessuna modifica al backend (resta l'unicità di `operationNumber` per materiale). Durante la rinumerazione i numeri devono rimanere univoci nella bozza; il backend gestisce già lo scambio dei numeri.

### REQ-4: Modifica accessibile da tastiera

- **User Story**: As a pianificatore che usa la tastiera, I want creare e togliere dipendenze senza trascinare so that possa completare il ciclo.
- **Acceptance Criteria**:
  - [ ] Il pannello del passo ha le sezioni "Predecessori" e "Successori": ogni sezione elenca i passi che possono essere collegati (esclusi quelli che creerebbero un ciclo, un anello o un duplicato) e permette di aggiungere e togliere il collegamento da tastiera.
  - [ ] La regola anti-ciclo è la stessa di `validateConnection` (una sola implementazione condivisa).
  - [ ] Il nodo ha ruolo e `aria-label` ("Operazione 30, SAL, Saldatura"), focus visibile (`focus-visible:ring`) e annuncia la selezione.
  - [ ] Un'area `aria-live="polite"` annuncia aggiunta e rimozione di passi e collegamenti, e il risultato del salvataggio.
  - [ ] Le icone decorative sono `aria-hidden`.
- **Technical Constraints**: componenti helm (input, label, select o elenco di pulsanti con `aria-pressed`: se il select helm non supporta la selezione multipla, motivare la scelta). Verificare nella documentazione di ng-diagram cosa copre già la tastiera (selezione, spostamento, Canc) e documentarlo nella pagina (suggerimento o legenda). Nessun colore fisso né `dark:`.

### REQ-5: Errori di validazione sul diagramma

- **User Story**: As a pianificatore, I want sapere quali passi hanno un problema so that li corregga subito.
- **Acceptance Criteria**:
  - [ ] `validateRouting` restituisce, per ogni problema, un messaggio e gli id dei passi coinvolti.
  - [ ] I passi con problema sono evidenziati nel nodo (bordo e testo con i token di errore) e il messaggio ha il pulsante "Mostra nel diagramma", che seleziona i passi e adatta la vista.
  - [ ] La validazione si ricalcola durante la modifica, e l'avviso scompare quando il problema è risolto; l'indicatore "Ciclo valido" o "N problemi" è sempre visibile in modifica.
  - [ ] I problemi di validazione (modificabili dall'utente) e gli errori del backend al salvataggio sono due blocchi distinti, ognuno con ruolo e `aria-live` adeguati.
  - [ ] Gli errori del backend 422 mostrano il testo ricevuto e restano finché non si salva di nuovo o si modifica.
- **Technical Constraints**: il backend resta l'autorità; la validazione client non lo sostituisce. Messaggi in italiano, senza gergo di diagramma.

### REQ-6: Feedback di salvataggio e annullamento

- **User Story**: As a pianificatore, I want sapere che cosa succede quando salvo so that mi fidi della pagina.
- **Acceptance Criteria**:
  - [ ] Durante il salvataggio il pulsante mostra "Salvataggio…" con uno spinner ed è disabilitato.
  - [ ] Dopo il salvataggio riuscito compare una notifica "Ciclo salvato" (toast).
  - [ ] L'indicatore "Modifiche non salvate" è ben visibile (non solo outline) e riserva lo spazio per non spostare i pulsanti.
  - [ ] "Annulla" con modifiche chiede conferma. Scorciatoia `Ctrl/Cmd + S` per salvare in modifica.
- **Technical Constraints**: riusare l'alert-dialog helm esistente per la conferma. Il toast usa il componente helm `sonner` (da generare, vedi REQ-9).

### REQ-7: Scala e stabilità del layout

- **User Story**: As a pianificatore, I want lavorare su cicli di 20 passi o più so that il diagramma resti leggibile e controllabile.
- **Acceptance Criteria**:
  - [ ] Dopo "Aggiungi" il nuovo nodo è selezionato e centrato nella vista, e posizionato vicino al passo selezionato (o in fondo se nessuno lo è), non in una colonna fissa fuori schermo.
  - [ ] Pulsanti "Riordina" (applica il layout a strati a tutti i nodi, dopo conferma perché cambia le posizioni) e "Adatta" (adatta la vista) nella barra del canvas.
  - [ ] Campo di ricerca sopra la palette delle fasi (filtro su codice e nome).
  - [ ] Palette e pannello del passo stanno in un contenitore a schede ("Fasi" e "Passo"), con altezza stabile: la selezione di un nodo non fa saltare gli elementi. Il pannello è sticky da `lg` in su.
  - [ ] Il testo dei nodi non scende sotto `text-xs`; il nome lungo ha un tooltip con il testo completo. Un solo segnale per lo stato selezionato.
  - [ ] Layout iniziale: i passi con **una sola** coordinata nulla sono trattati come senza posizione (non si sovrappongono); i passi con posizioni complete le mantengono.
  - [ ] Vista Elenco: intestazione fissa e altezza massima con scorrimento interno, e riga di totale dei tempi. L'indicazione "Sola lettura" è visibile.
  - [ ] L'altezza del canvas non dipende solo dalla viewport: `h-[max(24rem,calc(100dvh-14rem))]` o equivalente, verificata a 768 px di altezza.
- **Technical Constraints**: solo spartan helm e token semantici. Nessuna nuova libreria oltre ai componenti generati con il CLI (REQ-9).

### REQ-8: Titolo, testi e struttura della pagina

- **User Story**: As a utente, I want testi chiari e una struttura riconoscibile so that capisca subito dove sono.
- **Acceptance Criteria**:
  - [ ] Il titolo mostra il nome del materiale come `h1`, con "Ciclo di lavoro" e il codice in un'etichetta sopra; il link "← Materiali" resta in alto (o un breadcrumb helm se esiste).
  - [ ] Il sottotitolo è breve e senza gergo ("porte" e "collega da destra a sinistra" spariscono). In modifica, un suggerimento chiaro spiega come collegare i passi e quali scorciatoie esistono.
  - [ ] La vista Diagramma/Elenco usa il componente helm `tabs`; gli errori usano `alert`; i pannelli usano `card`.
  - [ ] Legenda di "Op.", "modificato" e "tempo effettivo" accessibile dalla pagina (tooltip o breve legenda).
- **Technical Constraints**: copy in italiano, coerente con il vocabolario del glossario (Fase, Passo, Dipendenza).

### REQ-9: Componenti spartan mancanti

- **User Story**: As a team, I want usare i componenti helm giusti so that la pagina non ricorra a `div` costruiti a mano.
- **Acceptance Criteria**:
  - [ ] Generati con il CLI di spartan, senza modifiche a mano, i componenti `alert`, `tabs`, `tooltip`, `card` e `sonner`, se non già presenti, con lint pulito e alias con prefisso `./`.
  - [ ] `sonner` è montato una sola volta nel layout e usato per "Ciclo salvato".
  - [ ] Nessun altro componente è aggiunto.
- **Technical Constraints**: comando `npx nx g @spartan-ng/cli:ui <nome> --no-interactive`, poi `npx nx lint <nome> --fix`. Nessun `baseUrl` in `tsconfig.base.json`.

### REQ-10: Documentazione

- **User Story**: As a team, I want le convenzioni dell'editor documentate so that le prossime pagine a diagramma le seguano.
- **Acceptance Criteria**:
  - [ ] `doc/structure.md` ("Routing editor") descrive la modalità lettura/modifica, la numerazione proposta, il calcolo del percorso più lungo e i componenti helm usati.
  - [ ] `doc/glossary.md`: nessun nuovo termine di dominio; se compaiono "contenuto di lavoro" e "percorso più lungo" (critical path), registrarli come termini d'interfaccia.
- **Technical Constraints**: solo file in `doc/`.

## 3. Layers impacted

- **Backend**: nessuna modifica.
- **Web**: `features/routing` (pagina, nodo, validazione, layout, nuova `routing-metrics.ts`), layout (`sonner`), componenti helm generati.
- **Nx workspace**: generazione di `alert`, `tabs`, `tooltip`, `card`, `sonner` in `libs/ui`, con aggiornamento di `tsconfig.base.json` e `nx.json` da parte del generatore.

## 4. Impact Assessment

- **File interessati**: `apps/web/src/app/features/routing/*`, il layout principale (montaggio di `sonner`), `libs/ui/{alert,tabs,tooltip,card,sonner}` (generati), `tsconfig.base.json`, `nx.json`, `doc/structure.md`.
- **Breaking changes**: no. Cambia solo l'uso della pagina (si apre in lettura).
- **Rischi**:
  - La lettura in sola lettura dipende dalle opzioni di ng-diagram: se non supportano nodi non trascinabili e non collegabili, serve un modello diverso per la lettura (da verificare per prima nel task relativo).
  - Il comportamento di ng-diagram non è ancora stato visto a schermo: il browser dell'agente non ha una finestra visibile. Le verifiche visive passano da uno screenshot dell'utente.
  - Il percorso più lungo con quantità 1 non rappresenta ancora un ordine reale: va rivisto con gli ordini di lavoro.
  - `Ctrl/Cmd + S` può confliggere con la scorciatoia del browser: va impedito il comportamento predefinito solo in modifica.
