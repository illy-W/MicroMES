import { ChangeDetectionStrategy, Component } from '@angular/core';
import { LANDING_LINKS, LICENSE_NAME } from '../../landing-links';

@Component({
  selector: 'app-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'bg-primary text-primary-foreground block' },
  template: `
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-4 px-4 py-8 text-sm sm:flex-row sm:items-center sm:justify-between sm:px-6">
      <p>MicroMES · MES gratuito e open source ({{ license }}) per le piccole officine.</p>
      <nav aria-label="Link del progetto" class="flex flex-wrap gap-x-5 gap-y-0">
        <a [href]="links.repo" class="inline-flex min-h-8 items-center underline underline-offset-4 max-sm:min-h-11" target="_blank" rel="noopener">Repository<span class="sr-only"> (si apre in una nuova scheda)</span></a>
        <a [href]="links.license" class="inline-flex min-h-8 items-center underline underline-offset-4 max-sm:min-h-11" target="_blank" rel="noopener">Licenza<span class="sr-only"> (si apre in una nuova scheda)</span></a>
        <a [href]="links.issues" class="inline-flex min-h-8 items-center underline underline-offset-4 max-sm:min-h-11" target="_blank" rel="noopener">Segnala un problema<span class="sr-only"> (si apre in una nuova scheda)</span></a>
      </nav>
    </div>
  `,
})
export class FooterComponent {
  protected readonly links = LANDING_LINKS;
  protected readonly license = LICENSE_NAME;
}
