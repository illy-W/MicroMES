import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowRight, lucideGithub } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { LANDING_LINKS } from '../../landing-links';

@Component({
  selector: 'app-cta',
  imports: [HlmButton, NgIcon],
  providers: [provideIcons({ lucideArrowRight, lucideGithub })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <div class="hazard" aria-hidden="true"></div>
    <section aria-labelledby="cta-titolo" class="mx-auto w-full max-w-6xl px-4 py-16 text-center sm:px-6 sm:py-20">
      <h2 id="cta-titolo" class="mx-auto max-w-2xl">Metti in fila il lavoro di domani.</h2>
      <p class="text-muted-foreground mx-auto mt-4 max-w-xl">Provalo in locale con i dati di esempio: non costa nulla.</p>
      <div class="mt-8 flex flex-wrap justify-center gap-3">
        <a hlmBtn size="lg" class="max-sm:min-h-11" href="#installa">Provalo in locale <ng-icon name="lucideArrowRight" aria-hidden="true" /></a>
        <a hlmBtn size="lg" variant="outline" class="max-sm:min-h-11" [href]="repo" target="_blank" rel="noopener">
          <ng-icon name="lucideGithub" aria-hidden="true" /> Repository<span class="sr-only"> (si apre in una nuova scheda)</span>
        </a>
      </div>
    </section>
  `,
})
export class CtaComponent {
  protected readonly repo = LANDING_LINKS.repo;
}
