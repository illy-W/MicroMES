# Spec: Rilascio e installazione (immagini, versioning, installer, aggiornamenti)

## 1. Context & Goals

Oggi MicroMES si avvia solo da sviluppatore (`docker compose up -d` avvia soltanto PostgreSQL, poi `pnpm install && npx nx serve web`). La landing promette un'installazione semplice, ma non esiste ancora: niente immagini, niente versioni, niente script. Obiettivo di questa spec: **preparare il terreno** perché un'installazione con un solo comando diventi vera, per due tipi di persona:

- **Utente finale** (titolare di una micro impresa, spesso senza competenze IT): installa su una macchina Linux e si aspetta che funzioni, sia aggiornabile senza rischi.
- **Consulente** (chi installa e mantiene più istanze per conto dei clienti): vuole ripetibilità, versioni fissabili, diagnosi rapida, backup e aggiornamenti controllati.

Punti di partenza verificati:

- Riferimento `ptconnect-nx`: workflow unico `build.yml` con GitVersion (versione dai messaggi di commit), job `version` -> `build` (test affected + publish) -> tre job `publish-*` (immagini su ghcr.io, tag `semver` + `latest`, firma cosign keyless) -> `update-env` che scrive il nuovo tag in un repo separato (`ptconnect-env`, kustomize, overlay `prod`). Funziona ma ha limiti per MicroMES: ogni push su `main` pubblica una versione (nessun concetto di "release" per l'utente), la versione dipende da token nei commit (`+semver:`), le tre immagini escono sempre insieme anche se cambia solo la landing, nessun changelog, solo `amd64`, e il deploy è un repo GitOps per un solo ambiente (non adatto a installazioni dei clienti).
- Backend: `Program.cs` applica le migration **solo in Development** (come i seeder); in produzione il database resterebbe senza schema. Decisione: le migration si applicano **automaticamente all'avvio in ogni ambiente**, come in `ptconnect-nx` (`DatabaseMigrationService`). La connection string è `ConnectionStrings:Default` (sovrascrivibile da variabile d'ambiente `ConnectionStrings__Default`). `apps/web` è una SPA (`outputPath dist/apps/web`) che oggi usa il proxy di sviluppo per `/api`.
- **Autenticazione non implementata** (`doc/product.md`): un'istanza raggiungibile in rete è aperta a chiunque. Va gestito esplicitamente (vedi REQ-9 e decisioni aperte).

### Proposta di design (da approvare)

| Tema | Proposta | Perché |
|---|---|---|
| Versioning | **Release Please** (manifest mode) con Conventional Commits (già usati: `feat(web)`, `fix(api)`...). Due componenti indipendenti: **`app`** (backend + frontend, path `apps/api`, `apps/web`, `libs/api-*`, `libs/ui`, `libs/shared`, `deploy/`) e **`landing`** (path `apps/landing`). Tag `app-vX.Y.Z` e `landing-vX.Y.Z`. | La release è un atto esplicito (si fonde la "Release PR", che contiene il changelog); BE e FE hanno una sola versione per costruzione; la landing non fa salire la versione dell'app; nessun token nei commit; niente repo GitOps. Alternativa scartata: GitVersion (come ptconnect) = versione implicita a ogni push, nessun changelog. Alternativa: `nx release` (nativo Nx, conventional commits, gruppi di progetti): valida, ma meno standard per GitHub Releases e Release PR. |
| Immagini | `micromes-api` e `micromes-web` con **stessa versione** (release `app`), `micromes-landing` con la propria. Tag: `X.Y.Z`, `X.Y`, `X`, `latest` solo alle release stabili; `edge` e `sha-<corto>` a ogni merge su `main` (per consulenti/test). Multi-arch `linux/amd64` + `linux/arm64`, SBOM, firma cosign keyless, pacchetti pubblici su ghcr.io. | `arm64` serve per mini PC/Raspberry in piccole officine; `edge` separa test da produzione. |
| Chart | Un solo **Helm chart `micromes`** (api + web + postgres opzionale + ingress), con `version == appVersion`, pubblicato come OCI su `ghcr.io/illy-w/charts/micromes`. | L'unità di rilascio è il chart: un comando installa/aggiorna/torna indietro; usabile anche da chi ha già k8s/Argo/Flux. |
| Installer | `install.sh` POSIX (shellcheck-clean) nel repo in `deploy/install/`, scaricato **direttamente da GitHub** (nessun dominio): ultima stabile `https://raw.githubusercontent.com/illy-W/MicroMES/stable/deploy/install/install.sh` (il branch/tag mobile `stable` avanza a ogni release `app`), versione fissa `.../app-vX.Y.Z/deploy/install/install.sh`. Installa **k3s** se manca un cluster, **helm**, poi `helm upgrade --install`. Idempotente: rilanciarlo = aggiornare. Installa anche il comando `micromes`. | Un solo punto d'ingresso per utenti e consulenti; k3s include Traefik e storage locale, perfetti per un nodo singolo. |
| Aggiornamenti | **Fase 1 (questa spec)**: nessun auto-update; l'app mostra un avviso "Disponibile vX.Y.Z" (con note di rilascio e il comando da lanciare) controllando le GitHub Release; l'aggiornamento si fa con `micromes upgrade` (o rilanciando lo script), con backup automatico prima della migration. **Pulsante "Aggiorna" nell'app: rinviato** a dopo l'autenticazione. | Un pulsante nell'app richiede che l'API possa modificare il cluster (permessi RBAC molto estesi) e, senza login, chiunque in LAN potrebbe usarlo. Notificare è sicuro e sufficiente ora. |
| DB esterno | **Sì**: `postgresql.mode: bundled` (default, StatefulSet `postgres:17-alpine` con PVC e backup giornaliero) oppure `external` (connection string da Secret). Installer: `--db-url` o `--db-secret`. Test di connessione prima di installare. | Molti consulenti hanno già un PostgreSQL gestito/da backuppare; separare il dato dal cluster semplifica backup e ripristino. |
| Semplicità | Comando `micromes`: `status`, `upgrade [--version]`, `backup`, `restore`, `doctor`, `support-bundle`, `uninstall`; configurazione in un solo `values.yaml` (`/etc/micromes/values.yaml`) versionabile dal consulente; default sensati (HTTP in LAN, nessuna domanda). | Stesso flusso per tutti; il consulente aggiunge solo `values.yaml` e `--version`. |

## 2. Requirements

### REQ-1: Immagini container (backend, frontend, landing)
- **User Story**: As a manutentore, I want che GitHub costruisca e pubblichi le immagini di API, web e landing so that nessuno compili a mano.
- **Acceptance Criteria**:
  - [ ] Esistono i Dockerfile `apps/api/Dockerfile` (runtime ASP.NET, utente non root, porta 8080), `apps/web/Dockerfile` (nginx statico con fallback SPA e security headers) e `apps/landing/Dockerfile` (già presente, da verificare con build reale).
  - [ ] Le tre immagini sono pubblicate su `ghcr.io/illy-w/micromes/{api,web,landing}` in `linux/amd64` e `linux/arm64`, con tag come in tabella, SBOM e firma cosign keyless; pacchetti pubblici.
  - [ ] Le immagini si costruiscono anche in locale con un target Nx (`npx nx run <progetto>:docker`).
  - [ ] `web` non contiene indirizzi hardcoded: `/api` è instradato dall'ingress (nessun proxy nel frontend in produzione).
- **Technical Constraints**: build degli artefatti una volta in CI e riuso (come ptconnect) con cache buildx `type=gha`; versione passata come build-arg e come `InformationalVersion` dell'API; la landing non entra nelle immagini dell'app.

### REQ-2: Versioning e release
- **User Story**: As a maintainer, I want rilasci espliciti e numerati so that utenti e consulenti sappiano cosa installano e cosa cambia.
- **Acceptance Criteria**:
  - [ ] Release Please configurato (`release-please-config.json`, manifest) con componenti `app` e `landing`; le modifiche a BE o FE portano alla stessa Release PR `app`; le modifiche solo a `apps/landing` alla Release PR `landing`.
  - [ ] Fondere la Release PR crea tag `app-vX.Y.Z` / `landing-vX.Y.Z`, la GitHub Release con changelog, e fa partire la pubblicazione di immagini e chart con quella versione.
  - [ ] Ogni merge su `main` pubblica `edge`/`sha-*` (mai `latest`).
  - [ ] Regole di bump documentate in `doc/` (feat = minor, fix = patch, `!`/`BREAKING CHANGE` = major; fino alla 1.0 un breaking incrementa il minor) e verificate da un controllo dei commit (commitlint o equivalente) sulle PR.
  - [ ] La versione installata è leggibile da `GET /api/version` (versione, commit, data build) e mostrata nell'interfaccia (piè della sidebar).
- **Technical Constraints**: Conventional Commits già in uso nel repository; versione unica per tutte le immagini `api`/`web`/chart dell'app; nessuna versione calcolata da token nei messaggi.

### REQ-3: Pipeline GitHub Actions
- **User Story**: As a maintainer, I want pipeline veloci e affidabili so that ogni PR e ogni release siano verificate.
- **Acceptance Criteria**:
  - [ ] Workflow `ci` su PR: `nx affected -t lint build unit-test` (test d'integrazione backend solo se toccato il backend, Docker disponibile sul runner), commitlint, shellcheck dello script, `helm lint` + `helm template` del chart, build (senza push) delle immagini toccate.
  - [ ] Controlli di PR specifici del repository: **contratto API aggiornato** (rigenera con `api-client:generate-api` e fallisce se `git diff` non è vuoto), **migration mancanti** (`dotnet ef migrations has-pending-model-changes`), formattazione (prettier, `dotnet format --verify-no-changes`), `actionlint` sui workflow, budget del bundle di `web` e `landing`.
  - [ ] **Code coverage** del backend (unit + integrazione, `coverlet.collector` già presente) caricata su Codecov (`codecov/codecov-action@v5`, `CODECOV_TOKEN` da secret; su PR da fork senza secret l'upload non deve far fallire la CI); baseline su ogni push a `main`; i check `codecov/project` e `codecov/patch` sono di Codecov e non entrano in `ci-ok`.
  - [ ] Controlli di convenzione: `commitlint` su ogni commit della PR e verifica che il titolo della PR sia un Conventional Commit.
  - [ ] Controlli di sicurezza: CodeQL (C# e JavaScript/TypeScript), `dependency-review-action` (vulnerabilità e licenze compatibili con AGPL-3.0), scansione segreti (`gitleaks`), Dependabot.
  - [ ] Un unico check obbligatorio **`ci-ok`** (job di sintesi con `if: always()` che dipende da tutti gli altri e tratta i job saltati dai filtri sui percorsi come riusciti): è l'unico nome richiesto dai ruleset.
  - [ ] Workflow `release-app` e `release-landing`: test, build, push immagini, push chart OCI, firma, avanzamento del branch `stable` solo a e2e verde.
  - [ ] Workflow `e2e-install` (su PR che toccano `deploy/` e a ogni release): installa il chart con `k3d`/`kind` e `install.sh`, controlla `GET /api/version`, esegue un upgrade da N-1 a N e una migration.
  - [ ] Concurrency per branch, cache pnpm/NuGet/buildx, permessi minimi per job (`packages: write`, `id-token: write` solo dove servono).
- **Technical Constraints**: `.nvmrc` e `global.json` come fonte delle versioni; azioni fissate per versione/SHA; nessuna pipeline di deploy verso clienti (non siamo noi a installare).

### REQ-4: Helm chart `micromes`
- **User Story**: As a consulente, I want un chart unico so that installare, aggiornare e tornare indietro sia un comando.
- **Acceptance Criteria**:
  - [ ] `deploy/helm/micromes` con: Deployment api e web (probe di liveness/readiness, risorse e `securityContext` restrittivi), Service, Ingress (path `/api` -> api, `/` -> web), Secret per la connection string, NetworkPolicy opzionale, `values.yaml` documentato.
  - [ ] PostgreSQL incluso (StatefulSet `postgres:17-alpine`, PVC, password generata, CronJob `pg_dump` giornaliero con retention) oppure esterno (REQ-7).
  - [ ] Le migration sono applicate dall'API all'avvio (nessun Job né hook). `helm upgrade` fa un rolling update: se la migration della nuova versione fallisce il nuovo pod non diventa pronto e i pod della versione precedente restano in servizio; `helm rollback` ripristina i manifest (il dato si ripristina con `micromes restore`).
  - [ ] Funziona su k3s (Traefik, local-path) e su un k8s generico (`ingress.className` configurabile).
- **Technical Constraints**: nessuna dipendenza da chart di terze parti (evitare subchart Bitnami); `version == appVersion` del release `app`; sicurezza: pod non root, filesystem in sola lettura dove possibile.

### REQ-5: Script di installazione
- **User Story**: As a utente o consulente, I want un solo comando so that su una macchina Linux ottengo MicroMES funzionante.
- **Acceptance Criteria**:
  - [ ] `curl -fsSL https://raw.githubusercontent.com/illy-W/MicroMES/stable/deploy/install/install.sh | sh` su Debian/Ubuntu/RHEL-like (amd64/arm64): verifica requisiti (root/sudo, RAM >= 2 GB, spazio, porte 80/443 libere), installa k3s se non c'è un cluster raggiungibile, installa helm, installa il chart, attende i pod pronti.
  - [ ] Opzioni: `--version`, `--namespace`, `--domain` (+ `--email` per Let's Encrypt), `--db-url`/`--db-secret`, `--values <file>`, `--demo-data`, `--kubeconfig/--context` (cluster esistente: salta k3s), `--yes`, `--dry-run`, `--uninstall`.
  - [ ] Idempotente: rilanciarlo aggiorna senza perdere dati. Mai distruttivo senza `--yes`/conferma esplicita.
  - [ ] Al termine stampa: URL, un **avviso esplicito** che l'applicazione non ha ancora autenticazione e va esposta solo su rete fidata, versione installata, comandi utili (`micromes status|upgrade|backup`).
  - [ ] Verifica integrità: checksum e firma cosign del chart/immagini quando `cosign` è presente, altrimenti avviso.
  - [ ] Testato in CI su k3d/kind (REQ-3) e provato su una VM Ubuntu pulita.
- **Technical Constraints**: POSIX `sh`, nessun pacchetto Python/Node richiesto, stampa messaggi in italiano, log in `/var/log/micromes-install.log`; lo script è un file del repository (`deploy/install/install.sh`), nessun hosting separato; il branch `stable` è aggiornato dalla pipeline `release-app` solo dopo che l'e2e di installazione passa.

### REQ-6: Aggiornamenti dopo l'installazione
- **User Story**: As a utente, I want sapere quando c'è una nuova versione e aggiornare in modo sicuro so that resto aggiornato senza rischi.
- **Acceptance Criteria**:
  - [ ] L'API controlla (al massimo una volta al giorno, disattivabile con `updates.check=false`) la GitHub Release `app` più recente; `GET /api/version` espone `latest` e `updateAvailable`; nessun dato dell'utente esce dall'istanza (solo una GET anonima).
  - [ ] L'interfaccia mostra un avviso discreto "Disponibile vX.Y.Z" con link alle note di rilascio e il comando `micromes upgrade`.
  - [ ] `micromes upgrade [--version X]` esegue: backup del database, `helm upgrade`, attesa pronto, test di salute, e in caso di errore ripristina la versione precedente e spiega cosa è successo.
  - [ ] Nessun aggiornamento automatico: ogni upgrade è una scelta esplicita (un'app gestionale con migration non si aggiorna da sola). L'auto-update dei soli patch è rinviato a una spec futura.
  - [ ] Il pulsante "Aggiorna" nell'app **non** è incluso (rinviato dopo l'autenticazione).
- **Technical Constraints**: l'API non riceve permessi di modifica sul cluster; compatibilità: le migration devono essere compatibili con la versione N-1 dell'app almeno per un upgrade interrotto (documentare la regola "migration additive").

### REQ-7: Database PostgreSQL esterno
- **User Story**: As a consulente, I want usare un PostgreSQL fuori da Kubernetes so that backup, alta affidabilità e dati restano sotto il mio controllo.
- **Acceptance Criteria**:
  - [ ] `values.yaml`: `postgresql.mode: external` con `existingSecret` o `connectionString`; supportato `--db-url` nello script.
  - [ ] Pre-flight: job/comando che verifica raggiungibilità, credenziali, versione >= 17 e permessi DDL, con messaggio chiaro in italiano in caso di errore.
  - [ ] Con DB esterno non viene creato lo StatefulSet né il CronJob di backup; `micromes backup` esegue `pg_dump` verso un percorso locale e avvisa che il backup completo è responsabilità del DB.
  - [ ] Documentati requisiti (versione, estensioni: nessuna richiesta, SSL, utente con diritto di creare tabelle) e passaggio da bundled a esterno (dump/restore).
- **Technical Constraints**: la connection string è passata come `ConnectionStrings__Default` da Secret; l'API migra con la stessa connessione (utente con diritti DDL).

### REQ-8: Flusso semplice per utenti e consulenti
- **User Story**: As a consulente, I want strumenti uniformi so that installo e assisto molti clienti con lo stesso metodo; as a utente, I want pochi passi e messaggi chiari.
- **Acceptance Criteria**:
  - [ ] Comando `micromes` installato dallo script: `status` (versione, stato pod, DB, spazio disco, ultima backup), `upgrade`, `backup`, `restore <file>`, `doctor` (diagnosi con esito per controllo e suggerimento), `support-bundle` (archivio con log e configurazione **senza segreti**), `version`, `uninstall`.
  - [ ] Configurazione in un solo file (`/etc/micromes/values.yaml`) che il consulente può tenere in un repository; versioni fissabili (`--version`), nessuna dipendenza da servizi nostri oltre ghcr.io e GitHub.
  - [ ] Pagina di documentazione "Installazione e aggiornamenti" (landing `/docs/install` o `doc/`) con due percorsi: *Utente* (1 comando) e *Consulente* (DB esterno, dominio, versione fissa, più istanze con `--namespace`, backup/ripristino, troubleshooting).
  - [ ] L'installazione con i default non fa domande; ogni scelta non banale ha un'opzione esplicita.
- **Technical Constraints**: messaggi in italiano; codici di uscita significativi; nessuna telemetria.

### REQ-9: Prontezza del backend per la produzione
- **User Story**: As a consulente, I want che l'API funzioni in produzione so that l'installazione non dipenda da trucchi manuali.
- **Acceptance Criteria**:
  - [ ] **Migration automatiche all'avvio in ogni ambiente** (chiave `Database:AutoMigrate`, predefinita `true`; disattivabile). I seeder restano solo Development o `Demo__Seed`. `GET /health/ready` risponde 503 finché le migration non sono applicate (non solo finché il database risponde). Il comando `Api.dll migrate` resta **opzionale** (consulenti con DB esterno e utente senza diritti DDL, pre-flight); il chart non lo usa.
  - [ ] Endpoint `GET /health/live` e `/health/ready` (ready controlla il database); `GET /api/version` (REQ-2/6); log su stdout in formato JSON in produzione.
  - [ ] I seeder restano solo Development; `--demo-data` (opzionale) esegue un seeder dedicato esplicitamente richiesto (variabile `Demo__Seed=true`), mai di default.

  - [ ] **Nessuna autenticazione per ora** (decisione): l'installer e la documentazione avvisano che l'istanza va tenuta su rete fidata (LAN/VPN); l'ingress espone di default le porte del nodo, con opzione `--basic-auth` (disattivata) per chi vuole una protezione temporanea.
  - [ ] Nessuna dipendenza da file locali: tutto lo stato è nel database.
- **Technical Constraints**: pattern backend di `doc/structure.md` (nessun `DbContext` nel codice di prodotto: il codice di migration all'avvio e il comando `migrate` condividono lo stesso componente di avvio previsto dalle regole); nuovi endpoint documentati e contratto OpenAPI rigenerato (`npx nx run api-client:generate-api`).

### REQ-10: Allineamento landing e documentazione
- **User Story**: As a visitatore, I want istruzioni vere so that il comando mostrato nella landing funzioni.
- **Acceptance Criteria**:
  - [ ] `landing-links.ts` punta all'URL raw GitHub dello script (`.../stable/deploy/install/install.sh`); la sezione "Installazione" mostra il comando unico come principale e `docker compose` solo come sviluppo (rimosso il badge "in arrivo"); `get.micromes.example` sparisce (`micromes.example` resta finché non c'è un dominio per canonical/sitemap).
  - [ ] `AGENTS.md` e `doc/structure.md` documentano release, immagini, chart, script e comandi (`doc/release.md` dedicato).
  - [ ] La landing mostra anche la variante a versione fissata.
- **Technical Constraints**: questo requisito si chiude solo dopo REQ-1..9 verificati su una VM reale.

### REQ-11: Branching e repository aperto alla collaborazione
- **User Story**: As a contributor esterno o consulente, I want regole chiare e un flusso semplice so that posso proporre modifiche senza rompere i rilasci.
- **Acceptance Criteria**:
  - [ ] Modello **trunk-based**: un solo ramo di sviluppo `main` (sempre rilasciabile) più il ramo `stable` gestito solo dalla pipeline; nessun `develop`. Le modifiche arrivano da rami brevi `feat/<tema>`, `fix/<tema>`, `docs/…`, `chore/…` (da fork per i contributori esterni) tramite pull request. Metodi di merge ammessi: **rebase merge** (PR dei maintainer con commit atomici già in formato Conventional Commit, es. `feat(api)`, `feat(web)`, `docs(specs)`: il changelog mantiene il dettaglio per layer) e **squash merge** (PR dei contributori esterni: il titolo della PR, un Conventional Commit, diventa il commit). Il **merge commit è disattivato**: la storia di `main` resta lineare. Release Please legge ogni commit su `main`, quindi ogni commit deve essere valido. Rami `release/app-X.Y` solo se servirà correggere una versione vecchia (non ora).
  - [ ] Impostazioni del repository (ruleset) applicate e documentate in `doc/release.md` con uno script `deploy/github/apply-settings.sh` (`gh api`, idempotente, eseguito da un amministratore): solo squash e rebase merge (squash con titolo PR come messaggio), controllo `commitlint` obbligatorio su **ogni commit** della PR e sul titolo, rami head eliminati dopo il merge, `main` protetto (PR obbligatoria, check `ci-ok` obbligatorio e ramo aggiornato, 1 approvazione con code owner, conversazioni risolte, no force-push, no cancellazione, storia lineare), `stable` scrivibile solo dal bot di release, tag `app-v*`/`landing-v*` creabili solo dalla pipeline, GitHub Actions con `GITHUB_TOKEN` in sola lettura di default e approvazione obbligatoria per i workflow dei contributori alla prima PR.
  - [ ] File di comunità: `CONTRIBUTING.md` (setup in 5 comandi, naming dei rami, Conventional Commits, come eseguire test e build), `CODE_OF_CONDUCT.md`, `SECURITY.md` (segnalazioni private), `CODEOWNERS`, template di PR (checklist: titolo conventional, test, doc) e di issue (bug, funzione, domanda -> Discussions), `dependabot.yml` (NuGet, npm/pnpm, GitHub Actions, Docker), secret scanning e dependency graph attivi.
  - [ ] **Contributi e licenza**: `CONTRIBUTING.md` spiega che il codice è AGPL-3.0; decisione tra DCO (sign-off, semplice) e CLA (consente di cambiare licenza o offrire una licenza commerciale in futuro), con raccomandazione CLA via GitHub App se si vuole tenere aperta la doppia licenza.
  - [ ] Il token usato da Release Please è un **GitHub App** (o PAT dedicato), non `GITHUB_TOKEN`: le PR create con `GITHUB_TOKEN` non avviano i workflow di CI, quindi la Release PR non verrebbe verificata.
- **Technical Constraints**: le impostazioni sul repository richiedono diritti di amministratore: l'agente prepara file e script, l'applicazione la fa il proprietario; nessun segreto necessario per le PR da fork (la CI non usa secret, la pubblicazione avviene solo da `main`/tag).

## 3. Layers impacted

- **Backend**: migration automatiche all'avvio (comando `migrate` opzionale), health check, `GET /api/version` con controllo aggiornamenti, log JSON, seeder demo esplicito, ricostruzione del contratto OpenAPI.
- **Web**: versione nel piè della sidebar, avviso "aggiornamento disponibile" (nessun pulsante di aggiornamento); eventuale pagina informazioni.
- **Repository GitHub**: ruleset, CODEOWNERS, template, Dependabot, documentazione per i contributori (REQ-11).
- **Nx workspace / infrastruttura**: Dockerfile api/web, target Nx `docker`, workflow GitHub Actions, Release Please, chart Helm, `install.sh` e CLI `micromes`, test e2e dell'installazione, aggiornamenti a `landing` (link allo script, docs), `AGENTS.md`/`doc/`. (Lavoro d'infrastruttura assegnato a `[Nx]` in assenza di un agente DevOps dedicato.)

## 4. Impact Assessment

- File/moduli: `.github/workflows/*`, `release-please-config.json`, `.release-please-manifest.json`, `commitlint.config.*`, `apps/{api,web}/Dockerfile`, `apps/api/Program.cs` (+ nuovi file health/version/migrate), `libs/api-features/*` (feature `SystemFeature`/`VersionFeature`), `apps/web` (sidebar, banner), `deploy/helm/micromes/**`, `deploy/install/{install.sh,micromes}`, `apps/landing` (docs, link allo script), `AGENTS.md`, `doc/structure.md`, `doc/release.md`.
- Breaking changes: **no** per l'utente; **sì per il flusso di sviluppo**: Conventional Commits diventano obbligatori sulle PR (già prassi); i tag git `app-v*` e `landing-v*` sono nuovi.
- Rischi:
  - assenza di autenticazione (decisione: nessuna auth per ora; mitigata da avviso nell'installer, documentazione e opzione `--basic-auth` opt-in);
  - migration non retro-compatibili che bloccano l'upgrade (mitigato da backup automatico e dal test e2e N-1 -> N);
  - script `curl | sh` (mitigato da checksum/cosign, versioni fissabili e sorgente leggibile);
  - costo dei build multi-arch su GitHub Actions (mitigato da cache e build solo alle release);
  - ghcr.io pubblico: nessun segreto nelle immagini.
- Glossario: nessuna nuova entità di dominio.

## 5. Decisioni prese

1. **Versioning: Release Please** (vedi spiegazione in chat): release esplicite con Release PR e changelog, componenti `app` (BE+FE) e `landing`.
2. **Autenticazione**: nessuna per ora; avviso e opzione `--basic-auth` opt-in.
3. **Aggiornamenti**: solo manuali (`micromes upgrade`) con avviso di nuova versione; niente auto-update, niente pulsante nell'app.
4. **Script**: ospitato nel repository GitHub (raw su branch `stable` o tag `app-vX.Y.Z`), nessun dominio necessario. La landing mostra quel comando.
5. **TLS**: HTTP in LAN di default; `--domain` + `--email` per Let's Encrypt.
6. **`--demo-data`**: incluso come opzione esplicita.
7. **Docker Compose** come terza via: fuori scopo.
8. **Registry**: ghcr.io pubblico sotto `illy-W`; `image.registry` configurabile.
9. **Branching**: trunk-based (`main` + `stable` automatico); PR con rebase merge (maintainer, commit atomici) o squash merge (esterni); merge commit disattivato; commitlint su ogni commit (REQ-11).
