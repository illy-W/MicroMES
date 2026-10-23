import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowRight, lucideGithub } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { LANDING_LINKS } from '../../landing-links';
import { ProductImageComponent } from '../product-image.component';

@Component({
  selector: 'app-hero',
  imports: [HlmButton, NgIcon, ProductImageComponent],
  providers: [provideIcons({ lucideArrowRight, lucideGithub })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <div class="mx-auto w-full max-w-6xl px-4 pt-12 pb-10 sm:px-6 sm:pt-20">
      <p class="tag mb-4">MES open source per officine e laboratori</p>
      <h1 class="max-w-4xl">Il MES gratuito per le piccole officine.</h1>
      <p class="text-muted-foreground mt-6 max-w-2xl text-lg">
        MicroMES ti dice cosa produrre, cosa puoi avviare adesso e cosa è stato fatto davvero. Pensato per le PMI e le
        micro imprese da 1 a 20 persone: nessun canone, nessun consulente, i dati sul tuo server.
      </p>
      <div class="mt-8 flex flex-wrap items-center gap-3">
        <a hlmBtn size="lg" class="max-sm:min-h-11" href="#installa">Provalo in locale <ng-icon name="lucideArrowRight" aria-hidden="true" /></a>
        <a hlmBtn size="lg" variant="outline" class="max-sm:min-h-11" [href]="repo" target="_blank" rel="noopener">
          <ng-icon name="lucideGithub" aria-hidden="true" /> Vedi il codice
          <span class="sr-only">(si apre in una nuova scheda)</span>
        </a>
      </div>
      <dl class="mt-10 grid max-w-3xl grid-cols-1 gap-x-8 gap-y-3 sm:grid-cols-3">
        <div class="border-border border-t-signal border-t-2 pt-2">
          <dt class="tag">Prezzo</dt>
          <dd class="font-semibold">0 € per sempre</dd>
        </div>
        <div class="border-border border-t-signal border-t-2 pt-2">
          <dt class="tag">Licenza</dt>
          <dd class="font-semibold">Open source AGPL-3.0</dd>
        </div>
        <div class="border-border border-t-signal border-t-2 pt-2">
          <dt class="tag">Dati</dt>
          <dd class="font-semibold">Sul tuo server</dd>
        </div>
      </dl>
    </div>
    <div class="hazard" aria-hidden="true"></div>
    <div class="mx-auto -mt-px w-full max-w-6xl px-4 pb-16 sm:px-6">
      <figure class="border-border bg-card mt-8 overflow-hidden rounded-lg border-2 shadow-sm">
        <app-product-image
          name="overview"
          narrowName="overview-narrow"
          imgClass="max-sm:aspect-[748/400] max-sm:object-cover sm:aspect-[1600/527]"
          [width]="1600"
          [height]="527"
          [priority]="true"
          sizes="(min-width: 1152px) 1100px, 100vw"
          title="Panoramica di un ordine di produzione"
          caption="Un ordine di produzione con due semilavorati: ogni scheda è un ordine di lavoro con il suo stato (dati di esempio)."
          alt="Panoramica grafica di un ordine di produzione in MicroMES: ordini di lavoro come schede con stato, due ordini figli raggruppati e frecce di dipendenza."
        />
        <figcaption class="text-muted-foreground border-border border-t px-4 py-2 text-sm">
          Un ordine di produzione con due semilavorati: ogni scheda è un ordine di lavoro con il suo stato (dati di esempio).
        </figcaption>
      </figure>
    </div>
  `,
})
export class HeroComponent {
  protected readonly repo = LANDING_LINKS.repo;
}
