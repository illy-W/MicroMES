# Tasks: Rilascio e installazione

Stato: requisiti APPROVATI. Task in attesa di approvazione. Owner: `[Nx]` = infrastruttura (nx-expert), `[Backend]`, `[Frontend]`, `[Owner]` = azioni che richiedono i diritti di amministratore del repository (le esegue il proprietario).

## Technical Strategy

- Cinque fasi consegnabili e verificabili da sole; ogni fase termina con `npx nx affected -t lint build unit-test` verde e un commit.
- Ordine: Backend (prontezza produzione) -> Nx (immagini, release, chart, installer, CI) -> Frontend (versione/avviso) -> Landing/docs -> Repo aperto.
- Cartelle: `deploy/helm/micromes`, `deploy/install/{install.sh,micromes}`, `deploy/github/apply-settings.sh`, `.github/{workflows,ISSUE_TEMPLATE,CODEOWNERS,dependabot.yml,pull_request_template.md}`, `doc/release.md`.
- Nomi: immagini `ghcr.io/illy-w/micromes/{api,web,landing}`, chart `oci://ghcr.io/illy-w/charts/micromes`, tag `app-vX.Y.Z` / `landing-vX.Y.Z`.
- Contratto API: i task backend che cambiano l'API terminano con la rigenerazione (`npx nx run api-client:generate-api`) prima del frontend.

## Fase 1: Backend pronto per la produzione, immagini, CI di base

### Backend
- [x] **B1** [Backend] Comando `migrate` (resta come strumento opzionale; vedi B6) (argomento `migrate` di `Api.dll`: applica le migration ed esce 0/1, usa i servizi di avvio esistenti nel rispetto delle regole sul `DbContext`); invariato il comportamento in Development; test d'integrazione (PostgreSQL) del comando su DB vuoto e su DB già migrato. _Satisfies: REQ-9, REQ-4_
- [x] **B2** [Backend] Health: `GET /health/live` e `/health/ready` (ready controlla il database), esclusi dal contratto pubblico o documentati; log JSON su stdout fuori da Development; configurazione `ConnectionStrings__Default` da ambiente; test. _Satisfies: REQ-9_
- [x] **B3** [Backend] `GET /api/version` (versione da `InformationalVersion`, commit, data build; `latest` e `updateAvailable` dalla GitHub Release `app` più recente, cache 24 h, `Updates:Check=false` per disattivare, errori di rete silenziosi con stato `unknown`); feature in `libs/api-features` con service e test (rete simulata). _Satisfies: REQ-2, REQ-6_
- [x] **B4** [Backend] Seeder demo esplicito: `Demo__Seed=true` esegue i seeder esistenti anche fuori da Development, mai di default, solo su DB vuoto; test. _Satisfies: REQ-5, REQ-9_
- [x] **B6** [Backend] Migration automatiche all'avvio in ogni ambiente (`Database:AutoMigrate`, predefinita `true`; Development invariato, seeder solo Development o `Demo__Seed`); `/health/ready` 503 finché le migration non sono applicate; test (avvio su DB vuoto, già migrato, AutoMigrate=false, readiness prima/dopo); il comando `migrate` resta. Se cambia il contratto, rigenerarlo. _Satisfies: REQ-9, REQ-4_
- [x] **B5** [Backend] Rigenerare il contratto (`npx nx run api-client:generate-api`) e riportare le modifiche. _Satisfies: REQ-6_

### Nx
- [x] **N1** [Nx] `apps/api/Dockerfile` (runtime aspnet, non root, porta 8080, `HEALTHCHECK`/probe-friendly, versione via build-arg), `apps/web/Dockerfile` + `nginx.conf` (SPA fallback, cache asset, security headers, nessun proxy `/api`), verifica build reale di api, web e landing in locale. _Satisfies: REQ-1_
- [x] **N2** [Nx] Target Nx `docker` per api, web, landing (`npx nx run <progetto>:docker`), con tag locale e build-arg di versione. _Satisfies: REQ-1_
- [x] **N3** [Nx] Workflow `ci.yml` su PR: `nx affected -t lint build unit-test` (integrazione backend solo se toccato il backend), commitlint di ogni commit della PR e verifica del titolo PR, controllo contratto API aggiornato (`generate-api --skip-nx-cache` + `git diff --exit-code`: la cache Nx può restituire un `openapi/Api.json` vecchio), controllo migration mancanti (`dotnet ef migrations has-pending-model-changes`), formattazione (prettier, `dotnet format --verify-no-changes`), `actionlint`, budget bundle web e landing, CodeQL, `dependency-review-action`, `gitleaks`, build senza push delle immagini toccate, concurrency e cache, permessi minimi; job di sintesi `ci-ok` (`if: always()`, job saltati = ok) come unico check richiesto. _Satisfies: REQ-3_

### Frontend
- [x] **F1** [Frontend] Versione installata nel piè della sidebar e avviso discreto "Disponibile vX.Y.Z" (link note di rilascio + comando `micromes upgrade` copiabile), dismiss per versione; nessun pulsante di aggiornamento; solo client generato. _Satisfies: REQ-2, REQ-6_

## Fase 2: Versioning e pubblicazione

- [ ] **N4** [Nx] Release Please: `release-please-config.json` e `.release-please-manifest.json` con componenti `app` (apps/api, apps/web, libs/api-*, libs/ui, libs/shared, deploy) e `landing` (apps/landing), tag `app-v*`/`landing-v*`, changelog; regola pre-1.0 documentata; workflow `release-please.yml` con token di GitHub App/PAT (non `GITHUB_TOKEN`). _Satisfies: REQ-2, REQ-11_
- [ ] **N5** [Nx] Workflow `publish-edge.yml` (merge su `main`): immagini `api`/`web`/`landing` solo se toccate, tag `edge` + `sha-*`, multi-arch amd64/arm64, SBOM, cosign keyless, mai `latest`. _Satisfies: REQ-1, REQ-3_
- [ ] **N6** [Nx] Workflow `release-app.yml` (tag `app-v*`): test, immagini `api`/`web` con tag `X.Y.Z`, `X.Y`, `X`, `latest`, push chart OCI, firma, avanzamento del branch `stable` solo dopo e2e verde (N12); `release-landing.yml` (tag `landing-v*`) per l'immagine landing. Versione iniettata come build-arg/`InformationalVersion`. _Satisfies: REQ-1, REQ-2, REQ-3_
- [ ] **N7** [Nx] Pacchetti ghcr.io resi pubblici (documentare i passi manuali) e verifica `cosign verify`. _Satisfies: REQ-1_

## Fase 3: Helm chart

- [ ] **N8** [Nx] `deploy/helm/micromes`: Deployment/Service api e web (probe, risorse, securityContext), Ingress (`/api` -> api, `/` -> web, `className` configurabile), Secret connection string, nessun job di migration (l'API migra all'avvio; readiness solo a schema aggiornato), NetworkPolicy opzionale, `values.yaml` e `values.schema.json` documentati. _Satisfies: REQ-4_
- [ ] **N9** [Nx] PostgreSQL integrato (StatefulSet `postgres:17-alpine`, PVC, password generata e conservata nel Secret tra gli upgrade, CronJob `pg_dump` giornaliero con retention) e modalità `external` (existingSecret/connectionString, pre-flight di connessione, versione >= 17, permessi DDL, messaggi chiari). _Satisfies: REQ-4, REQ-7_
- [ ] **N10** [Nx] Test del chart: `helm lint`, `helm template` (modalità bundled/external/demo) in CI come job di `ci-ok`, `version == appVersion`, nessun subchart di terze parti. _Satisfies: REQ-4, REQ-3_

## Fase 4: Installer e comando `micromes`

- [ ] **N11** [Nx] `deploy/install/install.sh` (POSIX, shellcheck-clean, italiano, log in `/var/log/micromes-install.log`): requisiti (root/sudo, RAM >= 2 GB, spazio, porte), installa k3s se manca un cluster, helm, chart, attesa pronto; opzioni `--version --namespace --domain --email --db-url --db-secret --values --demo-data --kubeconfig/--context --basic-auth --yes --dry-run --uninstall`; idempotente; avviso esplicito "nessuna autenticazione: rete fidata"; riepilogo finale con URL e comandi; checksum/cosign quando disponibili. _Satisfies: REQ-5, REQ-9_
- [ ] **N12** [Nx] Comando `deploy/install/micromes` (installato dallo script): `status`, `upgrade [--version]` (backup, `helm upgrade`, attesa, health check, rollback automatico con spiegazione), `backup`, `restore <file>`, `doctor`, `support-bundle` (senza segreti), `version`, `uninstall`; configurazione in `/etc/micromes/values.yaml`. _Satisfies: REQ-6, REQ-8, REQ-7_
- [ ] **N13** [Nx] Workflow `e2e-install.yml` (PR che toccano `deploy/` e release): k3d/kind, `install.sh` da checkout, controllo `GET /api/version`, upgrade N-1 -> N con migration, DB esterno, `--demo-data`; shellcheck dello script; il job conta in `ci-ok` (saltato se `deploy/` non è toccato). _Satisfies: REQ-3, REQ-5_
- [ ] **N14** [Nx] Prova guidata su VM Ubuntu pulita (istruzioni e checklist per il proprietario, risultato riportato nel report). _Satisfies: REQ-5_

## Fase 5: Landing e documentazione

- [ ] **F2** [Frontend] Landing: `landing-links.ts` con URL raw `stable` e variante a versione fissata, sezione installazione con comando unico (`curl | sh`) come principale, rimozione "in arrivo", avviso "rete fidata", requisiti (Linux, 2 GB RAM), spiegazione aggiornamenti e DB esterno, link alla documentazione; hero, CTA e meta description riallineati. _Satisfies: REQ-10_
- [ ] **N15** [Nx] `doc/release.md` (versioning, rilascio, canali edge/stable, installazione, aggiornamenti, DB esterno, percorsi Utente e Consulente, troubleshooting), aggiornare `AGENTS.md` e `doc/structure.md` (immagini, chart, script, comandi, flusso release); pagina "Installazione e aggiornamenti" sulla landing. _Satisfies: REQ-8, REQ-10_

## Fase 6: Repository aperto alla collaborazione

- [ ] **N16** [Nx] `CONTRIBUTING.md` (setup, branching trunk-based, quando usare rebase o squash merge, Conventional Commits, test, licenza AGPL e decisione DCO/CLA), `CODE_OF_CONDUCT.md`, `SECURITY.md`, `.github/CODEOWNERS`, template PR e issue, `dependabot.yml` (NuGet, npm, github-actions, docker). _Satisfies: REQ-11_
- [ ] **N17** [Nx] `deploy/github/apply-settings.sh` (gh api, idempotente): squash e rebase merge (merge commit disattivato), commitlint su ogni commit come check obbligatorio, auto-delete head branches, ruleset `main`/`stable`/tag (check richiesto: `ci-ok`; approvazione code owner attivabile quando ci sono altri contributori), Actions in sola lettura e approvazione dei workflow dei nuovi contributori, secret scanning, Discussions; istruzioni per GitHub App di Release Please. _Satisfies: REQ-11_
- [ ] **O1** [Owner] Eseguire `apply-settings.sh` con `gh auth` da amministratore, creare il GitHub App/PAT del bot di release, rendere pubblici i pacchetti ghcr.io, scegliere DCO o CLA. _Satisfies: REQ-11, REQ-1_
