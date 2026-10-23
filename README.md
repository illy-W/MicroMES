# MicroMES

MES (Manufacturing Execution System) pensato per le **micro imprese manifatturiere** (1-20 persone): gratuito, open source con licenza **AGPL-3.0**, con un vocabolario di dominio ISA-95 in forma ridotta (vedi [`doc/glossary.md`](doc/glossary.md)).

Per visione e obiettivi: [`doc/product.md`](doc/product.md).

> **Stato:** progetto in sviluppo. Non c'è ancora autenticazione: usalo solo su reti fidate. Script di installazione e immagini pronte non sono ancora rilasciati; per ora si prova da sorgente.

## Avvio rapido (locale)

### Requisiti

- Node.js come in [`.nvmrc`](.nvmrc) (v24.15.0)
- pnpm 10.7.1 (versione in `package.json`): `corepack enable`
- .NET SDK come in [`global.json`](global.json) (10.0.201)
- Docker (per il database PostgreSQL)

### Passi

```bash
git clone https://github.com/illy-W/MicroMES.git
cd MicroMES
corepack enable
pnpm install
docker compose up -d        # oppure: npx nx run Api:db-up
npx nx serve web
```

`nx serve web` avvia anche l'API. Frontend su <http://localhost:4300>, API su <http://localhost:5190>.

### Al primo avvio

In ambiente Development l'API applica le migration e, se il database è vuoto, carica dati di esempio di un'officina di mobili in acciaio e rovere. Al riavvio i dati non vengono duplicati.

### Azzerare i dati

```bash
docker compose down -v
docker compose up -d
```

### Problemi comuni

- **Porte occupate** (4300 web, 5190 API, 5432 PostgreSQL): libera la porta o ferma il processo che la usa.
- **Docker non in esecuzione**: avvia Docker Desktop (o il daemon) e rilancia `docker compose up -d`.
- **Database non avviato**: `/health/ready` risponde 503 e l'API riprova a connettersi ogni 10 s; avvia il database e attendi.

## Comandi utili

```bash
npx nx run-many -t build                 # build di tutti i progetti
npx nx affected -t lint build            # lint e build dei soli progetti toccati
npx nx run ApiTests:unit-test            # test unitari backend
npx nx run ApiTests.Integration:unit-test # test di integrazione (richiede Docker)
npx nx serve landing                     # landing page su http://localhost:4400
npx nx run api-client:generate-api       # rigenera openapi/Api.json e il client Angular
npx nx run Api:db-up / Api:db-down       # avvia/ferma il database locale
```

## Struttura del monorepo

Monorepo Nx (pnpm) con frontend Angular e backend .NET.

| Percorso | Descrizione |
|---|---|
| `apps/web` | Frontend Angular |
| `apps/api` | API ASP.NET Core |
| `apps/landing` | Landing page pubblica (SSR + prerender) |
| `libs/api-features`, `libs/api-core` | Librerie .NET (`Api` → `ApiFeatures` → `ApiCore`) |
| `libs/api-tests`, `libs/api-tests-integration` | Test .NET unitari e di integrazione |
| `libs/ui/*` | Componenti spartan helm (generati) |
| `libs/shared/api-client` | Client API generato con Orval |
| `doc/` | Prodotto, struttura, glossario, specifiche |

Dettagli e convenzioni in [`AGENTS.md`](AGENTS.md) e [`doc/structure.md`](doc/structure.md).

## Contribuire

- Usa i [Conventional Commits](https://www.conventionalcommits.org/lang/it/).
- Le feature non banali seguono un flusso spec-driven (requisiti → task → esecuzione → verifica), descritto in [`AGENTS.md`](AGENTS.md); le specifiche stanno in [`doc/specs`](doc/specs).

## Licenza

[AGPL-3.0](LICENSE).
