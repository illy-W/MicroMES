# Spec: Landing page

## 1. Context & Goals

MicroMES non ha oggi una pagina pubblica che spieghi cosa è. Serve una **landing page come sito separato** dall'app gestionale (`apps/web`), sul modello di `ptconnect-nx/apps/landing` (Angular, SSR con prerender, sezioni a componenti, Dockerfile nginx statico).

Messaggi chiave (da `doc/product.md`):

- MES **nato per le PMI** e le micro imprese manifatturiere (1-20 persone: laboratori, piccole officine, artigiani).
- **Totalmente gratuito** (nessun canone, nessun piano a pagamento, nessuna funzione "premium").
- Semplice prima che completo, nessun consulente necessario; vocabolario standard ISA-95; dati storici immutabili (istantanea al rilascio).
- Installazione **con un solo script** (il contenuto dello script è, per ora, un segnaposto inventato).

Obiettivo: far capire in meno di un minuto cosa fa, per chi è, che costa zero e come si prova; mostrare il prodotto reale con screenshot.

Decisioni di design (da confermare con l'approvazione):

- **ng-diagram: sì.** Una sezione mostra in modo interattivo (sola lettura) il flusso di un ordine: nodi = ordini di lavoro con stato, gruppo = ordine figlio (semilavorato), percorso più lungo evidenziato. Dati statici nel codice, nessuna chiamata API. Layout dei nodi con posizioni fisse (niente ELK: evita il peso nel bundle).
- **spartan-ng: sì.** Componenti helm (`button`, `badge`, `accordion` per le FAQ, `card`) tramite `libs/ui` già esistente (aggiunti con il generatore ufficiale se mancano), token semantici come in `doc/structure.md`.
- **Screenshot reali** del prodotto in `apps/landing/src/assets/images/product/` (WebP, versioni 960/1600 come in ptconnect), catturati dall'app con i dati di esempio del seeder di Development.

## 2. Requirements

### REQ-1: Sito landing separato nel monorepo
- **User Story**: As a visitatore, I want una pagina pubblica dedicata so that capisco MicroMES senza entrare nel gestionale.
- **Acceptance Criteria**:
  - [ ] Esiste un'app Nx `landing` (`apps/landing`), indipendente da `web` e da `Api` (nessuna dipendenza da `@micromes/api-client`).
  - [ ] `npx nx build landing` produce un sito prerenderizzato (`dist/apps/landing/browser`) con `index.html` contenente tutto il contenuto testuale.
  - [ ] `npx nx serve landing` avvia il sito in locale su una porta diversa da quella di `web` (4200 default di `web`: usare 4300).
  - [ ] Lint e build di `landing` passano; `web` non cambia comportamento.
- **Technical Constraints**: generata con `@nx/angular:application` (standalone, SCSS, SSR + prerender, Tailwind v4 con `.postcssrc.json` proprio, stessa versione Angular del workspace). Struttura come ptconnect: `src/app/landing/landing.component.ts` + `sections/*` (componenti OnPush). Nessun i18n: testi in italiano.

### REQ-2: Messaggio "per le PMI, totalmente gratis"
- **User Story**: As a titolare di una piccola officina, I want capire subito che è pensato per realtà come la mia e che costa zero so that lo provo senza rischi.
- **Acceptance Criteria**:
  - [ ] L'hero contiene titolo e sottotitolo che dichiarano "MES per le PMI/micro imprese" e "gratuito".
  - [ ] Una sezione dedicata (al posto del pricing di ptconnect) mostra un unico "piano" a 0 € con elenco di cosa è incluso (tutte le funzioni, nessun limite di utenti/ordini/macchine, nessuna carta di credito) il fatto che è self-hosted sui propri dati, la licenza AGPL-3.0 spiegata in una riga e una sezione "Serve una mano?" (consulenza, installazione e supporto opzionali a pagamento, contatto segnaposto).
  - [ ] La FAQ contiene domande su prezzo ("Cos'è il trucco?"), a chi è rivolto, integrazione ERP (ISA-95), dove stanno i dati.
  - [ ] Non compaiono affermazioni non vere sullo stato del prodotto: le funzioni non ancora realizzate (vista operatore, autenticazione, magazzino, costi, integrazione macchine) sono indicate come "in arrivo" o non citate.
- **Technical Constraints**: testi coerenti con `doc/product.md` (sezioni Vision, Fuori scopo, Roles). Nessun testo "OEE" (l'utilizzo non è OEE, vedi `doc/structure.md`).

### REQ-3: Screenshot del prodotto
- **User Story**: As a visitatore, I want vedere il prodotto reale so that valuto l'interfaccia prima di installarlo.
- **Acceptance Criteria**:
  - [ ] Almeno 4 screenshot reali: panoramica ordine (diagramma), pianificazione/timeline macchine, distinta base o ciclo di lavorazione, lista ordini di lavoro (o pagina macchina).
  - [ ] Ogni immagine ha `alt` descrittivo, `width`/`height`, `srcset` 960/1600, `loading="lazy"` tranne l'hero (`NgOptimizedImage`).
  - [ ] Gli screenshot usano i dati di esempio (officina mobili acciaio e rovere), nessun dato reale.
- **Technical Constraints**: catturati da `web` in Development con il seeder; formato WebP; peso di ogni file < 250 kB.

### REQ-4: Sezione diagramma interattivo (ng-diagram)
- **User Story**: As a visitatore, I want vedere come MicroMES rappresenta un ordine di produzione so that capisco il valore della panoramica grafica.
- **Acceptance Criteria**:
  - [ ] La sezione mostra un diagramma sola lettura con ordini di lavoro (nodi con codice, fase e badge di stato *Terminato/In esecuzione/Pronto/In attesa*), un ordine figlio come gruppo, frecce di dipendenza e percorso più lungo evidenziato.
  - [ ] Pan/zoom disattivati o limitati in modo da non intrappolare lo scroll della pagina; nodi non trascinabili.
  - [ ] Il diagramma è caricato in modo differito (`@defer (on viewport)`) e non pesa sul bundle iniziale; la pagina prerenderizzata non va in errore (nessun accesso a `window` lato server).
  - [ ] Colori solo da token semantici, leggibile con tastiera/lettore schermo: sotto il diagramma un elenco testuale equivalente (`sr-only` o visibile).
- **Technical Constraints**: skill `ng-diagram-*` (`fundamentals`, `custom-nodes`, `styling`, `model-init`, `viewport`) come guida; versione `ng-diagram` dal `package.json` root; modello creato una volta con `initializeModel`; posizioni calcolate a mano nel codice (nessun ELK); mai `display:none` sul diagramma.

### REQ-5: Blocco di installazione "un solo script"
- **User Story**: As a visitatore tecnico, I want vedere come si installa so that capisco che serve un solo comando.
- **Acceptance Criteria**:
  - [ ] Una sezione "Installa in un minuto" mostra un blocco codice con un comando `curl ... | bash` **segnaposto** (es. `curl -fsSL https://get.micromes.example/install.sh | bash`) e 3 passi sotto (scarica, avvia, apri il browser).
  - [ ] Pulsante "Copia" accessibile (`aria-label`, feedback "Copiato" annunciato con `aria-live`).
  - [ ] Accanto al blocco compare una nota visibile che il comando è un esempio e non ancora pubblicato.
  - [ ] Opzionale ma incluso: secondo blocco con requisiti (Docker) e comando di avvio manuale `docker compose up -d`.
- **Technical Constraints**: URL e contenuto sono segnaposto centralizzati in un unico file (`landing-links.ts`, come ptconnect) per sostituirli quando lo script esisterà; nessuno script reale viene creato o pubblicato in questa spec.

### REQ-6: Struttura e contenuti della pagina
- **User Story**: As a visitatore, I want una pagina ordinata e scorrevole so that trovo le informazioni in fretta.
- **Acceptance Criteria**:
  - [ ] Sezioni, in ordine: navbar (ancore, pulsante "Installa", link al repository), hero (titolo, CTA, screenshot principale), problemi che risolve (le 3 domande di `product.md`), funzioni (anagrafiche/distinta/cicli, pianificazione, esecuzione, macchine e calendari, scarti, attrezzaggi e pianificazione a capacità finita), screenshot/galleria, diagramma (REQ-4), "Gratis, per le PMI" (REQ-2), installazione (REQ-5), standard ISA-95 e dati tuoi, FAQ, CTA finale, footer.
  - [ ] Layout responsive (mobile 360 px senza scroll orizzontale), tema chiaro; navbar con ancore a scorrimento morbido (rispetta `prefers-reduced-motion`).
  - [ ] Ogni sezione ha un `h2`; un solo `h1`; contrasto AA; focus visibile.
  - [ ] `<title>`, meta description, Open Graph (`og-image.jpg`), favicon, `robots.txt`, `sitemap.xml`.
- **Technical Constraints**: priorità stile come `doc/structure.md` (helm -> token -> `styles.scss` -> utility). Nessun colore hardcoded né `dark:`. Icone `@ng-icons/lucide`. Design guidato dalla skill `impeccable` (contesto in `PRODUCT.md`/`DESIGN.md`), registro *brand*.

### REQ-7: Distribuzione statica
- **User Story**: As a manutentore, I want un'immagine statica pronta so that pubblico la landing ovunque.
- **Acceptance Criteria**:
  - [ ] `apps/landing` contiene `Dockerfile` (nginx statico sul `dist/apps/landing/browser`), `nginx.conf` e `security-headers.conf` adattati da ptconnect (senza riferimenti a ptconnect/Kubernetes specifici).
  - [ ] Pagina `404.html` statica.
  - [ ] `AGENTS.md` e `doc/structure.md` documentano il nuovo progetto e i comandi (`serve`/`build`).
- **Technical Constraints**: nessuna pipeline CI/CD né deploy in questa spec; nessun cookie né tracker (niente banner). Pagine legali (terms/privacy) **fuori scopo**.

## 3. Layers impacted

- **Backend**: nessuno.
- **Web**: nuova app `apps/landing` (non tocca `apps/web`); eventuali componenti spartan aggiunti in `libs/ui` (es. `badge`, `accordion`, `card`) solo se mancano. Screenshot catturati da `web` (nessuna modifica al codice di `web`).
- **Nx workspace**: nuovo progetto `landing` (generatore `@nx/angular:application`), eventuali `libs/ui/*` nuove, aggiornamento `tsconfig.base.json`/`nx.json` dal generatore spartan.

## 4. Impact Assessment

- File/moduli: `apps/landing/**` (nuovo), `libs/ui/*` (eventuali nuovi), `tsconfig.base.json`, `nx.json`, `package.json` (nessuna dipendenza nuova prevista: `ng-diagram`, spartan, Tailwind, lucide sono già presenti), `AGENTS.md`, `doc/structure.md`.
- Breaking changes: **no**.
- Rischi: bundle iniziale (mitigato da `@defer` e lazy), SSR con ng-diagram (accesso al DOM: render solo nel browser dentro `@defer`), qualità degli screenshot (richiedono DB con seeder e Docker).
- Glossario: nessuna nuova entità di dominio.

## 5. Decisioni prese

1. **Repository**: `https://github.com/illy-W/MicroMES` (dal remote git), usato in navbar e footer.
2. **Licenza**: AGPL-3.0 (file `LICENSE` del repository). La pagina dice "gratuito e open source (AGPL-3.0)": chiunque può usarlo; chi lo modifica o lo offre come servizio in rete deve pubblicare il codice modificato. Si può comunque offrire consulenza, installazione e supporto a pagamento: una sezione "Serve una mano?" lo dichiara con un contatto segnaposto.
3. **Dominio/script**: segnaposto `get.micromes.example`.

Nota: `package.json` dichiara `"license": "MIT"`, in contrasto con `LICENSE` (AGPL-3.0). Non fa parte di questa spec, ma va allineato (follow-up).
