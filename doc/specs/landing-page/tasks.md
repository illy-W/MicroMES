# Tasks: Landing page

Stato: requisiti e task APPROVATI dall'utente ("procedi con l'implementazione"). Licenza: AGPL-3.0.

## Technical Strategy

- Nuova app Nx `landing` (Angular standalone, SCSS, SSR + prerender, Tailwind v4 con `.postcssrc.json` proprio), porta di serve 4400 (4300 è di `web`). Modello: `ptconnect-nx/apps/landing` (percorso `~/personal/ptconnect/ptconnect-nx/apps/landing`).
- Nessun backend. Nessuna dipendenza da `@micromes/api-client`.
- Componenti spartan dalle `libs/ui` (aggiunti col generatore ufficiale, mai a mano). Token semantici, niente colori hardcoded né `dark:`.
- Diagramma ng-diagram in `@defer (on viewport)`, dati e posizioni statici, sola lettura.
- URL/segnaposto in un solo file `landing-links.ts`.

## Nx workspace

- [x] **T1** [Nx] Generare l'app `landing` (`@nx/angular:application`, porta serve 4300, SSR+prerender, SCSS, standalone), Tailwind v4 (`.postcssrc.json`), `tags` e lint coerenti; verificare `npx nx build landing` e `lint landing`. _Satisfies: REQ-1_
- [x] **T2** [Nx] Aggiungere con il generatore spartan i componenti `libs/ui` mancanti necessari (`badge`, `accordion`, `card`, eventuali altri richiesti dal frontend), poi `lint --fix` e prettier come da `doc/structure.md`. _Satisfies: REQ-6_
- [x] **T3** [Nx] Allineare la licenza: `package.json` `"license": "AGPL-3.0-only"` (altri manifest con licenza incoerente, se presenti). _Satisfies: REQ-2_

## Frontend

- [x] **T4** [Frontend] Struttura `src/app/landing/landing.component.ts` + `sections/*` (nav, hero, problemi, funzioni, galleria, diagramma, gratis-PMI, installazione, ISA-95, supporto, FAQ, CTA, footer), `landing-links.ts` (repo `https://github.com/illy-W/MicroMES`, script `https://get.micromes.example/install.sh`, contatto segnaposto), meta/OG/title, `robots.txt`, `sitemap.xml`, `404.html`, favicon. _Satisfies: REQ-1, REQ-6_
- [x] **T5** [Frontend] Contenuti IT: hero PMI + gratuito; sezione "Gratis" a 0 € con elenco incluso, self-hosted, AGPL-3.0 in una riga; "Serve una mano?" (consulenza/installazione a pagamento, opzionale); FAQ con accordion spartan (prezzo/trucco, destinatari, ERP/ISA-95, dati); funzioni "in arrivo" marcate correttamente (vista operatore, autenticazione); niente "OEE". _Satisfies: REQ-2, REQ-6_
- [x] **T6** [Frontend] Screenshot reali (>= 4, WebP 960/1600, < 250 kB): panoramica ordine, pianificazione/timeline, distinta o ciclo, ordini di lavoro o macchina. Catturare da `web` con DB seedato (`Api:db-up`, serve web+Api), `NgOptimizedImage`, `alt`, `width/height`, `srcset`, lazy tranne hero. Se la cattura non è possibile, riportare il blocco senza inventare immagini. _Satisfies: REQ-3_
- [x] **T7** [Frontend] Sezione diagramma ng-diagram sola lettura (nodi ordine di lavoro con badge stato, gruppo ordine figlio, dipendenze, percorso più lungo evidenziato), `@defer (on viewport)` + placeholder, nessun accesso a `window` in SSR, nodi non trascinabili, pan/zoom che non blocca lo scroll, elenco testuale equivalente. Consultare le skill `ng-diagram-*`. _Satisfies: REQ-4_
- [x] **T8** [Frontend] Sezione installazione: blocco codice con comando segnaposto, pulsante Copia accessibile (`aria-live` "Copiato"), nota "esempio non ancora pubblicato", 3 passi, requisiti Docker e `docker compose up -d`. _Satisfies: REQ-5_
- [x] **T9** [Frontend] Responsive 360 px, h1 unico e h2 per sezione, contrasto AA, focus visibile, scroll morbido con `prefers-reduced-motion`; icone lucide. Usare la skill `impeccable` (registro brand). Verificare `build`/`lint landing` e bundle iniziale. _Satisfies: REQ-6_
- [x] **T10** [Frontend] `Dockerfile` (nginx statico), `nginx.conf`, `security-headers.conf` adattati da ptconnect (senza riferimenti a ptconnect/Kubernetes), script CSP se necessario. _Satisfies: REQ-7_

## Docs (lead)

- [x] **T11** [Lead] Aggiornare `AGENTS.md` (tabella progetti, comandi) e `doc/structure.md` (sezione Landing). _Satisfies: REQ-7_

## Follow-up da critique/audit impeccable

- [x] **T12** [Frontend] Onestà installazione: mettere per primo il percorso che funziona (`docker compose up -d`, requisiti Docker), presentare lo script `curl | bash` come "in arrivo / esempio" (copia ancora possibile ma senza promessa), riallineare hero, CTA finale, nav e meta description ("Provalo in locale con Docker"); gestire il fallimento della clipboard (selezione testo + messaggio "Copia manualmente"). _Satisfies: REQ-5, REQ-2_
- [x] **T13** [Frontend] Bug: `touch-action` del diagramma (`pan-x pan-y`, pagina scorribile al tocco, verificare con tocco sintetizzato a 360 px); loader `NgOptimizedImage` senza `-undefined` (fallback a una larghezza reale) e `sizes` corretto per l'hero; target di tocco >= 44 px su mobile (nav, hero/CTA, Copia, accordion, link footer); hero screenshot leggibile su mobile; `role` sui nodi del diagramma; `motion-reduce` per l'accordion; skip link con target focusabile. _Satisfies: REQ-3, REQ-4, REQ-6_
- [x] **T14** [Frontend] Sezione "Guarda come funziona": sequenza di 3-4 passi con screenshot reali (ordine, rilascio, pianificazione, esecuzione), senza video né hosting, con didascalie e `alt`. _Satisfies: REQ-3, REQ-6_

## Palette

- [x] **T15** [Frontend] Rimuovere l'arancione (brand di ptconnect): eliminare `--signal`/`signal-foreground`, `--ring` e `--accent` ambrati, la tinta calda di sfondo/testi; usare i token neutri del preset spartan come `apps/web/src/styles.scss`; sostituire righe diagonali/tag basati su `signal` con soluzioni neutre (primary/border/muted). Colori semantici di stato del diagramma invariati; ricontrollare contrasti AA e il diagramma. _Satisfies: REQ-6_

## Seconda iterazione (critique/audit round 2)

- [x] **T16** [Frontend] Colore di carattere "industriale": un solo accento blu acciaio/segnale (non arancio/giallo/ambra), token `--signal` (+`--signal-foreground`, `--color-signal*`) in `styles.scss`; usato con parsimonia: righe diagonali, numeri dei passi, focus ring, percorso più lungo nel diagramma, bullet di stato. Contrasto AA su ogni coppia usata. _Satisfies: REQ-6_
- [x] **T17** [Frontend] Lightbox sulle immagini di prodotto con `@spartan-ng/helm/dialog` (componente condiviso `product-image`): click/Invio/Spazio, pulsante "Ingrandisci", Esc e pulsante Chiudi, focus restituito, immagine 1600 px con `alt` e didascalia, scroll interno se più grande del viewport; lazy (carica la 1600 solo all'apertura). _Satisfies: REQ-3_
- [x] **T18** [Frontend] Correzioni audit: regola `touch-action` scoped e commentata (ng-diagram) invece di `*` globale; link nav/footer/summary >= 24 px (meglio 32) su desktop; CSS di ng-diagram caricato solo con il chunk differito se possibile; hero con `<picture>` (una sola immagine scaricata); focus ring visibile su "GitHub" nav; link inline GitHub >= 44 px su mobile; Copia `min-h-10`. _Satisfies: REQ-4, REQ-6_
- [x] **T19** [Frontend] Correzioni critique: badge "in arrivo" leggibile sulla fascia scura; numerazione sezioni coerente (numerare tutte o nessuna); installazione: passo 0 "Installa Docker Desktop" (link), URL locale e cosa si vede al primo avvio; testo script non selezionabile; ridurre le ripetizioni (galleria "Schermate reali" senza le schermate già mostrate nei passi, tenendo ciò che aggiunge informazione); nav mobile con accesso a Installazione/Domande. _Satisfies: REQ-5, REQ-6_
