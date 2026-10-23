import { ChangeDetectionStrategy, Component } from '@angular/core';
import { HlmAccordionImports } from '@spartan-ng/helm/accordion';

@Component({
  selector: 'app-faq',
  imports: [HlmAccordionImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <section id="faq" aria-labelledby="faq-titolo" class="mx-auto w-full max-w-3xl px-4 py-16 sm:px-6 sm:py-24">
      <p class="tag">Domande</p>
      <h2 id="faq-titolo" class="mt-3">Domande frequenti</h2>
      <hlm-accordion type="single" class="mt-8">
        @for (f of faqs; track f.q) {
          <hlm-accordion-item>
            <hlm-accordion-trigger>{{ f.q }}</hlm-accordion-trigger>
            <hlm-accordion-content>{{ f.a }}</hlm-accordion-content>
          </hlm-accordion-item>
        }
      </hlm-accordion>
    </section>
  `,
})
export class FaqComponent {
  protected readonly faqs = [
    {
      q: 'Cos’è il trucco? Come fa a essere gratis?',
      a: 'Nessun trucco. MicroMES è software open source con licenza AGPL-3.0: lo scarichi, lo installi e lo usi senza canoni né limiti. Chi vuole può pagare per consulenza, installazione o supporto, ma è facoltativo.',
    },
    {
      q: 'A chi è rivolto?',
      a: 'A laboratori, piccole officine e artigiani con 1-20 persone che producono su ordine o a piccoli lotti. Se oggi usi fogli di calcolo e lavagna, sei nel posto giusto.',
    },
    {
      q: 'Si integra con il mio ERP o gestionale?',
      a: 'Il modello dati segue il vocabolario ISA-95 (IEC 62264) in forma ridotta, pensato per restare integrabile con ERP e altri sistemi. Non ci sono ancora connettori pronti: oggi MicroMES è autonomo.',
    },
    {
      q: 'Dove stanno i miei dati?',
      a: 'Sul tuo computer o server, in un database PostgreSQL. MicroMES è self-hosted: nessun dato viene inviato a terzi e questo sito non usa cookie né tracciamento.',
    },
    {
      q: 'Cosa non c’è ancora?',
      a: 'La vista operatore per il banco e l’autenticazione con permessi per ruolo sono in arrivo. Magazzino, costi, acquisti e collegamento diretto alle macchine non sono inclusi per ora.',
    },
    {
      q: 'Posso modificarlo?',
      a: 'Sì, il codice è nel repository. La licenza AGPL-3.0 richiede di pubblicare le modifiche se offri il software ad altri come servizio in rete.',
    },
  ];
}
