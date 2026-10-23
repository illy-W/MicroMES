import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideMail } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { LANDING_LINKS } from '../../landing-links';

@Component({
  selector: 'app-support',
  imports: [HlmButton, NgIcon],
  providers: [provideIcons({ lucideMail })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'bg-muted block' },
  template: `
    <section id="supporto" aria-labelledby="supporto-titolo" class="mx-auto w-full max-w-6xl px-4 py-16 sm:px-6 sm:py-20">
      <div class="grid items-center gap-8 md:grid-cols-[1fr_auto]">
        <div>
          <p class="tag">Serve una mano?</p>
          <h2 id="supporto-titolo" class="mt-3">Il software è gratis. L’aiuto è facoltativo.</h2>
          <p class="text-muted-foreground mt-4 max-w-2xl">
            Non serve alcun consulente per usare MicroMES. Se però preferisci farti aiutare, puoi richiedere in modo
            facoltativo e a pagamento consulenza, installazione sul tuo server e supporto. Il prodotto resta lo stesso, con
            tutte le funzioni, anche senza.
          </p>
        </div>
        <div>
          <a hlmBtn size="lg" class="max-sm:min-h-11" [href]="contactHref"><ng-icon name="lucideMail" aria-hidden="true" /> Scrivici</a>
          <p class="text-muted-foreground mt-2 text-xs">{{ email }} (indirizzo di esempio)</p>
        </div>
      </div>
    </section>
  `,
})
export class SupportComponent {
  protected readonly contactHref = LANDING_LINKS.contactHref;
  protected readonly email = LANDING_LINKS.contactEmail;
}
