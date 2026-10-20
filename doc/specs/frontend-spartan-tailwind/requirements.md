# Spec: Frontend con spartan-ng e Tailwind

## 1. Context & Goals

`apps/web` oggi è l'app Angular generata da Nx, senza libreria UI. Come in `ptconnect-nx`, il frontend deve basarsi su **spartan-ng** (componenti shadcn-style generati in `libs/ui/*`, importati con l'alias `@spartan-ng/helm/*`) e **Tailwind CSS v4**. L'obiettivo è predisporre le dipendenze, la configurazione e l'infrastruttura, mantenendo funzionanti `serve` e `build`.

Riferimento: `ptconnect-nx` (`components.json`, `.postcssrc.json`, `styles.scss`, `libs/ui/*`, alias in `tsconfig.base.json`, `doc/structure.md` §UI Styling Conventions).

## 2. Requirements

### REQ-1: Dipendenze e configurazione Tailwind v4

- **User Story**: As a sviluppatore frontend, I want Tailwind v4 configurato in `web` so that posso usare le utility e i token di spartan.
- **Acceptance Criteria**:
  - [ ] Installate (versioni esatte, come `save-exact`) le dipendenze runtime necessarie: `@angular/cdk`, `@spartan-ng/brain`, `@ng-icons/core`, `@ng-icons/lucide`, `class-variance-authority`, `clsx`, `tailwind-merge`, `tailwindcss`, `tw-animate-css`, `@tanstack/angular-table` (per le tabelle, stessa versione di ptconnect-nx); e dev: `@tailwindcss/postcss`, `@spartan-ng/cli`, `postcss`.
  - [ ] `apps/web/.postcssrc.json` usa `@tailwindcss/postcss` (come ptconnect-nx).
  - [ ] Le versioni sono allineate a quelle di `ptconnect-nx` e compatibili con Angular e Node del workspace (`package.json`, `.nvmrc`).
  - [ ] `engine-strict` non fallisce con le nuove dipendenze.
- **Technical Constraints**: Angular 22 e Node 24.15.0; nessuna libreria UI diversa da spartan; nessun PrimeNG.

### REQ-2: Stili globali e design token

- **User Story**: As a sviluppatore, I want gli stili globali con il preset spartan so that i componenti abbiano i token semantici.
- **Acceptance Criteria**:
  - [ ] `apps/web/src/styles.scss` importa `tailwindcss` (layer theme/base/utilities) e `@spartan-ng/brain/hlm-tailwind-preset.css`, come ptconnect-nx.
  - [ ] Definiti i token semantici (`--background`, `--foreground`, `--primary`, `--border`, `--ring`, `--radius`, ecc.) per tema chiaro e scuro (`:root.dark`).
  - [ ] Tipografia di base per h1–h6, `p`, `label` come ptconnect-nx; nessun colore del brand specifico (`--brand`) né font aggiuntivi: da decidere in seguito.
  - [ ] I token sono usabili tramite classi Tailwind (`bg-primary`, `text-muted-foreground`, `border`).
- **Technical Constraints**: `styles.scss` resta in SCSS (stile del progetto); la mappatura `@theme inline` segue il preset spartan.

### REQ-3: Infrastruttura librerie UI `libs/ui`

- **User Story**: As a sviluppatore, I want `libs/ui` e il generatore spartan configurati so that posso aggiungere componenti helm con un comando.
- **Acceptance Criteria**:
  - [ ] `components.json` nella root con `componentsPath: libs/ui`, `buildable: true`, `generateAs: library`, `importAlias: @spartan-ng/helm`, stile coerente con ptconnect-nx (`nova`).
  - [ ] Il generatore `@spartan-ng/cli` funziona nel workspace e crea ogni componente come libreria Nx in `libs/ui/<nome>` con alias in `tsconfig.base.json` (path con prefisso `./`, senza `baseUrl`).
  - [ ] Installato almeno il componente `button` come verifica dell'intera pipeline (generazione, alias, build, lint).
  - [ ] `nx.json` ha i `targetDefaults` per `@nx/angular:ng-packagr-lite` (cache, dependsOn `^build`) e le regole lint `enforceBuildableLibDependency` se necessarie per le librerie buildable.
  - [ ] Documentato come aggiungere nuovi componenti.
- **Technical Constraints**: i componenti generati non si scrivono a mano: si usa `@spartan-ng/cli`; le librerie seguono il modello di ptconnect-nx (`project.json` con `prefix: hlm`, `ng-package.json`, `tsconfig.lib*.json`).

### REQ-4: Uso in `web` e `serve` funzionante

- **User Story**: As a sviluppatore, I want che `nx serve web` continui a funzionare con spartan e Tailwind so that posso sviluppare normalmente.
- **Acceptance Criteria**:
  - [ ] `apps/web` usa il componente `button` (`hlmBtn`) e classi Tailwind in `app.html` al posto del contenuto di benvenuto Nx, come prova visibile; `nx-welcome.ts` rimosso (sparisce anche il warning di budget SCSS).
  - [ ] `npx nx build web` e `npx nx lint web` passano; `npx nx run-many -t build lint` passa su tutti i progetti.
  - [ ] `nx serve web` parte, compila senza errori e la pagina servita applica gli stili Tailwind/spartan (verifica con una richiesta HTTP al dev server e controllo del CSS servito, oppure screenshot).
  - [ ] Il dev server ha il proxy `/api` e la dipendenza `Api:serve` già presenti (nessuna regressione).
  - [ ] `dependsOn: ["Api:serve"]` resta a livello di target `serve` (Nx non permette `dependsOn` per configurazione): `nx serve web` usa `development` come default e continua ad avviare l'API. `serve` è solo per lo sviluppo.
- **Technical Constraints**: standalone, OnPush, signals; nessun uso di `dark:`/colori hardcoded nelle classi; icone con `@ng-icons/lucide`; l'avvio di `Api:serve` è continuo: la verifica del serve va fatta in background e chiusa al termine.

### REQ-5: Prettier, agent e documentazione

- **User Story**: As a team, I want che formattazione, agent e documenti riflettano lo stack UI so that gli agent seguano le regole spartan.
- **Acceptance Criteria**:
  - [ ] `.prettierrc` supporta le classi Tailwind (`tailwindFunctions: ["hlm", "cva", "classes"]`); se serve il plugin `prettier-plugin-tailwindcss`, valutato e documentato; nessun altro plugin Prettier viene aggiunto senza necessità.
  - [ ] `frontend.md` riporta le regole UI (priorità: componenti spartan helm → token semantici → `styles.scss` → utility Tailwind di layout; niente colori hardcoded né `dark:`; icone lucide; nuovi componenti con il generatore `@spartan-ng/cli`; Tailwind v4 richiede `.postcssrc.json` nell'app).
  - [ ] Installate le skill `angular-developer` e `spartan` per `frontend` (dal riferimento: `angular/skills` e `spartan-ng/spartan`) se disponibili e raggiungibili; altrimenti segnalato.
  - [ ] `doc/structure.md` ha una sezione "UI Styling Conventions" e la struttura di `libs/ui`; `AGENTS.md` aggiorna tabella e comandi.
- **Technical Constraints**: i file in `.opencode/agents/**`, `AGENTS.md` e le skill sono modificati da un agent con permessi di scrittura (il lead modifica solo `doc/**`).

## 3. Layers impacted

- **Backend**: nessuno.
- **Web**: `apps/web` (stili, `.postcssrc.json`, `app.html`, `app.ts`, `project.json`), `libs/ui/*` (nuove librerie).
- **Nx workspace**: `package.json`, `pnpm-lock.yaml`, `tsconfig.base.json`, `nx.json`, `components.json`, `eslint.config.mjs`, `.prettierrc`, skill, agent, documentazione.

## 4. Impact Assessment

- **Affected Files/Modules**: `package.json`, `pnpm-lock.yaml`, `components.json`, `tsconfig.base.json`, `nx.json`, `eslint.config.mjs`, `.prettierrc`, `apps/web/{.postcssrc.json,project.json,src/styles.scss,src/app/*}`, `libs/ui/button/**`, `.opencode/agents/frontend.md`, `.agents/skills/**`, `skills-lock.json`, `AGENTS.md`, `doc/structure.md`.
- **Breaking Changes**: No. Il contenuto di benvenuto Nx viene sostituito.

## 5. Decisioni (confermate)

TanStack Table (`@tanstack/angular-table`) è l'unica dipendenza extra rispetto alla proposta iniziale: serve per le tabelle. Solo installazione, nessun componente tabella in questa spec.

1. **Set di componenti** (confermato): installare solo `button` ora; gli altri si aggiungono con il generatore quando servono (ptconnect-nx ne ha 62, non servono).
2. **Dipendenze escluse** (non servono ora): NgRx, ngx-translate, Supabase, Stripe, Playwright, Vitest, `@fontsource-variable/inter`, brand e animazioni custom del riferimento.
3. **Stile spartan**: `nova` come ptconnect-nx, con tema chiaro/scuro neutro (token del preset, senza `--brand`).
4. **SSR**: non attivato (il riferimento lo usa, ma `web` è una SPA); nessuna modifica di `build`.
5. **Test**: nessun test unitario per ora (nessun runner configurato nel workspace).

## 6. Rischi

- Le versioni delle dipendenze di spartan e Angular 22 possono avere peer dependency non allineate: si usano le versioni di ptconnect-nx e si verifica `pnpm install` con `engine-strict`.
- Il generatore `@spartan-ng/cli` può aggiungere `baseUrl` o path senza `./`: si controlla `tsconfig.base.json` dopo ogni generazione (TS 6 e esbuild).
- Skill da GitHub: la rete ha già bloccato i template di `create-nx-workspace`; l'installazione delle skill può fallire e va segnalata.
- La verifica di `nx serve web` avvia anche `Api:serve` (`dotnet watch`): va eseguita in background e fermata.
