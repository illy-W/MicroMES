# Implementation Plan: Layout blank e full con sidebar

## Technical Strategy

Replicare in forma minimale `core/layouts` di ptconnect-nx: `blank` (solo `router-outlet`) e `full` (`hlm-sidebar` variante `inset`, header con trigger e separatore, `router-outlet` dentro `hlmSidebarInset`). Le voci di menu stanno in `sidebar-data.ts` (tipo `NavItem` minimale) e sono mostrate da `NavMain`. La pagina Dashboard, lazy, usa `hlm-empty`. Etichette in italiano, in chiaro (nessun i18n). Nessun backend e nessun uso del client API.

Ordine: `[Nx]` (componenti spartan con il generatore), poi `[Frontend]` (layout, pagina, routing, verifica del serve), infine `[Nx]` per agent e documentazione (il lead modifica solo `doc/**`; `frontend.md` e `doc/structure.md` sono assegnati a `nx-expert`).

## Tasks

- [x] 1. **Nx workspace: componenti spartan**
  - [x] 1.1 [Nx] Generare con `npx nx g @spartan-ng/cli:ui <nome> --no-interactive` i componenti `sidebar` ed `empty`, più le dipendenze che il generatore richiede (attese: `separator`, `sheet`, `skeleton`, `tooltip`, `input`). Per ciascuno: `npx nx lint <nome> --fix`, Prettier sui JSON riscritti (`nx.json`, `tsconfig.base.json`) e controllo degli alias `@spartan-ng/helm/<nome>` (prefisso `./`, nessun `baseUrl`).
    - _Satisfies: REQ-1_
  - [x] 1.2 [Nx] Verificare `npx nx run-many -t build lint` su tutti i progetti, incluse le nuove librerie. Riportare l'elenco finale dei componenti e degli alias.
    - _Satisfies: REQ-1_
- [x] 2. **Frontend: layout, pagina e routing**
  - [x] 2.1 [Frontend] Creare `core/layouts/blank/` con `BlankLayoutComponent` (`app-blank-layout`, standalone, OnPush, template `<router-outlet />`).
    - _Satisfies: REQ-2_
  - [x] 2.2 [Frontend] Creare `core/layouts/models/nav-item.model.ts` (`NavItem`: `id`, `displayName`, `iconName`, `route`) e `core/layouts/full/sidebar-data.ts` con la sola voce Dashboard (`lucideHouse`, `/dashboard`).
    - _Satisfies: REQ-3_
  - [x] 2.3 [Frontend] Creare `core/layouts/components/sidebar` (`hlm-sidebar` `inset`, header "MicroMES" senza logo, contenuto con `NavMain`, nessun footer), `components/nav-main` (lista con `hlmSidebarMenu`, `routerLink`, icona `NgIcon` + `provideIcons`, voce attiva evidenziata) e `components/header` (`hlmSidebarTrigger` + `hlm-separator` verticale, nessun breadcrumb). Standalone, OnPush, `input()`; solo classi semantiche.
    - _Satisfies: REQ-3_
  - [x] 2.4 [Frontend] Creare `core/layouts/full/` con `FullLayoutComponent` (`app-full-layout`): sidebar con le voci di `sidebar-data.ts`, `<main hlmSidebarInset>` con header e `router-outlet`; sidebar come sheet su schermi piccoli.
    - _Satisfies: REQ-3_
  - [x] 2.5 [Frontend] Creare `features/dashboard/` con `DashboardComponent` (standalone, OnPush) che mostra `hlm-empty` con icona lucide, titolo "Dashboard" e descrizione "Nessun contenuto al momento"; nessun dato né HTTP.
    - _Satisfies: REQ-4_
  - [x] 2.6 [Frontend] Aggiornare `app.routes.ts`: route `''` con `loadComponent` di `FullLayoutComponent` e figli (`''` → redirect `dashboard` con `pathMatch: 'full'`; `dashboard` → `loadComponent` di `DashboardComponent`). Ridurre `app.ts`/`app.html` al solo `router-outlet` e rimuovere la pagina di prova. `BlankLayoutComponent` non è referenziato dal routing.
    - _Satisfies: REQ-2, REQ-4_
  - [x] 2.7 [Frontend] Verificare `npx nx build web` e `npx nx lint web`. Avviare `npx nx serve web` in background (parte anche `Api:serve`): nessun errore di compilazione, `GET /` e `GET /dashboard` rispondono, il bundle contiene i selector dei layout; se disponibile uno screenshot con gli strumenti del browser (sidebar visibile, voce Dashboard attiva, `hlm-empty`). Poi fermare web e `dotnet watch`.
    - _Satisfies: REQ-3, REQ-4_
- [x] 3. **Agent e documentazione**
  - [x] 3.1 [Nx] Aggiornare `doc/structure.md`: `core/layouts` (`blank`, `full`, `components`, `models`), come aggiungere una voce di menu (`sidebar-data.ts`), come assegnare un layout a una route, `blank` per le pagine senza shell, pagina Dashboard con `hlm-empty`. Non toccare le altre sezioni.
    - _Satisfies: REQ-5_
  - [x] 3.2 [Nx] Aggiornare `.opencode/agents/frontend.md`: nuove pagine sotto un layout, voci di menu in `sidebar-data.ts`, rimando alla sezione di `doc/structure.md`. Mantenere stile e lingua del file.
    - _Satisfies: REQ-5_
- [x] 4. **Verifica**
  - [x] 4.1 `npx nx run-many -t build lint` passa su tutti i progetti.
    - _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4_
  - [x] 4.2 Controllo dei criteri di accettazione di `requirements.md` rispetto ai report degli agent; `git status` coerente (nessun `baseUrl`, alias con `./`, nessun file generato inatteso).
    - _Satisfies: REQ-1, REQ-2, REQ-3, REQ-4, REQ-5_
