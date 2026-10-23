/**
 * URL, contatti e comandi della landing in un solo punto.
 * Quelli con dominio `.example` sono SEGNAPOSTO: sostituirli quando script e dominio esisteranno.
 */
export const REPO_URL = 'https://github.com/illy-W/MicroMES';

export const LANDING_LINKS = {
  site: 'https://micromes.example',
  repo: REPO_URL,
  license: `${REPO_URL}/blob/main/LICENSE`,
  issues: `${REPO_URL}/issues`,
  /** Segnaposto: lo script non è ancora pubblicato. */
  installScript: 'https://get.micromes.example/install.sh',
  installCommand: 'curl -fsSL https://get.micromes.example/install.sh | bash',
  dockerInstall: 'https://docs.docker.com/get-docker/',
  /** Indirizzo del frontend in sviluppo (`nx serve web`, porta 4300). */
  localUrl: 'http://localhost:4300',
  composeCommand: 'docker compose up -d',
  serveCommand: 'pnpm install && npx nx serve web',
  /** Segnaposto del contatto per consulenza, installazione e supporto. */
  contactEmail: 'ciao@micromes.example',
  contactHref: 'mailto:ciao@micromes.example',
} as const;

export const LICENSE_NAME = 'AGPL-3.0';
