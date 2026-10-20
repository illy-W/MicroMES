# Spec: Layout blank e full con sidebar

## 1. Context & Goals

`apps/web` mostra una pagina di prova. Come in `ptconnect-nx` (`core/layouts/{blank,full}`) servono due layout:

- **blank**: contiene solo il `router-outlet`. Al momento nessuna route lo usa, perché non c'è autenticazione.
- **full**: shell con sidebar (`hlm-sidebar`, variante `inset`), header con il trigger della sidebar e area contenuto con il `router-outlet`. La sidebar mostra una lista di voci di menu; per ora solo "Dashboard".

La pagina Dashboard è vuota e usa il componente spartan `hlm-empty`.

Riferimento: `ptconnect-nx/apps/web-spartan/src/app/core/layouts` (`full`, `blank`, `components/{sidebar,header,nav-main}`, `models/nav-item.model.ts`, `full/sidebar-data.ts`) e `app.routes.ts`.

## 2. Requirements

### REQ-1: Componenti spartan necessari

- **User Story**: As a sviluppatore frontend, I want i componenti helm richiesti dal layout so that posso costruire sidebar e pagina vuota con la UI standard.
- **Acceptance Criteria**:
  - [ ] Generati con `@spartan-ng/cli` in `libs/ui`: `sidebar`, `empty` e le dipendenze richieste dal generatore (dal riferimento: `separator`, `sheet`, `skeleton`, `tooltip`, `input`; `button` e `utils` esistono già).
  - [ ] Alias `@spartan-ng/helm/<nome>` in `tsconfig.base.json` con prefisso `./`, senza `baseUrl`.
  - [ ] Ogni nuova libreria passa `nx build` e `nx lint` (con `lint --fix` per i peer, come da flusso in `AGENTS.md`).
- **Technical Constraints**: nessun componente scritto a mano; si installano solo quelli richiesti dal generatore per `sidebar` ed `empty`.

### REQ-2: Layout `blank`

- **User Story**: As a sviluppatore, I want un layout senza elementi di shell so that le pagine future (es. login) possano usarlo.
- **Acceptance Criteria**:
  - [ ] Esiste `BlankLayoutComponent` (selector `app-blank-layout`) in `apps/web/src/app/core/layouts/blank/` che rende solo `<router-outlet />`.
  - [ ] Standalone, OnPush, caricabile con `loadComponent`.
  - [ ] Nessuna route lo usa al momento; non è referenziato dal routing.
- **Technical Constraints**: nessuna logica di autenticazione.

### REQ-3: Layout `full` con sidebar

- **User Story**: As a utente, I want una shell con sidebar e header so that posso navigare tra le sezioni dell'app.
- **Acceptance Criteria**:
  - [ ] Esiste `FullLayoutComponent` (selector `app-full-layout`) in `apps/web/src/app/core/layouts/full/` con `hlm-sidebar` variante `inset`, header (trigger della sidebar + separatore verticale) e `router-outlet` nell'area contenuto (`hlmSidebarInset`), come nel riferimento.
  - [ ] La sidebar ha header con il nome dell'app ("MicroMES", senza logo) e il contenuto con la lista delle voci di menu (`NavMain`); nessun footer utente e nessun blocco aggiuntivo (nav-user, nav-secondary, nav-trainer).
  - [ ] Le voci sono definite in un file dati (`sidebar-data.ts`) tipizzato con `NavItem` (`id`, `displayName`, `iconName`, `route`): attualmente solo `{ id: 'dashboard', displayName: 'Dashboard', iconName: 'lucideHouse', route: '/dashboard' }`.
  - [ ] La voce attiva è evidenziata (`routerLinkActive`/stato attivo del `hlmSidebarMenuButton`).
  - [ ] Su schermi piccoli la sidebar si apre come sheet tramite il trigger.
  - [ ] Componenti standalone, OnPush, signals (`input()`); icone con `@ng-icons/lucide` (`NgIcon` + `provideIcons`).
- **Technical Constraints**: nessun i18n (`ngx-translate` non è installato: etichette in italiano, in chiaro); nessun ruolo, feature flag, badge né sotto-menu: il modello `NavItem` resta minimale e si estende quando servono; niente breadcrumb nel header; solo classi semantiche (niente colori hardcoded né `dark:`).

### REQ-4: Routing e pagina Dashboard

- **User Story**: As a utente, I want atterrare sulla Dashboard all'apertura dell'app so that vedo la shell completa.
- **Acceptance Criteria**:
  - [ ] `app.routes.ts`: route `''` con `loadComponent` di `FullLayoutComponent` e figli: `''` → redirect a `dashboard` (`pathMatch: 'full'`) e `dashboard` → pagina dashboard (lazy, `loadComponent`).
  - [ ] La pagina `DashboardComponent` (in `apps/web/src/app/features/dashboard/`) mostra `hlm-empty` con icona, titolo e descrizione brevi (etichette in italiano, in chiaro: "Dashboard" / "Nessun contenuto al momento"), senza dati né chiamate HTTP.
  - [ ] `app.ts`/`app.html` tornano a contenere solo il `router-outlet` (la pagina di prova del precedente setup viene rimossa).
  - [ ] `nx build web` e `nx lint web` passano; `nx serve web` mostra sidebar + Dashboard su `/` e `/dashboard`.
- **Technical Constraints**: route lazy; nessuna guard; nessun uso del client API generato (nessun endpoint).

### REQ-5: Agent e documentazione

- **User Story**: As a team, I want che la struttura dei layout sia documentata so that gli agent seguano lo stesso pattern.
- **Acceptance Criteria**:
  - [ ] `doc/structure.md` descrive: cartella `core/layouts` (`blank`, `full`, `components`, `models`), come aggiungere una voce di menu (`sidebar-data.ts`), come assegnare un layout a una route, la regola che `blank` serve per le pagine senza shell.
  - [ ] `frontend.md` rimanda alla sezione e alla regola: nuove pagine sotto un layout, voci di menu in `sidebar-data.ts`.
- **Technical Constraints**: modifiche a `.opencode/agents/**` eseguite da un agent con permessi di scrittura.

## 3. Layers impacted

- **Backend**: nessuno.
- **Web**: `apps/web/src/app` (layout, dashboard, routes, app root), `libs/ui/*` (nuovi componenti).
- **Nx workspace**: `tsconfig.base.json`, `package.json` (peer dei componenti generati), nuove librerie `libs/ui/*`.

## 4. Impact Assessment

- **Affected Files/Modules**: `libs/ui/{sidebar,empty,separator,sheet,skeleton,tooltip,input}`, `tsconfig.base.json`, `apps/web/src/app/{app.ts,app.html,app.routes.ts}`, `apps/web/src/app/core/layouts/**`, `apps/web/src/app/features/dashboard/**`, `doc/structure.md`, `.opencode/agents/frontend.md`.
- **Breaking Changes**: No. Sostituisce la pagina di prova.

## 5. Decisioni (confermate)

1. **Nome del layout**: `blank`, come nel riferimento.
2. **Percorsi**: `core/layouts/{blank,full,components,models}` e `features/dashboard`.
3. **Modello minimale**: `NavItem` senza ruoli, feature flag, badge o sotto-menu.
4. **Header**: solo trigger della sidebar e separatore; breadcrumb rimandato.
5. **Sidebar**: nome app "MicroMES" senza logo; nessun footer utente.
6. **Lingua UI**: italiano, con etichette in chiaro finché non si decide l'i18n.

## 6. Rischi

- Il generatore di `sidebar` può richiedere altre librerie (`sheet`, `tooltip`, `skeleton`, `input`, `separator`): si installano solo quelle richieste e si controllano gli alias con `./`.
- `hlm-sidebar` usa `@spartan-ng/brain/tooltip` e il servizio di stato della sidebar: verificare che funzioni con `provideRouter` e senza SSR.
- Il lint può segnalare peer dependencies mancanti nei `package.json` generati: si risolve con `nx lint <nome> --fix`.
