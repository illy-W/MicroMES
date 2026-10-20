# Implementation Plan: Frontend con spartan-ng e Tailwind

## Technical Strategy

Replicare l'approccio di ptconnect-nx in modo minimale: Tailwind v4 via `@tailwindcss/postcss` (`.postcssrc.json` in `apps/web`), preset `@spartan-ng/brain` in `styles.scss`, componenti helm generati da `@spartan-ng/cli` come librerie Nx in `libs/ui/*` (solo `button`), alias `@spartan-ng/helm/*` in `tsconfig.base.json` (con prefisso `./`, senza `baseUrl`). TanStack Table viene solo installato. `web` resta una SPA senza SSR.

Ordine: `[Nx]` (dipendenze, configurazione, generatore, skill, agent e docs), poi `[Frontend]` (app e verifica del serve). Nessun task `[Backend]`.

I file `.opencode/agents/*.md`, `AGENTS.md` e le skill (task 4.x) sono fuori dal perimetro di `feature-lead` (solo `doc/**`): li esegue `nx-expert`. Il task 4.4 modifica `doc/structure.md` ed è assegnato a `nx-expert` per coerenza con gli altri.

## Tasks

- [x] 1. **Nx workspace: dipendenze e configurazione**
  - [x] 1.1 [Nx] Installare con versioni esatte, allineate a `ptconnect-nx` e compatibili con Angular 22 e Node 24.15.0: runtime `@angular/cdk`, `@spartan-ng/brain`, `@ng-icons/core`, `@ng-icons/lucide`, `class-variance-authority`, `clsx`, `tailwind-merge`, `tailwindcss`, `tw-animate-css`, `@tanstack/angular-table`; dev `@tailwindcss/postcss`, `@spartan-ng/cli`, `postcss`. Verificare `pnpm install` con `engine-strict`.
    - _Satisfies: REQ-1_
  - [x] 1.2 [Nx] Creare `apps/web/.postcssrc.json` con `@tailwindcss/postcss` (come ptconnect-nx).
    - _Satisfies: REQ-1_
  - [x] 1.3 [Nx] Creare `components.json` nella root (`componentsPath: libs/ui`, `buildable: true`, `generateAs: library`, `importAlias: @spartan-ng/helm`, `style: nova`).
    - _Satisfies: REQ-3_
  - [x] 1.4 [Nx] Aggiungere in `nx.json` i `targetDefaults` per `@nx/angular:ng-packagr-lite` (cache, `dependsOn: ["^build"]`, inputs production/^production) e, in `eslint.config.mjs`, `enforceBuildableLibDependency` se le librerie buildable lo richiedono.
    - _Satisfies: REQ-3_
  - [x] 1.5 [Nx] Generare il componente `button` con `@spartan-ng/cli` (`npx nx g @spartan-ng/cli:ui button`) in `libs/ui/button`. Controllare `tsconfig.base.json`: alias `@spartan-ng/helm/button` con prefisso `./`, nessun `baseUrl`. Verificare `nx build button` e `nx lint button`.
    - _Satisfies: REQ-3_
  - [x] 1.6 [Nx] Aggiornare `.prettierrc` con `tailwindFunctions: ["hlm", "cva", "classes"]`; aggiungere `prettier-plugin-tailwindcss` solo se necessario e documentare la scelta.
    - _Satisfies: REQ-5_
- [x] 2. **Frontend: stili e app**
  - [x] 2.1 [Frontend] Riscrivere `apps/web/src/styles.scss` come in ptconnect-nx, senza `--brand` e senza font aggiuntivi: layer theme/base/utilities, import di `tailwindcss` e `@spartan-ng/brain/hlm-tailwind-preset.css`, token semantici chiaro e `:root.dark`, tipografia di base, `@apply` su bordo e sfondo.
    - _Satisfies: REQ-2_
  - [x] 2.2 [Frontend] Sostituire il contenuto di benvenuto Nx con una pagina che usa `hlmBtn` (da `@spartan-ng/helm/button`), classi Tailwind semantiche e un'icona `@ng-icons/lucide` (`provideIcons`). Rimuovere `nx-welcome.ts`. Standalone, OnPush, signals; niente colori hardcoded né `dark:`.
    - _Satisfies: REQ-4_
  - [x] 2.3 [Frontend] `dependsOn: ["Api:serve"]` resta a livello di target `serve`: Nx non supporta `dependsOn` per configurazione. Requisito aggiornato di conseguenza.
    - _Satisfies: REQ-4_
  - [x] 2.4 [Frontend] Verificare: `npx nx build web`, `npx nx lint web`, nessun warning di budget SCSS da `nx-welcome.ts`. Avviare `npx nx serve web` in background (parte anche `Api:serve`), controllare che compili senza errori, che `GET http://localhost:4200/` risponda e che il CSS servito contenga le utility Tailwind e i token; poi fermare i processi (web e `dotnet watch`).
    - _Satisfies: REQ-2, REQ-4_
- [x] 3. **Skill**
  - [x] 3.1 [Nx] Installare le skill `angular-developer` (`angular/skills`) e `spartan` (`spartan-ng/spartan`) a livello di progetto con `npx skills add ... -a opencode -y`, aggiornando `skills-lock.json`. Se la rete le blocca, riportarlo senza ripiegare su file scritti a mano.
    - _Satisfies: REQ-5_
- [x] 4. **Agent e documentazione**
  - [x] 4.1 [Nx] Aggiornare `.opencode/agents/frontend.md`: stack (Angular, spartan-ng, Tailwind v4, TanStack Table), regole UI (priorità componenti helm → token semantici → `styles.scss` → utility Tailwind di layout; niente colori hardcoded né `dark:`; icone lucide; nuovi componenti con `@spartan-ng/cli`, mai a mano; `.postcssrc.json` per ogni app Tailwind), skill disponibili (`angular-developer`, `spartan`).
    - _Satisfies: REQ-5_
  - [x] 4.2 [Nx] Aggiornare `.opencode/agents/nx-expert.md`: conosce `libs/ui/*`, `components.json` e il generatore spartan; i nuovi componenti si generano, non si scrivono.
    - _Satisfies: REQ-5_
  - [x] 4.3 [Nx] Aggiornare `AGENTS.md`: `libs/ui/*` nella tabella della struttura e il comando per aggiungere un componente spartan.
    - _Satisfies: REQ-5_
  - [x] 4.4 [Nx] Aggiornare `doc/structure.md`: sezione "UI Styling Conventions" (priorità, token, icone, Tailwind v4 e `.postcssrc.json`), struttura di `libs/ui`, come aggiungere un componente, TanStack Table installato per le tabelle.
    - _Satisfies: REQ-3, REQ-5_
- [x] 5. **Verifica**
  - [x] 5.1 `npx nx run-many -t build lint` passa su tutti i progetti, incluso `button`.
    - _Satisfies: REQ-3, REQ-4_
  - [x] 5.2 Controllo dei criteri di accettazione di `requirements.md` rispetto ai report degli agent, e `git status` coerente (nessun file generato inatteso, nessun `baseUrl`).
    - _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5_
