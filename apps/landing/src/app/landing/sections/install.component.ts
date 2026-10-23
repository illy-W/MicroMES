import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck, lucideCopy } from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { LANDING_LINKS } from '../../landing-links';

@Component({
  selector: 'app-install',
  imports: [NgIcon, HlmButton, HlmBadge],
  providers: [provideIcons({ lucideCheck, lucideCopy })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'bg-primary text-primary-foreground block' },
  template: `
    <section id="installa" aria-labelledby="installa-titolo" class="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-24">
      <p class="tag tag-inv">Installazione</p>
      <h2 id="installa-titolo" class="mt-3 max-w-3xl">Provalo in locale.</h2>
      <p class="text-primary-foreground/75 mt-4 max-w-2xl">
        Oggi Docker avvia il database PostgreSQL; l’API e l’interfaccia partono con Node e .NET dal repository. Al primo avvio trovi già i dati di esempio.
      </p>

      <div class="mt-8 grid gap-8 lg:grid-cols-[minmax(0,1fr)_minmax(0,22rem)]">
        <div class="min-w-0">
          <h3 class="flex items-center gap-2">Funziona già <span hlmBadge variant="secondary">Docker + Nx</span></h3>
          <p class="text-primary-foreground/75 mt-2 text-sm">Dalla cartella del repository (<a [href]="repo" class="inline-flex min-h-8 items-center underline underline-offset-4 max-sm:min-h-11" target="_blank" rel="noopener">GitHub<span class="sr-only"> (si apre in una nuova scheda)</span></a>), avvia prima il database:</p>
          <div class="bg-background text-foreground mt-3 flex items-stretch overflow-hidden rounded-lg">
            <pre #composeBox class="min-w-0 flex-1 overflow-x-auto p-4 font-mono text-sm" tabindex="0" aria-label="Comando Docker Compose"><code>{{ compose }}</code></pre>
            <button hlmBtn variant="secondary" type="button" class="m-2 min-h-10 shrink-0 self-center max-sm:min-h-11" aria-label="Copia il comando Docker Compose" (click)="copy(compose, composeBox)">
              <ng-icon [name]="copied() ? 'lucideCheck' : 'lucideCopy'" aria-hidden="true" />
              {{ copied() ? 'Copiato' : 'Copia' }}
            </button>
          </div>
          <p class="sr-only" role="status" aria-live="polite">{{ message() }}</p>
          @if (manual()) {
            <p class="text-primary-foreground mt-2 text-sm font-medium">Copia manualmente: il testo del comando è selezionato.</p>
          }
          <p class="text-primary-foreground/75 mt-4 text-sm">Poi avvia API e interfaccia (servono Node e .NET, versioni nel repository):</p>
          <pre class="bg-background text-foreground mt-3 overflow-x-auto rounded-lg p-4 font-mono text-sm" tabindex="0" aria-label="Comando per avviare API e interfaccia"><code>{{ serve }}</code></pre>
          <p class="text-primary-foreground/75 mt-4 text-sm">
            Apri <span class="text-primary-foreground font-mono">{{ localUrl }}</span>: vedi l’applicazione con i dati di esempio di un’officina di mobili (materiali, distinta, ordini e pianificazione già pronti).
          </p>

          <h3 class="mt-8 flex items-center gap-2">Script di installazione <span hlmBadge class="bg-signal-light text-primary border-transparent">In arrivo</span></h3>
          <p class="text-primary-foreground/75 mt-2 text-sm">Esempio di come sarà: lo script non è ancora pubblicato e l’indirizzo è un segnaposto.</p>
          <pre class="no-select bg-background text-foreground mt-3 overflow-x-auto rounded-lg p-4 font-mono text-sm opacity-80" tabindex="0" aria-label="Esempio di comando di installazione, non ancora disponibile"><code>{{ command }}</code></pre>
        </div>

        <ol class="grid gap-5 self-start">
          @for (s of steps; track s.n) {
            <li class="border-primary-foreground/25 flex gap-4 border-t-2 pt-3">
              <span class="text-signal-light font-mono text-2xl font-bold" aria-hidden="true">{{ s.n }}</span>
              <span>
                <strong class="block"><span class="sr-only">Passo {{ s.n }}: </span>{{ s.title }}</strong>
                <span class="text-primary-foreground/75 text-sm">{{ s.text }}</span>
                @if (s.href) {
                  <a [href]="s.href" class="text-primary-foreground mt-1 flex w-fit min-h-8 items-center text-sm underline underline-offset-4 max-sm:min-h-11" target="_blank" rel="noopener">Scarica Docker Desktop<span class="sr-only"> (si apre in una nuova scheda)</span></a>
                }
              </span>
            </li>
          }
        </ol>
      </div>
    </section>
  `,
})
export class InstallComponent {
  private readonly destroyRef = inject(DestroyRef);
  private timer: ReturnType<typeof setTimeout> | undefined;

  protected readonly command = LANDING_LINKS.installCommand;
  protected readonly compose = LANDING_LINKS.composeCommand;
  protected readonly serve = LANDING_LINKS.serveCommand;
  protected readonly localUrl = LANDING_LINKS.localUrl;
  protected readonly repo = LANDING_LINKS.repo;
  protected readonly copied = signal(false);
  protected readonly manual = signal(false);
  protected readonly message = signal('');
  protected readonly steps: { n: string; title: string; text: string; href?: string }[] = [
    { n: '0', title: 'Installa Docker Desktop', text: 'Serve per il database locale. Poi controlla che sia in esecuzione.', href: LANDING_LINKS.dockerInstall },
    { n: '1', title: 'Scarica il repository', text: 'Clona o scarica MicroMES da GitHub.' },
    { n: '2', title: 'Avvia il database', text: 'Esegui docker compose up -d: parte PostgreSQL.' },
    { n: '3', title: 'Avvia l’applicazione', text: 'Esegui pnpm install e npx nx serve web: partono API e interfaccia.' },
    { n: '4', title: 'Apri il browser', text: 'Vai su http://localhost:4300: trovi i dati di esempio già caricati.' },
  ];

  constructor() {
    this.destroyRef.onDestroy(() => clearTimeout(this.timer));
  }

  protected async copy(text: string, box: HTMLElement): Promise<void> {
    clearTimeout(this.timer);
    try {
      await navigator.clipboard.writeText(text);
      this.manual.set(false);
      this.copied.set(true);
      this.message.set('Comando copiato negli appunti');
      this.timer = setTimeout(() => {
        this.copied.set(false);
        this.message.set('');
      }, 2500);
    } catch {
      this.copied.set(false);
      this.manual.set(true);
      this.message.set('Copia manualmente: il testo del comando è selezionato');
      const range = document.createRange();
      range.selectNodeContents(box.querySelector('code') ?? box);
      const sel = window.getSelection();
      sel?.removeAllRanges();
      sel?.addRange(range);
    }
  }
}
