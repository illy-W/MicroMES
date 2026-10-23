import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCheck } from '@ng-icons/lucide';
import { LANDING_LINKS, LICENSE_NAME } from '../../landing-links';

@Component({
  selector: 'app-free',
  imports: [NgIcon],
  providers: [provideIcons({ lucideCheck })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'bg-muted block' },
  template: `
    <section id="gratis" aria-labelledby="gratis-titolo" class="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-24">
      <p class="tag">Gratis, per le PMI</p>
      <h2 id="gratis-titolo" class="mt-3 max-w-3xl">Un solo piano. Costa zero.</h2>
      <div class="mt-10 grid gap-8 lg:grid-cols-[minmax(0,26rem)_minmax(0,1fr)]">
        <div class="bg-primary text-primary-foreground flex flex-col justify-between rounded-lg p-6">
          <div>
            <p class="tag tag-inv">MicroMES</p>
            <p class="mt-2 flex items-baseline gap-2"><span class="text-6xl font-extrabold tracking-tight">0 €</span><span class="text-primary-foreground/75">per sempre</span></p>
            <p class="text-primary-foreground/75 mt-3 text-sm">Nessun canone, nessuna carta di credito, nessuna funzione “premium”.</p>
          </div>
          <div class="hazard mt-6 rounded-sm" aria-hidden="true"></div>
        </div>
        <div>
          <h3>Incluso, senza limiti</h3>
          <ul class="mt-4 grid gap-x-6 gap-y-2 sm:grid-cols-2">
            @for (i of included; track i) {
              <li class="flex items-start gap-2 text-sm">
                <ng-icon name="lucideCheck" class="mt-0.5 shrink-0" aria-hidden="true" /> {{ i }}
              </li>
            }
          </ul>
          <dl class="mt-8 grid gap-6 sm:grid-cols-2">
            <div>
              <dt class="font-semibold">Self-hosted: i dati sono tuoi</dt>
              <dd class="text-muted-foreground mt-1 text-sm">Gira sul tuo computer o server. Nessun cloud obbligato, nessun account da creare.</dd>
            </div>
            <div>
              <dt class="font-semibold">Open source, {{ license }}</dt>
              <dd class="text-muted-foreground mt-1 text-sm">
                Chiunque può usarlo e studiarlo; chi lo modifica o lo offre come servizio in rete deve pubblicare le sue modifiche.
                <a [href]="licenseUrl" class="text-foreground inline-flex items-center underline underline-offset-4 max-sm:min-h-11" target="_blank" rel="noopener">Testo della licenza<span class="sr-only"> (si apre in una nuova scheda)</span></a>
              </dd>
            </div>
          </dl>
        </div>
      </div>
    </section>
  `,
})
export class FreeComponent {
  protected readonly license = LICENSE_NAME;
  protected readonly licenseUrl = LANDING_LINKS.license;
  protected readonly included = [
    'Tutte le funzioni, nessuna riservata a un piano',
    'Utenti, ordini e macchine illimitati',
    'Aggiornamenti dal repository',
    'Dati in un database PostgreSQL tuo',
    'Codice sorgente leggibile e modificabile',
    'Nessun tracciamento né cookie su questo sito',
  ];
}
