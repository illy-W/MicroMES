# Rilascio e pubblicazione

Prima versione (fase 2 di `doc/specs/release-and-install`). Sarà completata da N15 (installazione, aggiornamenti, troubleshooting).

## Modello di versioni

- Due componenti con versione propria, gestiti da Release Please (modalità manifest, `release-type: simple`):
  - `app` (backend, web, chart): tutto il repository **tranne** `apps/landing`. Versione in `version.txt`, tag `app-vX.Y.Z`.
  - `landing`: `apps/landing`. Versione in `apps/landing/version.txt`, tag `landing-vX.Y.Z`.
- Le immagini `api` e `web` hanno sempre la stessa versione `app`. La versione non è scritta in `csproj`/`package.json`: la build la inietta (`VERSION` build-arg, `-p:InformationalVersion` per l'API, quindi `GET /api/version`).
- Limite: Release Please attribuisce un commit a un componente in base ai file toccati. `app` è la radice `.` con `exclude-paths: apps/landing`, perché un componente non può elencare più cartelle. Conseguenza: anche commit solo di `doc/`, `.github/` o root (tipi `feat`/`fix`) possono generare una release `app`; un commit che tocca sia landing sia il resto conta per entrambi.

## Convenzioni dei commit e bump

Conventional Commits (verificati da commitlint su ogni commit e sul titolo della PR).

| Commit | Prima della 1.0 (ora) | Dopo la 1.0 |
|---|---|---|
| `fix:` / `perf:` | patch | patch |
| `feat:` | minor | minor |
| `feat!:` / `BREAKING CHANGE:` | **minor** | major |

Configurazione: `bump-minor-pre-major: true`, `bump-patch-for-minor-pre-major: false`. Si parte da `0.0.0`: il primo `feat` produce `0.1.0`. `docs`, `refactor`, `test`, `build`, `ci`, `chore` non fanno rilasciare e sono nascosti dal changelog.

## Flusso di rilascio

1. Merge su `main` -> Release Please aggiorna la Release PR (`app` e/o `landing`); in parallelo `publish-edge.yml` pubblica `edge` e `sha-<short>` delle sole immagini toccate (mai `latest`).
2. Si fonde la Release PR -> Release Please crea tag e GitHub Release con il changelog.
3. Il tag avvia `release-app.yml` (test unit e integrazione, immagini `api`+`web` con tag `X.Y.Z`, `X.Y`, `X`, `latest`, chart OCI se presente, firma) oppure `release-landing.yml` (immagine landing, tag `X.Y.Z` e `latest`).
4. Solo a pubblicazione riuscita (e, dalla fase 4, `e2e-install` verde) il branch `stable` viene portato al commit del tag (mai arretrato).

Canali: `edge` = ultimo `main` (può rompersi); `stable` + tag `latest`/semver = ultima release.

Immagini: `ghcr.io/illy-w/micromes/{api,web,landing}` (multi-arch amd64/arm64, SBOM, provenance, firma cosign keyless). Chart: `oci://ghcr.io/illy-w/charts/micromes` (fase 3).

## Segreti e GitHub App

- `RELEASE_APP_ID`, `RELEASE_APP_PRIVATE_KEY`: App di release (non `GITHUB_TOKEN`, altrimenti Release PR e tag non avviano altri workflow). Permessi del repository: Contents (RW), Pull requests (RW), Metadata (R). Deve poter scrivere su `stable` e creare i tag `app-v*`/`landing-v*` (bypass dei ruleset).
- `CODECOV_TOKEN`: usato da `coverage.yml` (upload coverage backend, N18); non necessario per la pubblicazione.

## Verifica delle firme

```sh
cosign verify ghcr.io/illy-w/micromes/api:X.Y.Z \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com \
  --certificate-identity-regexp '^https://github.com/illy-W/MicroMES/\.github/workflows/.+@.+$'
```

Lo stesso vale per `web`, `landing` e il chart. Il job summary di ogni run riporta i digest.

## Passi manuali del proprietario (task O1)

1. Creare la GitHub App di release, installarla sul repository e aggiungere i secret `RELEASE_APP_ID` e `RELEASE_APP_PRIVATE_KEY` (Settings > Secrets and variables > Actions). Aggiungere anche `CODECOV_TOKEN`.
2. Consentire all'App di creare i tag e di scrivere su `stable` nei ruleset; creare il branch `stable` se la protezione lo richiede (viene creato dal workflow se assente).
3. Settings > Actions > General: permessi workflow del `GITHUB_TOKEN`; dopo il primo push di ogni immagine, in ghcr.io (Packages > `micromes/api`, `web`, `landing`, e `charts/micromes`) > Package settings: **Change visibility -> Public** e **Connect repository** (illy-W/MicroMES, da Package settings o dal link nel Dockerfile `org.opencontainers.image.source`). I pacchetti nascono privati.
4. Verificare con `cosign verify` (sopra) e `docker pull` anonimo.
