# Tasks: Ciclo di lavoro, lettura e modifica (routing-ux)

## Technical Strategy

- Nessuna modifica al backend né al contratto.
- Ordine: [Nx] 0 → [Frontend] Giro 1 → **fermata di controllo** (verifica visiva con l'utente, commit) → [Frontend] Giro 2 → [Lead] documentazione e verifica finale.
- I componenti helm mancanti (`alert`, `tabs`, `tooltip`, `card`, `sonner`) si generano all'inizio con il CLI (REQ-9), così entrambi i giri li usano.
- Giro 1 parte dalla verifica delle opzioni di ng-diagram per la sola lettura (nodi non trascinabili e non collegabili). Se non bastano, si ferma e riporta prima di scrivere codice.
- Logica pura in file separati e riusabili dal frontend: `routing-metrics.ts` (contenuto di lavoro, percorso più lungo), `routing-numbering.ts` (numero proposto, rinumerazione), regola anti-ciclo condivisa con `validateConnection`.
- Verifica visiva: il browser dell'agente non ha una finestra visibile, quindi la vista a schermo la controlla l'utente (screenshot) alla fermata tra i due giri e alla fine.

## 0. Nx workspace

- [x] 0.1 [Nx] Generare con `npx nx g @spartan-ng/cli:ui <nome> --no-interactive` i componenti `alert`, `tabs`, `tooltip`, `card`, `sonner` (solo quelli non già presenti), poi `npx nx lint <nome> --fix`. Verificare l'alias con prefisso `./`, nessun `baseUrl`, prettier solo sui JSON riscritti, `npx nx run-many -t build lint`. Segnalare dipendenze nuove richieste da `sonner` (es. `ngx-sonner`) con la versione esatta.
  _Satisfies: REQ-9_

## 1. Frontend, giro 1: lettura e modifica

- [x] 1.1 [Frontend] Verifica preliminare: leggere la documentazione di ng-diagram (README in `node_modules/ng-diagram`, https://www.ngdiagram.dev/docs) per le opzioni che rendono i nodi non trascinabili e non collegabili, e le porte non interattive, senza ricreare il modello. Se non esistono, fermarsi e riportare la soluzione alternativa proposta (es. un secondo diagramma in sola lettura).
  _Satisfies: REQ-1_
- [x] 1.2 [Frontend] Modalità lettura/modifica nella pagina: stato a signal, "Modifica ciclo" (primario), "Salva"/"Annulla" in modifica con conferma di scarto in alert-dialog, ritorno in lettura dopo il salvataggio. In lettura: niente palette né pannello di modifica; dettagli del passo selezionato in sola lettura. Ciclo vuoto con "Crea il ciclo". Controllo di uscita e `beforeunload` solo in modifica con bozza modificata.
  _Satisfies: REQ-1_
- [x] 1.3 [Frontend] `routing-metrics.ts` (funzione pura): contenuto di lavoro e percorso più lungo con tempi effettivi per 1 unità, con l'insieme di passi e archi del percorso; "n/d" se il grafo non è valido. Barra riepilogativa (numero di passi, contenuto di lavoro, percorso più lungo, formato "95 min · 1 h 35 min", `it-IT`), evidenziazione del percorso in lettura con token semantici e legenda testuale; ricalcolo sulla bozza in modifica.
  _Satisfies: REQ-2_
- [x] 1.4 [Frontend] `routing-numbering.ts` (funzione pura): numero proposto per un nuovo passo (massimo + 10), "Rinumera" (ordine topologico, a parità posizione verticale e poi numero attuale, multipli di 10). Campo numero operazione in sola lettura nel pannello, azione "Rinumera" in modifica, avviso non bloccante "I numeri non seguono il flusso" con pulsante "Rinumera" al salvataggio o durante la modifica.
  _Satisfies: REQ-3_
- [x] 1.5 [Frontend] Usare `tabs` helm per Diagramma/Elenco e `alert` per i blocchi di errore già presenti (cambio minimo, il resto è nel giro 2). `npx nx lint web` e `npx nx build web`.
  _Satisfies: REQ-1, REQ-8_

**Fermata di controllo:** il lead chiede all'utente di guardare la pagina (lettura, modifica, riepilogo, rinumerazione), poi commit del giro 1.

## 2. Frontend, giro 2: robustezza dell'editing

- [ ] 2.1 [Frontend] Modifica da tastiera: sezioni "Predecessori" e "Successori" nel pannello del passo (elenco dei passi collegabili senza cicli, anelli, duplicati; aggiunta e rimozione da tastiera), regola anti-ciclo condivisa con `validateConnection`. Nodo con ruolo e `aria-label`, `focus-visible:ring`, annunci in una regione `aria-live="polite"`, icone decorative `aria-hidden`. Legenda delle scorciatoie di ng-diagram già coperte (selezione, spostamento, Canc).
  _Satisfies: REQ-4_
- [ ] 2.2 [Frontend] Validazione con posizioni: `validateRouting` restituisce messaggi e id dei passi coinvolti; evidenziazione dei nodi con problema (token di errore), pulsante "Mostra nel diagramma" (seleziona e adatta la vista), validazione live con indicatore "Ciclo valido" / "N problemi", blocchi distinti per problemi di validazione e errori del backend, ognuno con ruolo e `aria-live`.
  _Satisfies: REQ-5_
- [ ] 2.3 [Frontend] Feedback di salvataggio: "Salvataggio…" con spinner, toast "Ciclo salvato" con `sonner` (montato una sola volta nel layout, quindi tocca il componente del layout), indicatore "Modifiche non salvate" visibile e con spazio riservato, conferma su "Annulla" con modifiche, scorciatoia `Ctrl/Cmd + S` solo in modifica (con `preventDefault`).
  _Satisfies: REQ-6_
- [ ] 2.4 [Frontend] Scala: nodo nuovo selezionato, centrato e posizionato vicino al passo selezionato; "Riordina" (con conferma) e "Adatta" nella barra del canvas; ricerca nella palette; palette e pannello in schede "Fasi"/"Passo" con altezza stabile e pannello sticky da `lg`; nodi senza testo sotto `text-xs`, tooltip sul nome troncato, un solo segnale per la selezione; layout iniziale che tratta come senza posizione i passi con una sola coordinata nulla; altezza del canvas `h-[max(24rem,calc(100dvh-14rem))]` o equivalente.
  _Satisfies: REQ-7_
- [ ] 2.5 [Frontend] Vista Elenco: intestazione fissa, altezza massima con scorrimento interno, riga di totale dei tempi, indicazione visibile "Sola lettura".
  _Satisfies: REQ-7_
- [ ] 2.6 [Frontend] Titolo e testi: nome del materiale come `h1` con etichetta "Ciclo di lavoro" e codice sopra, sottotitolo breve senza gergo, suggerimento di collegamento in modifica, pannelli con `card`, legenda di "Op.", "modificato" e "tempo effettivo" (tooltip o breve legenda).
  _Satisfies: REQ-8_
- [ ] 2.7 [Frontend] `npx nx lint web` e `npx nx build web`. Dichiarare cosa non è stato verificato a runtime (il browser dell'agente non ha finestra visibile).
  _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5, REQ-6, REQ-7, REQ-8_

## 3. Documentazione e verifica

- [ ] 3.1 [Lead] `doc/structure.md` ("Routing editor"): modalità lettura/modifica, numerazione proposta, percorso più lungo, componenti helm usati. `doc/glossary.md`: termini d'interfaccia "contenuto di lavoro" e "percorso più lungo" se compaiono.
  _Satisfies: REQ-10_
- [ ] 3.2 [Lead] `npx nx affected -t lint build`, controllo dei criteri di accettazione di ogni REQ, verifica visiva con l'utente (screenshot): lettura, modifica, errori sui nodi, ciclo con 20+ passi (creato in locale e poi scartato), tastiera.
  _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5, REQ-6, REQ-7, REQ-8, REQ-9, REQ-10_
